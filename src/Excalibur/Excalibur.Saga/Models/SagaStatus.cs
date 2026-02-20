// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Saga.Models;

/// <summary>
/// Represents the status of a saga.
/// </summary>
public enum SagaStatus
{
	/// <summary>
	/// The saga has been created but not started.
	/// </summary>
	Created = 0,

	/// <summary>
	/// The saga is currently running.
	/// </summary>
	Running = 1,

	/// <summary>
	/// The saga completed successfully.
	/// </summary>
	Completed = 2,

	/// <summary>
	/// The saga failed and compensation was triggered.
	/// </summary>
	Failed = 3,

	/// <summary>
	/// The saga is compensating for a failure.
	/// </summary>
	Compensating = 4,

	/// <summary>
	/// The saga compensation completed.
	/// </summary>
	Compensated = 5,

	/// <summary>
	/// The saga was cancelled.
	/// </summary>
	Cancelled = 6,

	/// <summary>
	/// The saga is suspended and can be resumed.
	/// </summary>
	Suspended = 7,

	/// <summary>
	/// The saga has expired and cannot be resumed.
	/// </summary>
	Expired = 8,
}

