// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Transport.Aws;

/// <summary>
/// Session status enumeration.
/// </summary>
public enum SessionStatus
{
	/// <summary>
	/// Session is idle and available for processing.
	/// </summary>
	Idle = 0,

	/// <summary>
	/// Session is currently being processed.
	/// </summary>
	Active = 1,

	/// <summary>
	/// Session is locked by a examples.AdvancedSample.Consumer.
	/// </summary>
	Locked = 2,

	/// <summary>
	/// Session is closed and no longer accepting messages.
	/// </summary>
	Closed = 3,

	/// <summary>
	/// Session has expired.
	/// </summary>
	Expired = 4,

	/// <summary>
	/// Session is temporarily suspended.
	/// </summary>
	Suspended = 5,

	/// <summary>
	/// Session is in the process of closing.
	/// </summary>
	Closing = 6,
}
