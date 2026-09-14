// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Collections.Concurrent;

using Excalibur.Dispatch.Options.Resilience;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Resilience.Polly;

/// <summary>
/// Polly-based implementation of <see cref="ITransportCircuitBreakerRegistry"/> that manages
/// per-transport circuit breakers using Polly's resilience pipeline.
/// </summary>
/// <remarks>
/// <para>
/// This registry creates and manages <see cref="PollyCircuitBreakerPolicyAdapter"/> instances
/// for each transport, ensuring transport-level isolation of failures. When one transport
/// experiences issues, other transports continue operating normally.
/// </para>
/// <para>
/// The registry is thread-safe and lazily creates circuit breakers on first access.
/// </para>
/// </remarks>
public sealed partial class PollyTransportCircuitBreakerRegistry : ITransportCircuitBreakerRegistry, ITransportCircuitBreakerDiagnostics, IDisposable
{
	/// <summary>
	/// Upper bound on distinct circuits held at once, matching the in-box registry. The circuit key can
	/// be derived from the message through <see cref="CircuitBreakerOptions.CircuitKeySelector"/>, so an
	/// unbounded map is a memory-growth vector driven by message content. This registry previously had
	/// no bound at all, so swapping the package reference silently turned a bounded registry into an
	/// unbounded one -- a divergence a consumer choosing an implementation by editing a project file has
	/// no way to see.
	/// </summary>
	internal const int MaxBreakers = 1024;

	/// <summary>
	/// Monotonic ticket source for recency. A counter rather than a clock: eviction needs only an
	/// ordering, and a counter cannot be perturbed by a clock adjustment.
	/// </summary>
	private long _accessTicket;

	private readonly ConcurrentDictionary<string, Entry> _circuitBreakers = new(StringComparer.OrdinalIgnoreCase);
	private readonly CircuitBreakerOptions _defaultOptions;
	private readonly ILogger _logger;
	private volatile bool _disposed;

	/// <summary>
	/// Initializes a new instance of the <see cref="PollyTransportCircuitBreakerRegistry"/> class
	/// with default options.
	/// </summary>
	public PollyTransportCircuitBreakerRegistry()
		: this(new CircuitBreakerOptions(), null)
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="PollyTransportCircuitBreakerRegistry"/> class.
	/// </summary>
	/// <param name="defaultOptions">The default circuit breaker options for new registrations.</param>
	/// <param name="logger">Optional logger instance.</param>
	public PollyTransportCircuitBreakerRegistry(
		CircuitBreakerOptions defaultOptions,
		ILogger<PollyTransportCircuitBreakerRegistry>? logger)
	{
		_defaultOptions = defaultOptions ?? throw new ArgumentNullException(nameof(defaultOptions));
		_logger = logger ?? NullLogger<PollyTransportCircuitBreakerRegistry>.Instance;
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="PollyTransportCircuitBreakerRegistry"/> class
	/// using options from dependency injection.
	/// </summary>
	/// <param name="options">The circuit breaker options accessor.</param>
	/// <param name="logger">Optional logger instance.</param>
	public PollyTransportCircuitBreakerRegistry(
		IOptions<CircuitBreakerOptions> options,
		ILogger<PollyTransportCircuitBreakerRegistry>? logger = null)
		: this(options?.Value ?? new CircuitBreakerOptions(), logger)
	{
	}

	/// <inheritdoc />
	public int Count => _circuitBreakers.Count;

	/// <inheritdoc />
	public ICircuitBreakerPolicy GetOrCreate(string transportName)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(transportName);
		ObjectDisposedException.ThrowIf(_disposed, this);

		return GetOrCreateBounded(transportName, _defaultOptions);
	}

	/// <inheritdoc />
	public ICircuitBreakerPolicy GetOrCreate(string transportName, CircuitBreakerOptions options)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(transportName);
		ArgumentNullException.ThrowIfNull(options);
		ObjectDisposedException.ThrowIf(_disposed, this);

		return GetOrCreateBounded(transportName, options);
	}

	/// <inheritdoc />
	public ICircuitBreakerPolicy? TryGet(string transportName)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(transportName);
		ObjectDisposedException.ThrowIf(_disposed, this);

		return _circuitBreakers.TryGetValue(transportName, out var entry) ? entry.Policy : null;
	}

	/// <inheritdoc />
	public bool Remove(string transportName)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(transportName);
		ObjectDisposedException.ThrowIf(_disposed, this);

		if (_circuitBreakers.TryRemove(transportName, out var entry))
		{
			// Dispose if the circuit breaker implements IDisposable
			(entry.Policy as IDisposable)?.Dispose();
			LogCircuitBreakerRemoved(transportName);
			return true;
		}

		return false;
	}

	/// <inheritdoc />
	public async Task ResetAllAsync(CancellationToken cancellationToken)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);

		// Awaited in sequence, matching the in-box registry: the postcondition is that EVERY circuit is
		// closed on return. Each adapter's close is a real await here rather than a fire-and-forget, so
		// the count logged below is a count of circuits actually closed.
		var count = 0;
		foreach (var kvp in _circuitBreakers)
		{
			await kvp.Value.Policy.ResetAsync(cancellationToken).ConfigureAwait(false);
			count++;
		}

		LogAllCircuitBreakersReset(count);
	}

	/// <inheritdoc />
	public IReadOnlyDictionary<string, CircuitState> GetAllStates()
	{
		ObjectDisposedException.ThrowIf(_disposed, this);

		var states = new Dictionary<string, CircuitState>(StringComparer.OrdinalIgnoreCase);
		foreach (var kvp in _circuitBreakers)
		{
			states[kvp.Key] = kvp.Value.Policy.State;
		}

		return states;
	}

	/// <inheritdoc />
	public IEnumerable<string> GetTransportNames()
	{
		ObjectDisposedException.ThrowIf(_disposed, this);
		return _circuitBreakers.Keys;
	}

	/// <summary>
	/// Disposes all managed circuit breakers and clears the registry.
	/// </summary>
	public void Dispose()
	{
		if (_disposed)
		{
			return;
		}

		_disposed = true;

		foreach (var kvp in _circuitBreakers)
		{
			(kvp.Value.Policy as IDisposable)?.Dispose();
		}

		_circuitBreakers.Clear();
	}

	/// <summary>
	/// Returns the circuit for <paramref name="transportName"/>, creating it within the bound.
	/// </summary>
	/// <param name="transportName">The circuit key.</param>
	/// <param name="options">Options to build the circuit with when it does not already exist.</param>
	/// <returns>The circuit for that key. Never another key's circuit, and never one that is not retained.</returns>
	/// <remarks>
	/// Eviction prefers a circuit that is closed and carrying no failures; when none is idle the least
	/// recently used is evicted whatever its state. An unstored circuit is deliberately NOT an option: it
	/// would make GetOrCreate report success for a key TryGet then cannot find, which is the same
	/// incoherence as handing out a shared circuit, moved into a branch nobody exercises.
	/// </remarks>
	private ICircuitBreakerPolicy GetOrCreateBounded(string transportName, CircuitBreakerOptions options)
	{
		if (_circuitBreakers.TryGetValue(transportName, out var existing))
		{
			existing.Touch(Interlocked.Increment(ref _accessTicket));
			return existing.Policy;
		}

		if (_circuitBreakers.Count >= MaxBreakers)
		{
			_ = TryEvictIdleCircuit() || TryEvictLeastRecentlyUsed();
		}

		var created = false;
		var entry = _circuitBreakers.GetOrAdd(transportName, name =>
		{
			created = true;
			return new Entry(CreateCircuitBreaker(name, options));
		});

		if (created)
		{
			LogCircuitBreakerCreated(transportName);
		}

		entry.Touch(Interlocked.Increment(ref _accessTicket));
		return entry.Policy;
	}

	/// <summary>Evicts the least recently used circuit that is provably idle, disposing it.</summary>
	/// <returns><see langword="true"/> when a circuit was evicted; otherwise <see langword="false"/>.</returns>
	/// <remarks>
	/// Idle means closed AND carrying no consecutive failures, so an open or recovering circuit is never
	/// the first choice -- evicting one sends traffic straight back at the dependency it shields. A policy
	/// that does not expose <see cref="ICircuitBreakerDiagnostics"/> is treated as NOT evictable, because
	/// its failure count cannot be read and the fail-safe reading of an unknown is to keep the circuit.
	/// The evicted adapter is disposed, matching what Remove already does: it owns a resilience pipeline.
	/// </remarks>
	private bool TryEvictIdleCircuit()
	{
		string? victim = null;
		var oldest = long.MaxValue;

		foreach (var candidate in _circuitBreakers)
		{
			if (candidate.Value.Policy is not ICircuitBreakerDiagnostics diagnostics
				|| candidate.Value.Policy.State != CircuitState.Closed
				|| diagnostics.ConsecutiveFailures > 0)
			{
				continue;
			}

			var ticket = candidate.Value.LastAccess;
			if (ticket < oldest)
			{
				oldest = ticket;
				victim = candidate.Key;
			}
		}

		return TryEvict(victim);
	}

	/// <summary>Evicts the least recently used circuit whatever its state, disposing it.</summary>
	/// <returns><see langword="true"/> when a circuit was evicted; otherwise <see langword="false"/>.</returns>
	/// <remarks>
	/// Reached only when no circuit is idle, so some protection must be given up whichever is chosen.
	/// Giving up the least recently used one keeps the map coherent, which is the property that must hold
	/// on every path.
	/// </remarks>
	private bool TryEvictLeastRecentlyUsed()
	{
		string? victim = null;
		var oldest = long.MaxValue;

		foreach (var candidate in _circuitBreakers)
		{
			var ticket = candidate.Value.LastAccess;
			if (ticket < oldest)
			{
				oldest = ticket;
				victim = candidate.Key;
			}
		}

		return TryEvict(victim);
	}

	private bool TryEvict(string? victim)
	{
		if (victim is null || !_circuitBreakers.TryRemove(victim, out var evicted))
		{
			return false;
		}

		(evicted.Policy as IDisposable)?.Dispose();
		LogCircuitBreakerRemoved(victim);
		return true;
	}

	/// <summary>A circuit and the recency needed to choose an eviction victim.</summary>
	/// <param name="policy">The circuit this entry holds.</param>
	private sealed class Entry(ICircuitBreakerPolicy policy)
	{
		private long _lastAccess;

		/// <summary>Gets the circuit this entry holds.</summary>
		/// <value>The policy handed to callers for this key.</value>
		public ICircuitBreakerPolicy Policy { get; } = policy;

		/// <summary>Gets the ticket recorded at this entry's most recent use.</summary>
		/// <value>A monotonically increasing value; lower means less recently used.</value>
		public long LastAccess => Volatile.Read(ref _lastAccess);

		/// <summary>Records that this entry has just been used.</summary>
		/// <param name="ticket">The ticket to record.</param>
		public void Touch(long ticket) => Volatile.Write(ref _lastAccess, ticket);
	}

	private PollyCircuitBreakerPolicyAdapter CreateCircuitBreaker(
			string transportName,
			CircuitBreakerOptions options) =>
			new(options, transportName, _logger);

	// Source-generated logging methods
	[LoggerMessage(ResilienceEventId.TransportCircuitBreakerRegistered, LogLevel.Debug,
		"Created circuit breaker for transport: {TransportName}")]
	private partial void LogCircuitBreakerCreated(string transportName);

	[LoggerMessage(ResilienceEventId.TransportCircuitBreakerUnregistered, LogLevel.Debug,
		"Removed circuit breaker for transport: {TransportName}")]
	private partial void LogCircuitBreakerRemoved(string transportName);

	[LoggerMessage(ResilienceEventId.AllCircuitBreakersReset, LogLevel.Information,
		"Reset {Count} circuit breakers")]
	private partial void LogAllCircuitBreakersReset(int count);
}
