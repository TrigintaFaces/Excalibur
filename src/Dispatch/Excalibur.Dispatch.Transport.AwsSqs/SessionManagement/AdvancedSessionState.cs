// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Transport.Aws;

/// <summary>
/// Represents advanced session state information.
/// </summary>
public sealed class AdvancedSessionState
{
	/// <summary>
	/// Gets or sets the session identifier.
	/// </summary>
	/// <value>
	/// The session identifier.
	/// </value>
	public string SessionId { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the current session status.
	/// </summary>
	/// <value>
	/// The current session status.
	/// </value>
	public SessionStatus Status { get; set; }

	/// <summary>
	/// Gets or sets the last activity timestamp.
	/// </summary>
	/// <value>
	/// The last activity timestamp.
	/// </value>
	public DateTime LastActivityUtc { get; set; }

	/// <summary>
	/// Gets or sets the session creation timestamp.
	/// </summary>
	/// <value>
	/// The session creation timestamp.
	/// </value>
	public DateTime CreatedUtc { get; set; }

	/// <summary>
	/// Gets or sets the session expiration timestamp.
	/// </summary>
	/// <value>
	/// The session expiration timestamp.
	/// </value>
	public DateTime? ExpiresUtc { get; set; }

	/// <summary>
	/// Gets or sets the message count processed in this session.
	/// </summary>
	/// <value>
	/// The message count processed in this session.
	/// </value>
	public long MessageCount { get; set; }

	/// <summary>
	/// Gets or sets the lock token if the session is locked.
	/// </summary>
	/// <value>
	/// The lock token if the session is locked.
	/// </value>
	public string? LockToken { get; set; }

	/// <summary>
	/// Gets or sets the owner of the session lock.
	/// </summary>
	/// <value>
	/// The owner of the session lock.
	/// </value>
	public string? LockOwner { get; set; }

	/// <summary>
	/// Gets custom metadata for the session.
	/// </summary>
	/// <value>
	/// Custom metadata for the session.
	/// </value>
	public Dictionary<string, object> Metadata { get; } = [];
}
