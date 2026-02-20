// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Jobs.Coordination;

/// <summary>
/// Defines the possible statuses for a job processing instance.
/// </summary>
public enum JobInstanceStatus
{
	/// <summary>
	/// The instance is active and available for job processing.
	/// </summary>
	Active = 0,

	/// <summary>
	/// The instance is temporarily draining and not accepting new jobs.
	/// </summary>
	Draining = 1,

	/// <summary>
	/// The instance is inactive and not processing jobs.
	/// </summary>
	Inactive = 2,

	/// <summary>
	/// The instance has failed or is in an error state.
	/// </summary>
	Failed = 3,
}
