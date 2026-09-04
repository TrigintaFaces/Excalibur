// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Metrics;

using Excalibur.Dispatch.Caching.Diagnostics;
using Excalibur.Dispatch.Resilience;

using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Caching;

/// <summary>
/// Bounds every asynchronous call to the distributed (L2) cache backend by
/// <see cref="CacheBehaviorOptions.CacheTimeout"/>, so a slow backend degrades to a cache miss instead of
/// stalling the request.
/// </summary>
/// <remarks>
/// <para>
/// The bound belongs here rather than around the cache lookup-or-create operation as a whole. That
/// operation runs the message handler inside it and is shared by every concurrent caller of the same key,
/// so a per-caller deadline placed around it bounds handler execution and, worse, is abandoned
/// independently by each caller — collapsing the single-flight behaviour that makes caching worth having.
/// A deadline applied to the backend call itself sits inside the shared operation: one slow read produces
/// one timeout, all waiting callers observe the same outcome, and exactly one of them executes the handler.
/// </para>
/// <para>
/// A timed-out read is reported as a miss and a timed-out write is dropped, matching how the underlying
/// hybrid cache already treats a backend that throws: the cache is an optimisation and must never fail the
/// operation it is accelerating. A dropped write costs a repeated handler execution later; it never
/// produces a wrong answer.
/// </para>
/// <para>
/// The synchronous members pass straight through. A blocking call cannot be abandoned without leaking the
/// thread that is stuck in it, and the hybrid cache uses only the asynchronous members.
/// </para>
/// </remarks>
internal class TimeoutDistributedCache : IDistributedCache
{
	private readonly IDistributedCache _inner;
	private readonly IOptions<CacheOptions> _options;
	private readonly ILogger _logger;
	private readonly Counter<long>? _timeoutCounter;
	private readonly ICircuitBreakerPolicy? _circuitBreaker;
	private readonly IOptions<CacheOptions> _breakerOptions;

	/// <summary>
	/// Initializes a new instance of the <see cref="TimeoutDistributedCache"/> class.
	/// </summary>
	/// <param name="inner">The distributed cache being bounded.</param>
	/// <param name="options">Cache options supplying <see cref="CacheBehaviorOptions.CacheTimeout"/>.</param>
	/// <param name="meterFactory">
	/// The meter factory used to record timeout counts, or <see langword="null"/> in a composition without
	/// metrics. Bounding backend latency is a correctness concern and must not require a consumer to
	/// register telemetry to obtain it.
	/// </param>
	/// <param name="logger">Logger for timeout diagnostics.</param>
	/// <param name="circuitBreaker">
	/// Optional breaker guarding the backend. This decorator is the backend boundary, so it is the only
	/// component that can observe backend health: a swallowed timeout is invisible above it, and the
	/// middleware sees an ordinary cache miss. Reporting here is what allows a chronically slow backend to
	/// open the breaker and stop every request paying the deadline twice.
	/// </param>
	[SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope",
		Justification = "The meter is owned by the IMeterFactory, which disposes it with the container.")]
	public TimeoutDistributedCache(
		IDistributedCache inner,
		IOptions<CacheOptions> options,
		IMeterFactory? meterFactory,
		ILogger<TimeoutDistributedCache> logger,
		ICircuitBreakerPolicy? circuitBreaker = null)
	{
		ArgumentNullException.ThrowIfNull(inner);
		ArgumentNullException.ThrowIfNull(options);
		ArgumentNullException.ThrowIfNull(logger);

		_inner = inner;
		_options = options;
		_breakerOptions = options;
		_logger = logger;
		_circuitBreaker = circuitBreaker;
		_timeoutCounter = meterFactory?
			.Create(DispatchCachingTelemetryConstants.MeterName)
			.CreateCounter<long>("dispatch.cache.timeouts", description: "Number of cache operation timeouts");
	}

	/// <summary>
	/// Gets the distributed cache being bounded.
	/// </summary>
	/// <value>The inner distributed cache.</value>
	internal IDistributedCache Inner => _inner;

	/// <inheritdoc />
	public byte[]? Get(string key) => _inner.Get(key);

	/// <inheritdoc />
	public void Refresh(string key) => _inner.Refresh(key);

	/// <inheritdoc />
	public void Remove(string key) => _inner.Remove(key);

	/// <inheritdoc />
	public void Set(string key, byte[] value, DistributedCacheEntryOptions options) => _inner.Set(key, value, options);

	/// <inheritdoc />
	public Task<byte[]?> GetAsync(string key, CancellationToken token = default)
		=> RunAsync(static (cache, key, ct) => cache.GetAsync(key, ct), _inner, key, onTimeout: null, token);

	/// <inheritdoc />
	public Task RefreshAsync(string key, CancellationToken token = default)
		=> RunAsync(static async (cache, key, ct) => { await cache.RefreshAsync(key, ct).ConfigureAwait(false); return (byte[]?)null; }, _inner, key, onTimeout: null, token);

	/// <inheritdoc />
	public Task RemoveAsync(string key, CancellationToken token = default)
		=> RunAsync(static async (cache, key, ct) => { await cache.RemoveAsync(key, ct).ConfigureAwait(false); return (byte[]?)null; }, _inner, key, onTimeout: null, token);

	/// <inheritdoc />
	public Task SetAsync(string key, byte[] value, DistributedCacheEntryOptions options, CancellationToken token = default)
		=> RunAsync(
			async (cache, k, ct) => { await cache.SetAsync(k, value, options, ct).ConfigureAwait(false); return (byte[]?)null; },
			_inner,
			key,
			onTimeout: null,
			token);

	/// <summary>
	/// Runs a backend operation under <see cref="CacheBehaviorOptions.CacheTimeout"/>, returning
	/// <paramref name="onTimeout"/> when the deadline is reached before the caller cancels.
	/// </summary>
	/// <typeparam name="TResult">The operation result type.</typeparam>
	/// <param name="operation">The backend operation to run.</param>
	/// <param name="cache">The backend to run it against.</param>
	/// <param name="key">The cache key, for diagnostics.</param>
	/// <param name="onTimeout">The value to return when the deadline is reached.</param>
	/// <param name="token">The caller's cancellation token, which always propagates.</param>
	/// <returns>The operation result, or <paramref name="onTimeout"/> when the deadline is reached.</returns>
	protected async Task<TResult> RunAsync<TResult>(
		Func<IDistributedCache, string, CancellationToken, Task<TResult>> operation,
		IDistributedCache cache,
		string key,
		TResult onTimeout,
		CancellationToken token)
	{
		var timeout = _options.Value.Behavior.CacheTimeout;
		if (timeout <= TimeSpan.Zero)
		{
			return await operation(cache, key, token).ConfigureAwait(false);
		}

		using var cts = CancellationTokenSource.CreateLinkedTokenSource(token);
		cts.CancelAfter(timeout);

		var breaker = BreakerIfEnabled();

		try
		{
			// The operation runs THROUGH the breaker rather than beside it. Reporting outcomes to the
			// breaker afterwards left the Polly-backed implementation counting into a field its own
			// strategy never reads, so the circuit could not open however badly the backend behaved.
			// A backend that fails FAST is as unhealthy as one that fails slow, and both reach the
			// breaker here because both leave this delegate as an exception.
			return breaker is null
				? await RunOnceAsync(operation, cache, key, cts.Token).ConfigureAwait(false)
				: await breaker.ExecuteAsync(
					ct => RunOnceAsync(operation, cache, key, ct),
					cts.Token).ConfigureAwait(false);
		}
		catch (OperationCanceledException) when (!token.IsCancellationRequested)
		{
			// The breaker has already seen this as a failure; the caller still gets the fail-open value,
			// because a cache must not turn a slow backend into a broken request.
			RecordTimeout(key, timeout);
			return onTimeout;
		}
		catch (CircuitBreakerOpenException)
		{
			// Optional infrastructure fails open. An open circuit means stop paying for the backend,
			// not fail the request that happened to arrive while it was open.
			return onTimeout;
		}
	}

	private static Task<TResult> RunOnceAsync<TResult>(
		Func<IDistributedCache, string, CancellationToken, Task<TResult>> operation,
		IDistributedCache cache,
		string key,
		CancellationToken token) => operation(cache, key, token);

	/// <summary>Gets the breaker to run through, or <see langword="null"/> when none is configured.</summary>
	private ICircuitBreakerPolicy? BreakerIfEnabled() =>
		_circuitBreaker is not null && _breakerOptions.Value.Resilience.CircuitBreaker.Enabled
			? _circuitBreaker
			: null;



	/// <summary>
	/// Records a backend timeout to the meter and the log.
	/// </summary>
	/// <param name="key">The cache key whose operation timed out.</param>
	/// <param name="timeout">The deadline that was exceeded.</param>
	protected void RecordTimeout(string key, TimeSpan timeout)
	{
		_timeoutCounter?.Add(1);
		_logger.LogDebug(
			"Distributed cache operation for key {CacheKey} exceeded {CacheTimeout} and was treated as a miss.",
			key,
			timeout);
	}

	/// <summary>
	/// Gets the configured backend deadline.
	/// </summary>
	/// <returns>The configured <see cref="CacheBehaviorOptions.CacheTimeout"/>.</returns>
	protected TimeSpan GetTimeout() => _options.Value.Behavior.CacheTimeout;
}

/// <summary>
/// A <see cref="TimeoutDistributedCache"/> that also forwards the buffer-based cache surface, so bounding a
/// backend does not cost the allocation-free read and write path the hybrid cache prefers when the backend
/// supports it.
/// </summary>
internal sealed class BufferTimeoutDistributedCache : TimeoutDistributedCache, IBufferDistributedCache
{
	private readonly IBufferDistributedCache _inner;

	/// <summary>
	/// Initializes a new instance of the <see cref="BufferTimeoutDistributedCache"/> class.
	/// </summary>
	/// <param name="inner">The buffer-capable distributed cache being bounded.</param>
	/// <param name="options">Cache options supplying <see cref="CacheBehaviorOptions.CacheTimeout"/>.</param>
	/// <param name="meterFactory">The meter factory used to record timeout counts, or <see langword="null"/>.</param>
	/// <param name="logger">Logger for timeout diagnostics.</param>
	/// <param name="circuitBreaker">
	/// Optional breaker guarding the backend, forwarded to the base decorator so a bounded buffer read or
	/// write reports backend health exactly as the byte-array path does.
	/// </param>
	public BufferTimeoutDistributedCache(
		IBufferDistributedCache inner,
		IOptions<CacheOptions> options,
		IMeterFactory? meterFactory,
		ILogger<TimeoutDistributedCache> logger,
		ICircuitBreakerPolicy? circuitBreaker = null)
		: base(inner, options, meterFactory, logger, circuitBreaker) => _inner = inner;

	/// <inheritdoc />
	public bool TryGet(string key, IBufferWriter<byte> destination) => _inner.TryGet(key, destination);

	/// <inheritdoc />
	public void Set(string key, ReadOnlySequence<byte> value, DistributedCacheEntryOptions options) => _inner.Set(key, value, options);

	/// <inheritdoc />
	public async ValueTask<bool> TryGetAsync(string key, IBufferWriter<byte> destination, CancellationToken token = default)
	{
		var timeout = GetTimeout();
		if (timeout <= TimeSpan.Zero)
		{
			return await _inner.TryGetAsync(key, destination, token).ConfigureAwait(false);
		}

		using var cts = CancellationTokenSource.CreateLinkedTokenSource(token);
		cts.CancelAfter(timeout);

		try
		{
			return await _inner.TryGetAsync(key, destination, cts.Token).ConfigureAwait(false);
		}
		catch (OperationCanceledException) when (!token.IsCancellationRequested)
		{
			RecordTimeout(key, timeout);
			return false;
		}
	}

	/// <inheritdoc />
	public async ValueTask SetAsync(string key, ReadOnlySequence<byte> value, DistributedCacheEntryOptions options, CancellationToken token = default)
	{
		var timeout = GetTimeout();
		if (timeout <= TimeSpan.Zero)
		{
			await _inner.SetAsync(key, value, options, token).ConfigureAwait(false);
			return;
		}

		using var cts = CancellationTokenSource.CreateLinkedTokenSource(token);
		cts.CancelAfter(timeout);

		try
		{
			await _inner.SetAsync(key, value, options, cts.Token).ConfigureAwait(false);
		}
		catch (OperationCanceledException) when (!token.IsCancellationRequested)
		{
			RecordTimeout(key, timeout);
		}
	}
}
