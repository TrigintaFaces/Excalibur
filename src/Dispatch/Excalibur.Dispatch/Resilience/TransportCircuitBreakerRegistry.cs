// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Collections.Concurrent;

using Excalibur.Dispatch.Options.Resilience;

using Microsoft.Extensions.Logging;

namespace Excalibur.Dispatch.Resilience;

/// <summary>
/// Registry for managing per-transport circuit breaker instances.
/// </summary>
/// <remarks>
/// <para>
/// Each transport (e.g., RabbitMQ, Azure Service Bus, Kafka) gets its own circuit breaker
/// to prevent failures in one transport from affecting others. That isolation is the guarantee: a
/// circuit is never shared between keys, so one dependency's failures cannot open another's circuit.
/// </para>
/// <para>
/// The registry is bounded, because the circuit key may be derived from message content. When the
/// bound is reached it evicts the least recently used circuit that is closed and carrying no
/// failures; a circuit that is open or recovering is evicted only when no idle circuit remains (see
/// below). An evicted key's next circuit
/// starts without its predecessor's history.
/// </para>
/// <para>
/// If every circuit is open, recovering, or accumulating failures, the least recently used one is
/// evicted anyway. Under that pressure some protection has to be given up whichever circuit is
/// chosen, and the transport that loses its circuit starts its next failure run from zero. What is
/// never given up is coherence: a key handed a circuit can always retrieve the same one, no key ever
/// receives another key's circuit or another key's settings, and dispatch is never refused to keep
/// the bound.
/// </para>
/// </remarks>
internal sealed class TransportCircuitBreakerRegistry : ITransportCircuitBreakerRegistry, ITransportCircuitBreakerDiagnostics
{
	/// <summary>
	/// Upper bound on distinct circuits held at once. The circuit key can be derived from the message
	/// through <see cref="CircuitBreakerOptions.CircuitKeySelector"/>, so an unbounded map is a
	/// memory-growth vector driven by message content.
	/// </summary>
	internal const int MaxBreakers = 1024;

	private readonly ConcurrentDictionary<string, Entry> _breakers = new(StringComparer.OrdinalIgnoreCase);

	/// <summary>
	/// Monotonic ticket source for recency. A counter rather than a clock: eviction only needs an
	/// ordering, and a counter cannot be perturbed by a clock adjustment.
	/// </summary>
	private long _accessTicket;
	private readonly CircuitBreakerOptions _defaultOptions;
	private readonly ILoggerFactory? _loggerFactory;

	/// <summary>
	/// Initializes a new instance of the <see cref="TransportCircuitBreakerRegistry"/> class.
	/// </summary>
	/// <param name="defaultOptions">Default options for circuit breakers when not explicitly configured.</param>
	/// <param name="loggerFactory">Optional logger factory for creating circuit breaker loggers.</param>
	public TransportCircuitBreakerRegistry(
		CircuitBreakerOptions? defaultOptions = null,
		ILoggerFactory? loggerFactory = null)
	{
		_defaultOptions = defaultOptions ?? new CircuitBreakerOptions();
		_loggerFactory = loggerFactory;
	}

	/// <inheritdoc />
	public int Count => _breakers.Count;

	/// <inheritdoc />
	public ICircuitBreakerPolicy GetOrCreate(string transportName)
	{
		return GetOrCreate(transportName, _defaultOptions);
	}

	/// <inheritdoc />
	public ICircuitBreakerPolicy GetOrCreate(string transportName, CircuitBreakerOptions options)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(transportName);
		ArgumentNullException.ThrowIfNull(options);

		if (_breakers.TryGetValue(transportName, out var existing))
		{
			existing.Touch(NextTicket());
			return existing.Policy;
		}

		// Bounded registry. This used to substitute a single shared overflow circuit once the cap was
		// reached, which defeated the sentence at the top of this class three ways: one transport's
		// failures opened another's circuit, TryGet returned null for a key GetOrCreate had just
		// accepted, and the shared circuit ran on whichever caller happened to create it first -- so a
		// caller's own thresholds were silently discarded. A circuit shared between transports is not a
		// circuit breaker for either of them.
		//
		// Make room instead, preferring a circuit that is not doing anything useful.
		//
		// The else-branch evicts ANYWAY rather than handing back an unstored circuit. An unstored
		// circuit would re-create the very defect this method is being fixed for: GetOrCreate would
		// report success while TryGet returned null for the same key, which is the history-constraint
		// break, just moved into the rare branch where nothing would notice it. Keeping the map
		// coherent on every path is worth more than keeping one protective circuit under pathological
		// pressure -- and that cost is bounded and self-healing, where the incoherence would not be.
		if (_breakers.Count >= MaxBreakers)
		{
			_ = TryEvictIdleCircuit() || TryEvictLeastRecentlyUsed();
		}

		var entry = _breakers.GetOrAdd(transportName, name => new Entry(CreatePolicy(name, options)));
		entry.Touch(NextTicket());
		return entry.Policy;
	}

	private long NextTicket() => Interlocked.Increment(ref _accessTicket);

	private CircuitBreakerPolicy CreatePolicy(string name, CircuitBreakerOptions options) =>
		new(options, name, _loggerFactory?.CreateLogger<CircuitBreakerPolicy>());

	/// <summary>
	/// Removes the least recently used circuit that is provably idle, and reports whether it did.
	/// </summary>
	/// <returns><see langword="true"/> when a circuit was evicted; otherwise <see langword="false"/>.</returns>
	/// <remarks>
	/// Idle means Closed AND carrying no consecutive failures. Plain least-recently-used is not safe
	/// here: under sustained load past the cap it can evict a circuit that is currently OPEN, which
	/// sends traffic straight back at the failing dependency the circuit exists to shield. An open or
	/// half-open circuit is precisely the one that must be kept.
	///
	/// A policy that does not expose <see cref="ICircuitBreakerDiagnostics"/> is treated as NOT
	/// evictable. Its failure count cannot be read, so "idle" cannot be established, and the fail-safe
	/// reading of an unknown is to keep the circuit rather than to discard it.
	/// </remarks>
	private bool TryEvictIdleCircuit()
	{
		string? victim = null;
		var oldest = long.MaxValue;

		foreach (var candidate in _breakers)
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

		return victim is not null && _breakers.TryRemove(victim, out _);
	}

	/// <summary>
	/// Removes the least recently used circuit whatever its state, and reports whether it did.
	/// </summary>
	/// <returns><see langword="true"/> when a circuit was evicted; otherwise <see langword="false"/>.</returns>
	/// <remarks>
	/// Reached only when no circuit is idle -- every one is open, recovering, or accumulating failures --
	/// so some protection has to be given up whatever is chosen. Giving up the least recently used one
	/// keeps the map coherent, which is the property that must not bend: a caller that is handed a
	/// circuit can always find it again.
	/// </remarks>
	private bool TryEvictLeastRecentlyUsed()
	{
		string? victim = null;
		var oldest = long.MaxValue;

		foreach (var candidate in _breakers)
		{
			var ticket = candidate.Value.LastAccess;
			if (ticket < oldest)
			{
				oldest = ticket;
				victim = candidate.Key;
			}
		}

		return victim is not null && _breakers.TryRemove(victim, out _);
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

	/// <inheritdoc />
	public ICircuitBreakerPolicy? TryGet(string transportName)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(transportName);

		return _breakers.TryGetValue(transportName, out var entry) ? entry.Policy : null;
	}

	/// <inheritdoc />
	public bool Remove(string transportName)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(transportName);

		return _breakers.TryRemove(transportName, out _);
	}

	/// <inheritdoc />
	public async Task ResetAllAsync(CancellationToken cancellationToken)
	{
		// Awaited in sequence rather than started in parallel: the postcondition is that EVERY circuit
		// is closed when this returns, and a Task.WhenAll over a snapshot would also have to decide what
		// to do about a circuit added while it ran. Sequential keeps the guarantee simple and the
		// registry is bounded, so the cost is bounded with it.
		foreach (var entry in _breakers.Values)
		{
			await entry.Policy.ResetAsync(cancellationToken).ConfigureAwait(false);
		}
	}

	/// <inheritdoc />
	public IReadOnlyDictionary<string, CircuitState> GetAllStates()
	{
		return _breakers.ToDictionary(
			kvp => kvp.Key,
			kvp => kvp.Value.Policy.State,
			StringComparer.OrdinalIgnoreCase);
	}

	/// <inheritdoc />
	public IEnumerable<string> GetTransportNames()
	{
		return _breakers.Keys.ToList();
	}
}
