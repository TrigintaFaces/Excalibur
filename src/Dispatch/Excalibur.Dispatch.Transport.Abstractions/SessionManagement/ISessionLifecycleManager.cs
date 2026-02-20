// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Transport;

/// <summary>
/// Manages the lifecycle of sessions.
/// </summary>
public interface ISessionLifecycleManager
{
	/// <summary>
	/// Creates a new session.
	/// </summary>
	/// <param name="sessionId"> The session identifier. </param>
	/// <param name="options"> The session options. </param>
	/// <param name="cancellationToken"> The cancellation token. </param>
	/// <returns> The created session context. </returns>
	Task<SessionContext> CreateSessionAsync(
		string sessionId,
		SessionOptions? options,
		CancellationToken cancellationToken);

	/// <summary>
	/// Opens an existing session.
	/// </summary>
	/// <param name="sessionId"> The session identifier. </param>
	/// <param name="cancellationToken"> The cancellation token. </param>
	/// <returns> The session context if found, null otherwise. </returns>
	Task<SessionContext?> OpenSessionAsync(
		string sessionId,
		CancellationToken cancellationToken);

	/// <summary>
	/// Closes a session.
	/// </summary>
	/// <param name="sessionId"> The session identifier. </param>
	/// <param name="graceful"> Whether to wait for pending operations to complete. </param>
	/// <param name="cancellationToken"> The cancellation token. </param>
	/// <returns> True if the session was closed, false otherwise. </returns>
	Task<bool> CloseSessionAsync(
		string sessionId,
		bool graceful,
		CancellationToken cancellationToken);

	/// <summary>
	/// Renews a session to prevent expiration.
	/// </summary>
	/// <param name="sessionId"> The session identifier. </param>
	/// <param name="duration"> The duration to extend the session by. </param>
	/// <param name="cancellationToken"> The cancellation token. </param>
	/// <returns> The new expiration time if successful, null otherwise. </returns>
	Task<DateTimeOffset?> RenewSessionAsync(
		string sessionId,
		TimeSpan? duration,
		CancellationToken cancellationToken);

	/// <summary>
	/// Abandons a session, releasing all resources without saving state.
	/// </summary>
	/// <param name="sessionId"> The session identifier. </param>
	/// <param name="reason"> The reason for abandoning the session. </param>
	/// <param name="cancellationToken"> The cancellation token. </param>
	/// <returns> True if the session was abandoned, false otherwise. </returns>
	Task<bool> AbandonSessionAsync(
		string sessionId,
		string? reason,
		CancellationToken cancellationToken);

	/// <summary>
	/// Cleans up expired sessions.
	/// </summary>
	/// <param name="cancellationToken"> The cancellation token. </param>
	/// <returns> The number of sessions cleaned up. </returns>
	Task<int> CleanupExpiredSessionsAsync(
		CancellationToken cancellationToken);
}
