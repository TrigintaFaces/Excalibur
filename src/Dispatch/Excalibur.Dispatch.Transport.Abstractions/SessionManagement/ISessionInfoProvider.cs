// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Transport;

/// <summary>
/// Provides information about sessions.
/// </summary>
public interface ISessionInfoProvider
{
	/// <summary>
	/// Gets information about a specific session.
	/// </summary>
	/// <param name="sessionId"> The session identifier. </param>
	/// <param name="cancellationToken"> The cancellation token. </param>
	/// <returns> The session information if found, null otherwise. </returns>
	Task<SessionInfo?> GetSessionInfoAsync(
		string sessionId,
		CancellationToken cancellationToken);

	/// <summary>
	/// Gets information about multiple sessions.
	/// </summary>
	/// <param name="sessionIds"> The session identifiers. </param>
	/// <param name="cancellationToken"> The cancellation token. </param>
	/// <returns> A dictionary of session information keyed by session ID. </returns>
	Task<IReadOnlyDictionary<string, SessionInfo>> GetSessionInfosAsync(
		IEnumerable<string> sessionIds,
		CancellationToken cancellationToken);

	/// <summary>
	/// Lists all active sessions.
	/// </summary>
	/// <param name="maxSessions"> The maximum number of sessions to return. </param>
	/// <param name="cancellationToken"> The cancellation token. </param>
	/// <returns> A list of active session information. </returns>
	Task<IReadOnlyList<SessionInfo>> ListActiveSessionsAsync(
		int maxSessions,
		CancellationToken cancellationToken);

	/// <summary>
	/// Gets session statistics.
	/// </summary>
	/// <param name="cancellationToken"> The cancellation token. </param>
	/// <returns> The session statistics. </returns>
	Task<SessionStatistics> GetStatisticsAsync(
		CancellationToken cancellationToken);
}
