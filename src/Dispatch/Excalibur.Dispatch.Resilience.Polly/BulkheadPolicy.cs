// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Threading.RateLimiting;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

using Polly;

namespace Excalibur.Dispatch.Resilience.Polly;

/// <summary>
/// Bulkhead policy implementation using Polly.
/// </summary>
public partial class BulkheadPolicy : IBulkheadPolicy, IDisposable, IAsyncDisposable
{
	private readonly string _name;
	private readonly BulkheadOptions _options;
	private readonly ILogger _logger;
	private readonly ResiliencePipeline _pipeline;
	private readonly ConcurrencyLimiter _limiter;
	private readonly int _queueLimit;
	private volatile bool _disposed;
	private long _totalExecutions;
	private long _rejectedExecutions;
	private long _queuedExecutions;
	private int _activeExecutions;

	/// <summary>
	/// Initializes a new instance of the <see cref="BulkheadPolicy" /> class.
	/// </summary>
	/// <param name="name">The bulkhead identifier.</param>
	/// <param name="options">The bulkhead configuration options.</param>
	/// <param name="logger">The logger used for diagnostic output.</param>
	public BulkheadPolicy(string name, BulkheadOptions options, ILogger? logger = null)
	{
		_name = name ?? throw new ArgumentNullException(nameof(name));
		_options = options ?? throw new ArgumentNullException(nameof(options));
		_logger = logger ?? NullLogger.Instance;

		// Queueing is a configured capability, so a bulkhead with it turned off admits no waiters at all.
		_queueLimit = _options.AllowQueueing ? _options.MaxQueueLength : 0;

		_limiter = new ConcurrencyLimiter(new ConcurrencyLimiterOptions
		{
			PermitLimit = _options.MaxConcurrency,
			QueueLimit = _queueLimit,

			// OldestFirst is the only order that rejects the arriving caller when the queue is full.
			// NewestFirst would evict a caller that is already waiting to make room for the new one,
			// which is a different contract from the one this bulkhead advertises. It also makes the
			// queue FIFO, where the semaphore it replaces left ordering unspecified.
			QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
		});

		_pipeline = new ResiliencePipelineBuilder()
			.AddTimeout(_options.OperationTimeout)
			.Build();
	}

	/// <inheritdoc />
	public bool HasCapacity
	{
		get
		{
			var statistics = TryGetStatistics();
			return statistics is null || statistics.CurrentAvailablePermits > 0 || statistics.CurrentQueuedCount < _queueLimit;
		}
	}

	/// <inheritdoc />
	public async Task<T> ExecuteAsync<T>(Func<Task<T>> operation, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(operation);

		_ = Interlocked.Increment(ref _totalExecutions);

		var startTime = DateTimeOffset.UtcNow;

		// A lease that completes synchronously never entered the queue; one that does not, did. The
		// limiter enforces MaxQueueLength itself, so there is no counter to keep in step with it.
		var acquisition = _limiter.AcquireAsync(permitCount: 1, cancellationToken);
		var queued = !acquisition.IsCompleted;
		if (queued)
		{
			_ = Interlocked.Increment(ref _queuedExecutions);
			LogBulkheadQueueing(_name, (int)(TryGetStatistics()?.CurrentQueuedCount ?? 0), _queueLimit);
		}

		using var lease = await acquisition.ConfigureAwait(false);
		if (!lease.IsAcquired)
		{
			_ = Interlocked.Increment(ref _rejectedExecutions);
			LogBulkheadRejected(_name, _queueLimit);
			throw new BulkheadRejectedException($"Bulkhead '{_name}' queue is full");
		}

		try
		{
			_ = Interlocked.Increment(ref _activeExecutions);
			LogBulkheadExecuting(_name, _activeExecutions, _options.MaxConcurrency);

			var result = await _pipeline.ExecuteAsync(
				async _ => await operation().ConfigureAwait(false),
				cancellationToken).ConfigureAwait(false);

			var duration = (DateTimeOffset.UtcNow - startTime).TotalMilliseconds;
			LogBulkheadCompleted(_name, duration);

			return result;
		}
		finally
		{
			_ = Interlocked.Decrement(ref _activeExecutions);
		}
	}

	/// <inheritdoc />
	public BulkheadMetrics GetMetrics()
	{
		var statistics = TryGetStatistics();

		return new BulkheadMetrics
		{
			Name = _name,
			MaxConcurrency = _options.MaxConcurrency,
			MaxQueueLength = _options.MaxQueueLength,
			ActiveExecutions = Volatile.Read(ref _activeExecutions),
			QueueLength = (int)(statistics?.CurrentQueuedCount ?? 0),
			TotalExecutions = Interlocked.Read(ref _totalExecutions),
			RejectedExecutions = Interlocked.Read(ref _rejectedExecutions),
			QueuedExecutions = Interlocked.Read(ref _queuedExecutions),
			AvailableCapacity = (int)(statistics?.CurrentAvailablePermits ?? 0),
		};
	}

	/// <summary>
	/// Reads the limiter's live counters, or <see langword="null" /> once the policy is disposed.
	/// </summary>
	/// <remarks>
	/// Reading diagnostics off a disposed bulkhead is a normal thing for a shutdown path to do, and it
	/// must not throw. The limiter refuses once disposed, so the disposed case reports nothing rather
	/// than failing.
	/// </remarks>
	/// <returns>The limiter statistics, or <see langword="null" /> if this policy has been disposed.</returns>
	private RateLimiterStatistics? TryGetStatistics() => _disposed ? null : _limiter.GetStatistics();

	/// <inheritdoc />
	public void Dispose()
	{
		Dispose(disposing: true);
		GC.SuppressFinalize(this);
	}

	/// <summary>
	/// Releases managed resources.
	/// </summary>
	/// <param name="disposing">Indicates whether the method is called from <see cref="Dispose()"/>.</param>
	protected virtual void Dispose(bool disposing)
	{
		if (_disposed)
		{
			return;
		}

		// Set before releasing the limiter, so a concurrent metrics read sees "disposed" rather than
		// reaching a limiter that is already gone.
		_disposed = true;

		if (disposing)
		{
			_limiter.Dispose();
		}
	}

	/// <summary>
	/// Asynchronously releases resources used by the bulkhead policy.
	/// </summary>
	public async ValueTask DisposeAsync()
	{
		if (_disposed)
		{
			return;
		}

		_disposed = true;
		await _limiter.DisposeAsync().ConfigureAwait(false);
		GC.SuppressFinalize(this);
	}

	// Source-generated logging methods
	[LoggerMessage(ResilienceEventId.BulkheadExecuting, LogLevel.Debug,
		"Bulkhead '{Name}' executing operation. Active: {Active}/{MaxConcurrency}")]
	private partial void LogBulkheadExecuting(string name, int active, int maxConcurrency);

	[LoggerMessage(ResilienceEventId.BulkheadExecutionRejected, LogLevel.Warning,
		"Bulkhead '{Name}' rejected operation. Queue full at {QueueLength}")]
	private partial void LogBulkheadRejected(string name, int queueLength);

	[LoggerMessage(ResilienceEventId.BulkheadCompleted, LogLevel.Debug,
		"Bulkhead '{Name}' completed operation in {Duration}ms")]
	private partial void LogBulkheadCompleted(string name, double duration);

	[LoggerMessage(ResilienceEventId.BulkheadQueueing, LogLevel.Information,
		"Bulkhead '{Name}' queueing operation. Queue: {QueueLength}/{MaxQueueLength}")]
	private partial void LogBulkheadQueueing(string name, int queueLength, int maxQueueLength);
}
