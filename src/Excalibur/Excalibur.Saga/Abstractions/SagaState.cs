// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Saga.Abstractions;

/// <summary>
/// Represents the state of a saga.
/// </summary>
public enum SagaState
{
	/// <summary>
	/// Saga has been created but not started.
	/// </summary>
	Created = 0,

	/// <summary>
	/// Saga is currently executing.
	/// </summary>
	Running = 1,

	/// <summary>
	/// Saga is compensating due to a failure.
	/// </summary>
	Compensating = 2,

	/// <summary>
	/// Saga completed successfully.
	/// </summary>
	Completed = 3,

	/// <summary>
	/// Saga failed and compensation was successful.
	/// </summary>
	CompensatedSuccessfully = 4,

	/// <summary>
	/// Saga failed and compensation also failed.
	/// </summary>
	CompensationFailed = 5,

	/// <summary>
	/// Saga was cancelled.
	/// </summary>
	Cancelled = 6,
}
