// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Jobs.Workflows;

/// <summary>
/// Defines the possible statuses for workflow execution.
/// </summary>
public enum WorkflowStatus
{
	/// <summary>
	/// The workflow is currently running.
	/// </summary>
	Running = 0,

	/// <summary>
	/// The workflow completed successfully.
	/// </summary>
	Completed = 1,

	/// <summary>
	/// The workflow failed due to an error.
	/// </summary>
	Failed = 2,

	/// <summary>
	/// The workflow is suspended and waiting for external input.
	/// </summary>
	Suspended = 3,

	/// <summary>
	/// The workflow was cancelled.
	/// </summary>
	Cancelled = 4,
}
