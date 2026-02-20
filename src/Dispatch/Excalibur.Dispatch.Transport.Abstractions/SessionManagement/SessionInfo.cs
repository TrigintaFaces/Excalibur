// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Transport;

/// <summary>
/// Contains information about a session.
/// </summary>
public sealed class SessionInfo
{
	/// <summary>
	/// Gets or sets the session identifier.
	/// </summary>
	/// <value>The current <see cref="SessionId"/> value.</value>
	public string SessionId { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the session state.
	/// </summary>
	/// <value>The current <see cref="State"/> value.</value>
	public DispatchSessionState State { get; set; }

	/// <summary>
	/// Gets or sets when the session was created.
	/// </summary>
	/// <value>The current <see cref="CreatedAt"/> value.</value>
	public DateTimeOffset CreatedAt { get; set; }

	/// <summary>
	/// Gets or sets when the session was last accessed.
	/// </summary>
	/// <value>The current <see cref="LastAccessedAt"/> value.</value>
	public DateTimeOffset LastAccessedAt { get; set; }

	/// <summary>
	/// Gets or sets when the session expires.
	/// </summary>
	/// <value>The current <see cref="ExpiresAt"/> value.</value>
	public DateTimeOffset? ExpiresAt { get; set; }

	/// <summary>
	/// Gets or sets the number of messages processed in this session.
	/// </summary>
	/// <value>The current <see cref="MessageCount"/> value.</value>
	public long MessageCount { get; set; }

	/// <summary>
	/// Gets or sets the number of pending messages awaiting processing.
	/// </summary>
	/// <value>The current <see cref="PendingMessageCount"/> value.</value>
	public long PendingMessageCount { get; set; }

	/// <summary>
	/// Gets the session metadata.
	/// </summary>
	/// <value>The current <see cref="Metadata"/> value.</value>
	public Dictionary<string, string> Metadata { get; init; } = [];

	/// <summary>
	/// Gets or sets the current lock token if the session is locked.
	/// </summary>
	/// <value>The current <see cref="LockToken"/> value.</value>
	public string? LockToken { get; set; }

	/// <summary>
	/// Gets or sets the owner of the session.
	/// </summary>
	/// <value>The current <see cref="OwnerId"/> value.</value>
	public string? OwnerId { get; set; }
}
