// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Delivery;

/// <summary>
/// Defines how to handle executions that were missed while the scheduler was offline.
/// </summary>
public enum MissedExecutionBehavior
{
	/// <summary>
	/// Skip all missed executions and schedule the next future occurrence.
	/// </summary>
	SkipMissed = 0,

	/// <summary>
	/// Execute the most recent missed execution immediately, then resume normal schedule.
	/// </summary>
	ExecuteLatestMissed = 1,

	/// <summary>
	/// Execute all missed executions up to the configured maximum.
	/// </summary>
	ExecuteAllMissed = 2,

	/// <summary>
	/// Treat missed executions as errors and disable the schedule.
	/// </summary>
	DisableSchedule = 3,
}
