// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Dispatch.Options.Resilience;

namespace Excalibur.Dispatch.CloudNative;

/// <summary>
/// Factory for creating circuit breaker instances.
/// </summary>
public interface ICircuitBreakerFactory
{
	/// <summary>
	/// Get or create a circuit breaker with the specified name.
	/// </summary>
	/// <remarks>
	/// Returns the <see cref="IResiliencePattern"/> abstraction, never a concrete breaker. This is
	/// load-bearing, not stylistic: naming a concrete class here previously forced every alternative
	/// implementation to <c>new</c>-hide the surface rather than override it, and <c>new</c> binds by
	/// STATIC type — so callers reached the named class's behaviour and the implementation they had
	/// registered never executed. Dispatching through the interface makes that inexpressible.
	/// Implementations live in the resilience packages; this assembly ships only the contract.
	/// </remarks>
	IResiliencePattern GetOrCreate(string name, CircuitBreakerOptions? options = null);

	/// <summary>
	/// Get metrics for all circuit breakers.
	/// </summary>
	Dictionary<string, CircuitBreakerMetrics> GetAllMetrics();

	/// <summary>
	/// Remove a circuit breaker.
	/// </summary>
	bool Remove(string name);
}
