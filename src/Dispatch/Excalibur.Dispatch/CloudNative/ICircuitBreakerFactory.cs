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
	CircuitBreakerPattern GetOrCreate(string name, CircuitBreakerOptions? options = null);

	/// <summary>
	/// Get metrics for all circuit breakers.
	/// </summary>
	Dictionary<string, CircuitBreakerMetrics> GetAllMetrics();

	/// <summary>
	/// Remove a circuit breaker.
	/// </summary>
	bool Remove(string name);
}
