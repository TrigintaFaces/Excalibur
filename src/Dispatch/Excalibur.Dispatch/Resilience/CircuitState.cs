// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Resilience;

/// <summary>
/// Represents the current state of a circuit breaker.
/// </summary>
public enum CircuitState
{
	/// <summary>
	/// The circuit is closed and allowing operations through.
	/// This is the normal operating state.
	/// </summary>
	Closed = 0,

	/// <summary>
	/// The circuit is open and rejecting all operations.
	/// This state is entered when the failure threshold is exceeded.
	/// </summary>
	Open = 1,

	/// <summary>
	/// The circuit is half-open, testing if the underlying service has recovered.
	/// A limited number of operations are allowed through.
	/// </summary>
	HalfOpen = 2,
}
