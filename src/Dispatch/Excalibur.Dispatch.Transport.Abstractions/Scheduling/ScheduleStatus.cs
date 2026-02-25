// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Transport;

/// <summary>
/// Represents the status of a scheduled message.
/// </summary>
public enum ScheduleStatus
{
	/// <summary>
	/// The message is scheduled and waiting for delivery.
	/// </summary>
	Scheduled = 0,

	/// <summary>
	/// The message is currently being delivered.
	/// </summary>
	InProgress = 1,

	/// <summary>
	/// The message was delivered successfully.
	/// </summary>
	Completed = 2,

	/// <summary>
	/// The message delivery failed.
	/// </summary>
	Failed = 3,

	/// <summary>
	/// The scheduled message was cancelled.
	/// </summary>
	Cancelled = 4,
}
