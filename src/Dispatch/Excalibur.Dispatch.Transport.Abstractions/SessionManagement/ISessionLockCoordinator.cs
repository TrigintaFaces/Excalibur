// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Transport;

/// <summary>
/// Coordinates distributed locks for session management.
/// </summary>
public interface ISessionLockCoordinator
{
	/// <summary>
	/// Acquires a lock for the specified session.
	/// </summary>
	/// <param name="sessionId"> The session identifier. </param>
	/// <param name="lockDuration"> The duration to hold the lock. </param>
	/// <param name="cancellationToken"> The cancellation token. </param>
	/// <returns> A lock token that can be used to release or extend the lock. </returns>
	Task<SessionLockToken> AcquireLockAsync(
		string sessionId,
		TimeSpan lockDuration,
		CancellationToken cancellationToken);

	/// <summary>
	/// Tries to acquire a lock for the specified session without waiting.
	/// </summary>
	/// <param name="sessionId"> The session identifier. </param>
	/// <param name="lockDuration"> The duration to hold the lock. </param>
	/// <param name="cancellationToken"> The cancellation token. </param>
	/// <returns> A lock token if successful, null otherwise. </returns>
	Task<SessionLockToken?> TryAcquireLockAsync(
		string sessionId,
		TimeSpan lockDuration,
		CancellationToken cancellationToken);

	/// <summary>
	/// Extends an existing lock.
	/// </summary>
	/// <param name="lockToken"> The lock token to extend. </param>
	/// <param name="extension"> The duration to extend the lock by. </param>
	/// <param name="cancellationToken"> The cancellation token. </param>
	/// <returns> True if the lock was extended, false otherwise. </returns>
	Task<bool> ExtendLockAsync(
		SessionLockToken lockToken,
		TimeSpan extension,
		CancellationToken cancellationToken);

	/// <summary>
	/// Releases a lock.
	/// </summary>
	/// <param name="lockToken"> The lock token to release. </param>
	/// <param name="cancellationToken"> The cancellation token. </param>
	/// <returns> True if the lock was released, false otherwise. </returns>
	Task<bool> ReleaseLockAsync(
		SessionLockToken lockToken,
		CancellationToken cancellationToken);

	/// <summary>
	/// Checks if a session is currently locked.
	/// </summary>
	/// <param name="sessionId"> The session identifier. </param>
	/// <param name="cancellationToken"> The cancellation token. </param>
	/// <returns> True if the session is locked, false otherwise. </returns>
	Task<bool> IsLockedAsync(
		string sessionId,
		CancellationToken cancellationToken);
}
