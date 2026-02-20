// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Jobs.Coordination;

/// <summary>
/// Represents a distributed lock for coordinating job execution across multiple instances.
/// </summary>
public interface IDistributedJobLock : IAsyncDisposable
{
	/// <summary>
	/// Gets the unique key for the job that this lock protects.
	/// </summary>
	/// <value>
	/// The unique key for the job that this lock protects.
	/// </value>
	string JobKey { get; }

	/// <summary>
	/// Gets the unique identifier for the instance that holds this lock.
	/// </summary>
	/// <value>
	/// The unique identifier for the instance that holds this lock.
	/// </value>
	string InstanceId { get; }

	/// <summary>
	/// Gets the time when this lock was acquired.
	/// </summary>
	/// <value>
	/// The time when this lock was acquired.
	/// </value>
	DateTimeOffset AcquiredAt { get; }

	/// <summary>
	/// Gets the time when this lock will expire.
	/// </summary>
	/// <value>
	/// The time when this lock will expire.
	/// </value>
	DateTimeOffset ExpiresAt { get; }

	/// <summary>
	/// Gets a value indicating whether this lock is still valid and held.
	/// </summary>
	/// <value>
	/// A value indicating whether this lock is still valid and held.
	/// </value>
	bool IsValid { get; }

	/// <summary>
	/// Extends the duration of this lock.
	/// </summary>
	/// <param name="additionalDuration"> The additional time to extend the lock. </param>
	/// <param name="cancellationToken"> A token to monitor for cancellation requests. </param>
	/// <returns> A task indicating whether the lock extension was successful. </returns>
	Task<bool> ExtendAsync(TimeSpan additionalDuration, CancellationToken cancellationToken);

	/// <summary>
	/// Releases the lock before its expiration time.
	/// </summary>
	/// <param name="cancellationToken"> A token to monitor for cancellation requests. </param>
	/// <returns> A task representing the asynchronous operation. </returns>
	Task ReleaseAsync(CancellationToken cancellationToken);
}
