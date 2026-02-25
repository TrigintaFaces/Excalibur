// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Transport;

/// <summary>
/// Represents the state of a session.
/// </summary>
public enum DispatchSessionState
{
	/// <summary>
	/// The session is active and processing messages.
	/// </summary>
	Active = 0,

	/// <summary>
	/// The session is idle, waiting for messages.
	/// </summary>
	Idle = 1,

	/// <summary>
	/// The session is locked by another processor.
	/// </summary>
	Locked = 2,

	/// <summary>
	/// The session has expired.
	/// </summary>
	Expired = 3,

	/// <summary>
	/// The session is being closed.
	/// </summary>
	Closing = 4,

	/// <summary>
	/// The session is closed.
	/// </summary>
	Closed = 5,
}
