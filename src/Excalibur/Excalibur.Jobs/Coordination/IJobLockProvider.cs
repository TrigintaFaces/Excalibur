// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Jobs.Coordination;

/// <summary>
/// Provides distributed lock acquisition for job scheduling.
/// </summary>
public interface IJobLockProvider
{
	/// <summary>
	/// Attempts to acquire a distributed lock for the specified job.
	/// </summary>
	/// <param name="jobKey"> The unique key identifying the job. </param>
	/// <param name="lockDuration"> The duration for which to hold the lock. </param>
	/// <param name="cancellationToken"> A token to monitor for cancellation requests. </param>
	/// <returns> A task containing the acquired lock, or null if the lock could not be acquired. </returns>
	Task<IDistributedJobLock?> TryAcquireLockAsync(string jobKey, TimeSpan lockDuration, CancellationToken cancellationToken);
}
