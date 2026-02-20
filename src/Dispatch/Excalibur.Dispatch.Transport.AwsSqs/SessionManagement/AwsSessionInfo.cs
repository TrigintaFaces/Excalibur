// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Transport.Aws;

/// <summary>
/// Represents the state of an AWS session.
/// </summary>
public sealed class AwsSessionInfo
{
	/// <summary>
	/// Gets or sets the session identifier.
	/// </summary>
	/// <value>
	/// The session identifier.
	/// </value>
	public required string SessionId { get; set; }

	/// <summary>
	/// Gets or sets the session status.
	/// </summary>
	/// <value>
	/// The session status.
	/// </value>
	public SessionStatus Status { get; set; }

	/// <summary>
	/// Gets or sets when the session was created.
	/// </summary>
	/// <value>
	/// When the session was created.
	/// </value>
	public DateTime CreatedAt { get; set; }

	/// <summary>
	/// Gets or sets when the session was last accessed.
	/// </summary>
	/// <value>
	/// When the session was last accessed.
	/// </value>
	public DateTime LastAccessedAt { get; set; }

	/// <summary>
	/// Gets or sets the number of messages processed in this session.
	/// </summary>
	/// <value>
	/// The number of messages processed in this session.
	/// </value>
	public int MessageCount { get; set; }

	/// <summary>
	/// Gets custom session data.
	/// </summary>
	/// <value>
	/// Custom session data.
	/// </value>
	public Dictionary<string, object> Data { get; } = [];

	/// <summary>
	/// Gets or sets the current lock information.
	/// </summary>
	/// <value>
	/// The current lock information.
	/// </value>
	public SessionLockInfo? CurrentLock { get; set; }

	/// <summary>
	/// Gets session metadata.
	/// </summary>
	/// <value>
	/// Session metadata.
	/// </value>
	public Dictionary<string, string> Metadata { get; } = [];
}
