// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Transport.Aws;

/// <summary>
/// Information about a session.
/// </summary>
public sealed class SessionInfo
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
	/// Gets or sets the current consumer ID if locked.
	/// </summary>
	/// <value>
	/// The current consumer ID if locked.
	/// </value>
	public string? ConsumerId { get; set; }

	/// <summary>
	/// Gets or sets when the session was created.
	/// </summary>
	/// <value>
	/// When the session was created.
	/// </value>
	public DateTime CreatedAt { get; set; }

	/// <summary>
	/// Gets or sets when the session was last active.
	/// </summary>
	/// <value>
	/// When the session was last active.
	/// </value>
	public DateTime LastActivityAt { get; set; }

	/// <summary>
	/// Gets or sets the number of messages in the session.
	/// </summary>
	/// <value>
	/// The number of messages in the session.
	/// </value>
	public int MessageCount { get; set; }

	/// <summary>
	/// Gets or sets the number of pending messages.
	/// </summary>
	/// <value>
	/// The number of pending messages.
	/// </value>
	public int PendingMessageCount { get; set; }

	/// <summary>
	/// Gets or sets session metrics.
	/// </summary>
	/// <value>
	/// Session metrics.
	/// </value>
	public SessionMetrics Metrics { get; set; } = new();
}
