// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Transport.Aws;

/// <summary>
/// Context for session processing.
/// </summary>
public sealed class SessionContext
{
	/// <summary>
	/// Gets or sets the session identifier.
	/// </summary>
	/// <value>
	/// The session identifier.
	/// </value>
	public required string SessionId { get; set; }

	/// <summary>
	/// Gets or sets the session lock.
	/// </summary>
	/// <value>
	/// The session lock.
	/// </value>
	public SessionLock? Lock { get; set; }

	/// <summary>
	/// Gets or sets the session state.
	/// </summary>
	/// <value>
	/// The session state.
	/// </value>
	public AwsSessionState? State { get; set; }

	/// <summary>
	/// Gets or sets the consumer identifier.
	/// </summary>
	/// <value>
	/// The consumer identifier.
	/// </value>
	public string? ConsumerId { get; set; }

	/// <summary>
	/// Gets or sets the processing start time.
	/// </summary>
	/// <value>
	/// The processing start time.
	/// </value>
	public DateTime ProcessingStartedAt { get; set; }

	/// <summary>
	/// Gets custom context data.
	/// </summary>
	/// <value>
	/// Custom context data.
	/// </value>
	public Dictionary<string, object> Data { get; } = [];
}
