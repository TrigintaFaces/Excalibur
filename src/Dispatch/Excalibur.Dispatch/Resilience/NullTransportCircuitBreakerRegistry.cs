// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Options.Resilience;

namespace Excalibur.Dispatch.Resilience;

/// <summary>
/// A no-op implementation of <see cref="ITransportCircuitBreakerRegistry"/> that returns pass-through circuit breakers.
/// </summary>
/// <remarks>
/// This implementation follows the Null Object pattern to provide a safe default when
/// circuit breaker functionality is not configured or not needed. All circuit breakers
/// returned are in the closed state and pass all operations through without protection.
/// </remarks>
public sealed class NullTransportCircuitBreakerRegistry : ITransportCircuitBreakerRegistry, ITransportCircuitBreakerDiagnostics
{
	private NullTransportCircuitBreakerRegistry()
	{
	}

	/// <summary>
	/// Gets the singleton instance of the null circuit breaker registry.
	/// </summary>
	public static NullTransportCircuitBreakerRegistry Instance { get; } = new();

	/// <inheritdoc />
	public int Count => 0;

	/// <inheritdoc />
	public ICircuitBreakerPolicy GetOrCreate(string transportName) =>
		NullCircuitBreakerPolicy.Instance;

	/// <inheritdoc />
	public ICircuitBreakerPolicy GetOrCreate(string transportName, CircuitBreakerOptions options) =>
		NullCircuitBreakerPolicy.Instance;

	/// <inheritdoc />
	public ICircuitBreakerPolicy? TryGet(string transportName) => null;

	/// <inheritdoc />
	public bool Remove(string transportName) => false;

	/// <inheritdoc />
	public void ResetAll()
	{
		// No-op
	}

	/// <inheritdoc />
	public IReadOnlyDictionary<string, CircuitState> GetAllStates() =>
		new Dictionary<string, CircuitState>();

	/// <inheritdoc />
	public IEnumerable<string> GetTransportNames() =>
		Array.Empty<string>();
}
