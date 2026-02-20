// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Saga.Models;

/// <summary>
/// Represents the status of a saga step.
/// </summary>
public enum StepStatus
{
	/// <summary>
	/// The step has not been executed.
	/// </summary>
	NotStarted = 0,

	/// <summary>
	/// The step is currently executing.
	/// </summary>
	Running = 1,

	/// <summary>
	/// The step completed successfully.
	/// </summary>
	Succeeded = 2,

	/// <summary>
	/// The step failed.
	/// </summary>
	Failed = 3,

	/// <summary>
	/// The step was skipped.
	/// </summary>
	Skipped = 4,

	/// <summary>
	/// The step timed out.
	/// </summary>
	TimedOut = 5,
}

