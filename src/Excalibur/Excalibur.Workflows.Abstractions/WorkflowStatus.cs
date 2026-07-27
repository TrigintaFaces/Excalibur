// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Workflows;

/// <summary>
/// The lifecycle status of a durable workflow instance.
/// </summary>
public enum WorkflowStatus
{
	/// <summary>
	/// The workflow instance is executing or awaiting a timer or signal.
	/// </summary>
	Running,

	/// <summary>
	/// The workflow instance has completed successfully.
	/// </summary>
	Completed,

	/// <summary>
	/// The workflow instance has terminated with an unrecoverable failure.
	/// </summary>
	Faulted,
}
