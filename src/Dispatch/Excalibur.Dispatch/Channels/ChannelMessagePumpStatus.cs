// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Channels;

/// <summary>
/// Represents the status of a channel message pump.
/// </summary>
public enum ChannelMessagePumpStatus
{
	/// <summary>
	/// The pump has not been started.
	/// </summary>
	NotStarted = 0,

	/// <summary>
	/// The pump is starting up.
	/// </summary>
	Starting = 1,

	/// <summary>
	/// The pump is running and producing/consuming messages.
	/// </summary>
	Running = 2,

	/// <summary>
	/// The pump is stopping gracefully.
	/// </summary>
	Stopping = 3,

	/// <summary>
	/// The pump has stopped.
	/// </summary>
	Stopped = 4,

	/// <summary>
	/// The pump has encountered an error.
	/// </summary>
	Faulted = 5,
}
