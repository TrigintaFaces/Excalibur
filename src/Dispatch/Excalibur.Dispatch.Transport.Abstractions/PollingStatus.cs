// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Transport;

/// <summary>
/// Represents the status of a polling operation.
/// </summary>
public enum PollingStatus
{
	/// <summary>
	/// Polling is currently idle.
	/// </summary>
	Idle = 0,

	/// <summary>
	/// Polling is actively running.
	/// </summary>
	Running = 1,

	/// <summary>
	/// Polling has been paused.
	/// </summary>
	Paused = 2,

	/// <summary>
	/// Polling has been stopped.
	/// </summary>
	Stopped = 3,

	/// <summary>
	/// Polling is in an error state.
	/// </summary>
	Error = 4,
}
