// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Transport;

/// <summary>
/// Provides advanced session locking capabilities.
/// </summary>
public interface IAdvancedSessionLocking
{
	/// <summary>
	/// Acquires locks for multiple sessions atomically.
	/// </summary>
	/// <param name="sessionIds"> The session identifiers to lock. </param>
	/// <param name="lockDuration"> The duration to hold the locks. </param>
	/// <param name="cancellationToken"> The cancellation token. </param>
	/// <returns> A collection of lock tokens for the acquired locks. </returns>
	Task<IReadOnlyList<SessionLockToken>> AcquireMultipleLocksAsync(
		IEnumerable<string> sessionIds,
		TimeSpan lockDuration,
		CancellationToken cancellationToken);

	/// <summary>
	/// Waits for a lock to become available.
	/// </summary>
	/// <param name="sessionId"> The session identifier. </param>
	/// <param name="timeout"> The maximum time to wait. </param>
	/// <param name="cancellationToken"> The cancellation token. </param>
	/// <returns> A lock token if acquired within the timeout, null otherwise. </returns>
	Task<SessionLockToken?> WaitForLockAsync(
		string sessionId,
		TimeSpan timeout,
		CancellationToken cancellationToken);

	/// <summary>
	/// Converts a read lock to a write lock.
	/// </summary>
	/// <param name="readLockToken"> The read lock token. </param>
	/// <param name="cancellationToken"> The cancellation token. </param>
	/// <returns> A write lock token if successful, null otherwise. </returns>
	Task<SessionLockToken?> UpgradeToWriteLockAsync(
		SessionLockToken readLockToken,
		CancellationToken cancellationToken);

	/// <summary>
	/// Converts a write lock to a read lock.
	/// </summary>
	/// <param name="writeLockToken"> The write lock token. </param>
	/// <param name="cancellationToken"> The cancellation token. </param>
	/// <returns> A read lock token if successful, null otherwise. </returns>
	Task<SessionLockToken?> DowngradeToReadLockAsync(
		SessionLockToken writeLockToken,
		CancellationToken cancellationToken);

	/// <summary>
	/// Gets information about all current locks.
	/// </summary>
	/// <param name="cancellationToken"> The cancellation token. </param>
	/// <returns> A collection of lock information. </returns>
	Task<IReadOnlyList<LockInfo>> GetAllLocksAsync(
		CancellationToken cancellationToken);

	/// <summary>
	/// Forcefully breaks a lock (administrative operation).
	/// </summary>
	/// <param name="sessionId"> The session identifier. </param>
	/// <param name="reason"> The reason for breaking the lock. </param>
	/// <param name="cancellationToken"> The cancellation token. </param>
	/// <returns> True if the lock was broken, false otherwise. </returns>
	Task<bool> BreakLockAsync(
		string sessionId,
		string reason,
		CancellationToken cancellationToken);
}
