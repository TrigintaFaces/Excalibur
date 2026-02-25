// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Transport.Aws;

/// <summary>
/// AWS session state enumeration matching what tests expect.
/// </summary>
/// <remarks> This is an alias for SessionStatus to maintain compatibility with existing test code. </remarks>
public enum AwsSessionState
{
	/// <summary>
	/// The session is idle and not processing messages.
	/// </summary>
	Idle = SessionStatus.Idle,

	/// <summary>
	/// The session is active and can process messages.
	/// </summary>
	Active = SessionStatus.Active,

	/// <summary>
	/// The session is locked for exclusive processing.
	/// </summary>
	Locked = SessionStatus.Locked,

	/// <summary>
	/// The session is temporarily suspended.
	/// </summary>
	Suspended = SessionStatus.Suspended,

	/// <summary>
	/// The session is being closed.
	/// </summary>
	Closing = SessionStatus.Closing,

	/// <summary>
	/// The session has been closed.
	/// </summary>
	Closed = SessionStatus.Closed,

	/// <summary>
	/// The session has expired.
	/// </summary>
	Expired = SessionStatus.Expired,
}
