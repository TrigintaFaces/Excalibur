// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Application.Requests;

/// <summary>
/// Defines the types of activities in the system.
/// </summary>
public enum ActivityType
{
	/// <summary>
	/// The activity type is unknown.
	/// </summary>
	Unknown = 0,

	/// <summary>
	/// Represents a command activity.
	/// </summary>
	Command = 1,

	/// <summary>
	/// Represents a query activity.
	/// </summary>
	Query = 2,

	/// <summary>
	/// Represents a notification activity.
	/// </summary>
	Notification = 3,

	/// <summary>
	/// Represents a job activity.
	/// </summary>
	Job = 4,
}
