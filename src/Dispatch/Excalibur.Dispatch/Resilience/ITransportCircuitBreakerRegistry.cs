// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


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
/// For diagnostic and admin operations (Count, Remove, ResetAll, GetAllStates, GetTransportNames),
/// use <c>GetService(typeof(ITransportCircuitBreakerDiagnostics))</c>.
/// </para>
/// </remarks>
public interface ITransportCircuitBreakerRegistry
{
	/// <summary>
	/// Gets or creates a circuit breaker for the specified transport using default options.
	/// </summary>
	/// <param name="transportName">The name of the transport (e.g., "RabbitMQ", "AzureServiceBus").</param>
	/// <returns>The circuit breaker policy for the transport.</returns>
	ICircuitBreakerPolicy GetOrCreate(string transportName);

	/// <summary>
	/// Gets or creates a circuit breaker for the specified transport with custom options.
	/// </summary>
	/// <param name="transportName">The name of the transport.</param>
	/// <param name="options">The circuit breaker configuration options.</param>
	/// <returns>The circuit breaker policy for the transport.</returns>
	ICircuitBreakerPolicy GetOrCreate(string transportName, CircuitBreakerOptions options);

	/// <summary>
	/// Tries to get an existing circuit breaker for the specified transport.
	/// </summary>
	/// <param name="transportName">The name of the transport.</param>
	/// <returns>The circuit breaker policy if found; otherwise, null.</returns>
	ICircuitBreakerPolicy? TryGet(string transportName);
}

/// <summary>
/// Provides diagnostic and administrative operations for the transport circuit breaker registry.
/// Access via <c>GetService(typeof(ITransportCircuitBreakerDiagnostics))</c> on the registry instance.
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
	bool Remove(string transportName);

	/// <summary>
	/// Resets all circuit breakers to the closed state.
	/// </summary>
	void ResetAll();

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
