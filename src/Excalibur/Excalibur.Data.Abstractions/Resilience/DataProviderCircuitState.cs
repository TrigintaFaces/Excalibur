// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Data.Abstractions.Resilience;

/// <summary>
/// Represents the state of a data provider circuit breaker.
/// </summary>
public enum DataProviderCircuitState
{
	/// <summary>
	/// The circuit is closed. Operations flow normally.
	/// </summary>
	Closed,

	/// <summary>
	/// The circuit is open. Operations are rejected immediately.
	/// </summary>
	Open,

	/// <summary>
	/// The circuit is half-open. A limited number of trial operations are allowed
	/// to determine if the underlying service has recovered.
	/// </summary>
	HalfOpen
}
