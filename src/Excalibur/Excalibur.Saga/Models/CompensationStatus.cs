// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Saga.Models;

/// <summary>
/// Represents the compensation status of a step.
/// </summary>
public enum CompensationStatus
{
	/// <summary>
	/// No compensation needed or attempted.
	/// </summary>
	NotRequired = 0,

	/// <summary>
	/// Compensation is pending.
	/// </summary>
	Pending = 1,

	/// <summary>
	/// Compensation is in progress.
	/// </summary>
	Running = 2,

	/// <summary>
	/// Compensation succeeded.
	/// </summary>
	Succeeded = 3,

	/// <summary>
	/// Compensation failed.
	/// </summary>
	Failed = 4,

	/// <summary>
	/// Step cannot be compensated.
	/// </summary>
	NotCompensable = 5,
}

