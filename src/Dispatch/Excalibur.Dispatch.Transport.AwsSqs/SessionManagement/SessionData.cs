// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Transport.Aws;

/// <summary>
/// Session data model for Redis storage.
/// </summary>
public sealed class SessionData
{
	/// <summary>
	/// Gets or sets the session ID.
	/// </summary>
	/// <value>
	/// The session ID.
	/// </value>
	public string Id { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the session state.
	/// </summary>
	/// <value>
	/// The session state.
	/// </value>
	public AwsSessionState State { get; set; }

	/// <summary>
	/// Gets or sets when the session was created.
	/// </summary>
	/// <value>
	/// When the session was created.
	/// </value>
	public DateTimeOffset CreatedAt { get; set; }

	/// <summary>
	/// Gets or sets when the session was last accessed.
	/// </summary>
	/// <value>
	/// When the session was last accessed.
	/// </value>
	public DateTimeOffset LastAccessedAt { get; set; }

	/// <summary>
	/// Gets or sets when the session expires.
	/// </summary>
	/// <value>
	/// When the session expires.
	/// </value>
	public DateTimeOffset? ExpiresAt { get; set; }

	/// <summary>
	/// Gets session metadata.
	/// </summary>
	/// <value>
	/// Session metadata.
	/// </value>
	public Dictionary<string, object> Metadata { get; } = [];

	/// <summary>
	/// Gets or sets the number of messages processed.
	/// </summary>
	/// <value>
	/// The number of messages processed.
	/// </value>
	public long MessageCount { get; set; }
}
