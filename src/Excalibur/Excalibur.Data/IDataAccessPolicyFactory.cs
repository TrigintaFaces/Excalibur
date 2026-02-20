// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Polly;

namespace Excalibur.Data;

/// <summary>
/// Factory interface for creating data access policies using Polly.
/// </summary>
public interface IDataAccessPolicyFactory
{
	/// <summary>
	/// Gets a comprehensive policy that combines retry and circuit breaker policies.
	/// </summary>
	/// <returns> An asynchronous policy for comprehensive data access resilience. </returns>
	IAsyncPolicy GetComprehensivePolicy();

	/// <summary>
	/// Gets a retry policy for transient failures.
	/// </summary>
	/// <returns> An asynchronous retry policy for data access operations. </returns>
	IAsyncPolicy GetRetryPolicy();

	/// <summary>
	/// Creates a circuit breaker policy for handling repeated failures.
	/// </summary>
	/// <returns> An asynchronous circuit breaker policy for data access operations. </returns>
	IAsyncPolicy CreateCircuitBreakerPolicy();
}
