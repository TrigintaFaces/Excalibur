// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0


namespace Excalibur.Saga.Abstractions;

/// <summary>
/// The result of asking a store to retire a delivered timeout.
/// </summary>
/// <remarks>
/// <para>
/// Retirement can decline, so it reports rather than returning nothing. A call that removed no row and a
/// call that removed the row are different outcomes with different consequences for the caller, and a
/// void return made them the same observation.
/// </para>
/// <para>
/// There are deliberately two values and not three. "The row was not found" is not separable from
/// "another claim retired it": once the row is gone the store cannot tell those apart, so a third value
/// would invent a distinction no implementation can honour.
/// </para>
/// </remarks>
public enum SagaTimeoutRetirementOutcome
{
	/// <summary>
	/// The caller held the current claim and the timeout was retired. Delivery is complete.
	/// </summary>
	Retired = 0,

	/// <summary>
	/// The caller's claim is no longer the current one, so nothing was retired.
	/// </summary>
	/// <remarks>
	/// The lease expired and another processor re-claimed the timeout, which is the case the lease exists
	/// to handle. The caller must <strong>not</strong> report the timeout as delivered on its own behalf:
	/// the live claim holder owns the outcome now. This is an ordinary, expected result under a
	/// multi-instance deployment and is not an error.
	/// </remarks>
	Superseded = 1,
}
