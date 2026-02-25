// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Data.ElasticSearch.Resilience;

/// <summary>
/// Represents the possible states of a circuit breaker.
/// </summary>
public enum CircuitBreakerState
{
	/// <summary>
	/// The circuit is closed, allowing all requests to pass through.
	/// </summary>
	Closed = 0,

	/// <summary>
	/// The circuit is open, blocking all requests and failing fast.
	/// </summary>
	Open = 1,

	/// <summary>
	/// The circuit is half-open, allowing limited test requests to determine if the service has recovered.
	/// </summary>
	HalfOpen = 2,
}
