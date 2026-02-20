// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Transport;

/// <summary>
/// Contains session statistics.
/// </summary>
public sealed class SessionStatistics
{
	/// <summary>
	/// Gets or sets the total number of sessions.
	/// </summary>
	/// <value>The current <see cref="TotalSessions"/> value.</value>
	public int TotalSessions { get; set; }

	/// <summary>
	/// Gets or sets the number of active sessions.
	/// </summary>
	/// <value>The current <see cref="ActiveSessions"/> value.</value>
	public int ActiveSessions { get; set; }

	/// <summary>
	/// Gets or sets the number of idle sessions.
	/// </summary>
	/// <value>The current <see cref="IdleSessions"/> value.</value>
	public int IdleSessions { get; set; }

	/// <summary>
	/// Gets or sets the number of locked sessions.
	/// </summary>
	/// <value>The current <see cref="LockedSessions"/> value.</value>
	public int LockedSessions { get; set; }

	/// <summary>
	/// Gets or sets the total messages processed across all sessions.
	/// </summary>
	/// <value>The current <see cref="TotalMessagesProcessed"/> value.</value>
	public long TotalMessagesProcessed { get; set; }

	/// <summary>
	/// Gets or sets the average messages per session.
	/// </summary>
	/// <value>The current <see cref="AverageMessagesPerSession"/> value.</value>
	public double AverageMessagesPerSession { get; set; }

	/// <summary>
	/// Gets or sets the average session duration.
	/// </summary>
	/// <value>The current <see cref="AverageSessionDuration"/> value.</value>
	public TimeSpan AverageSessionDuration { get; set; }

	/// <summary>
	/// Gets or sets when the statistics were generated.
	/// </summary>
	/// <value>The current <see cref="GeneratedAt"/> value.</value>
	public DateTimeOffset GeneratedAt { get; set; }
}
