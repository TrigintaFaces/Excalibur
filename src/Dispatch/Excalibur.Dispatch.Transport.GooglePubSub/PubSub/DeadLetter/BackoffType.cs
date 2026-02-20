// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Transport.Google;

/// <summary>
/// Types of backoff strategies.
/// </summary>
public enum BackoffType
{
	/// <summary>
	/// Constant delay between retries.
	/// </summary>
	Constant = 0,

	/// <summary>
	/// Linear increase in delay.
	/// </summary>
	Linear = 1,

	/// <summary>
	/// Exponential increase in delay.
	/// </summary>
	Exponential = 2,

	/// <summary>
	/// Decorrelated jitter backoff.
	/// </summary>
	DecorrelatedJitter = 3,
}
