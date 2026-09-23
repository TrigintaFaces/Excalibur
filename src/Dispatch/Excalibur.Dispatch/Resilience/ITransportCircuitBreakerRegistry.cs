// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0


using Excalibur.Dispatch.Options.Resilience;

namespace Excalibur.Dispatch.Resilience;

/// <summary>
/// Registry for managing circuit breaker instances per transport.
/// </summary>
/// <remarks>
/// <para>
/// This registry ensures each transport has its own isolated circuit breaker,
/// preventing failures in one transport from affecting others.
/// </para>
/// <para>
/// Diagnostic and admin operations (Count, Remove, ResetAll, GetAllStates, GetTransportNames) are
/// kept off this interface so an implementation is not obliged to carry them. Test the registry
/// instance for <see cref="ITransportCircuitBreakerDiagnostics"/> to reach them.
/// </para>
/// <para>
/// <b>Transport names are matched without regard to case, using ordinal comparison.</b> "Kafka",
/// "kafka" and "KAFKA" name one transport and share one circuit. An implementation that
/// distinguishes them gives the same transport two breakers, so each observes only the calls that
/// used its spelling &#8212; and a transport failing every call can stay below its configured
/// threshold on both and never open.
/// </para>
/// </remarks>
public interface ITransportCircuitBreakerRegistry
{
	/// <summary>
	/// Gets or creates a circuit breaker for the specified transport using default options.
	/// </summary>
	/// <param name="transportName">The name of the transport (e.g., "RabbitMQ", "AzureServiceBus").</param>
	/// <returns>The circuit breaker policy for the transport.</returns>
	/// <exception cref="ArgumentNullException"><paramref name="transportName"/> is <see langword="null"/>.</exception>
	/// <exception cref="ArgumentException"><paramref name="transportName"/> is empty or consists only of white-space characters.</exception>
	ICircuitBreakerPolicy GetOrCreate(string transportName);

	/// <summary>
	/// Gets or creates a circuit breaker for the specified transport with custom options.
	/// </summary>
	/// <param name="transportName">The name of the transport.</param>
	/// <param name="options">The circuit breaker configuration options.</param>
	/// <returns>The circuit breaker policy for the transport.</returns>
	/// <exception cref="ArgumentNullException"><paramref name="transportName"/> or <paramref name="options"/> is <see langword="null"/>.</exception>
	/// <exception cref="ArgumentException"><paramref name="transportName"/> is empty or consists only of white-space characters.</exception>
	ICircuitBreakerPolicy GetOrCreate(string transportName, CircuitBreakerOptions options);

	/// <summary>
	/// Tries to get an existing circuit breaker for the specified transport.
	/// </summary>
	/// <param name="transportName">The name of the transport.</param>
	/// <returns>The circuit breaker policy if found; otherwise, null.</returns>
	/// <exception cref="ArgumentNullException"><paramref name="transportName"/> is <see langword="null"/>.</exception>
	/// <exception cref="ArgumentException"><paramref name="transportName"/> is empty or consists only of white-space characters.</exception>
	ICircuitBreakerPolicy? TryGet(string transportName);
}

/// <summary>
/// Provides diagnostic and administrative operations for the transport circuit breaker registry.
/// Test an <see cref="ITransportCircuitBreakerRegistry"/> instance for this interface to reach it.
/// </summary>
public interface ITransportCircuitBreakerDiagnostics
{
	/// <summary>
	/// Gets the number of registered circuit breakers.
	/// </summary>
	int Count { get; }

	/// <summary>
	/// Removes a circuit breaker for the specified transport.
	/// </summary>
	/// <param name="transportName">The name of the transport.</param>
	/// <returns>True if the circuit breaker was removed; otherwise, false.</returns>
	/// <exception cref="ArgumentNullException"><paramref name="transportName"/> is <see langword="null"/>.</exception>
	/// <exception cref="ArgumentException"><paramref name="transportName"/> is empty or consists only of white-space characters.</exception>
	bool Remove(string transportName);

	/// <summary>
	/// Returns every registered circuit breaker to the closed state.
	/// </summary>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>A task that completes once every circuit is closed.</returns>
	/// <remarks>
	/// When the returned task completes, every circuit this registry holds reports
	/// <see cref="CircuitState.Closed" />. The individual resets are awaited rather than started, so a
	/// caller that needs the whole registry back in service can wait for exactly that.
	/// </remarks>
	Task ResetAllAsync(CancellationToken cancellationToken);

	/// <summary>
	/// Gets the current states of all registered circuit breakers.
	/// </summary>
	/// <returns>A dictionary mapping transport names to their circuit states.</returns>
	IReadOnlyDictionary<string, CircuitState> GetAllStates();

	/// <summary>
	/// Gets the names of all registered transports.
	/// </summary>
	/// <returns>A collection of transport names.</returns>
	IEnumerable<string> GetTransportNames();
}
