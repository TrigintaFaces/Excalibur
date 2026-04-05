// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Delivery;

/// <summary>
/// CRUD operations for persistent cron job storage.
/// </summary>
/// <remarks>
/// <para>
/// This is a sub-interface of <see cref="ICronJobStore"/>.
/// For query operations, see <see cref="ICronJobStoreQuery"/>.
/// For operational actions, see <see cref="ICronJobStoreOperations"/>.
/// </para>
/// </remarks>
public interface ICronJobStoreCrud
{
	/// <summary>
	/// Adds a new cron job to the store.
	/// </summary>
	/// <param name="job"> The job to add. </param>
	/// <param name="cancellationToken"> The cancellation token. </param>
	/// <returns> A task representing the asynchronous operation. </returns>
	Task AddJobAsync(RecurringCronJob job, CancellationToken cancellationToken);

	/// <summary>
	/// Updates an existing cron job.
	/// </summary>
	/// <param name="job"> The job to update. </param>
	/// <param name="cancellationToken"> The cancellation token. </param>
	/// <returns> A task representing the asynchronous operation. </returns>
	Task UpdateJobAsync(RecurringCronJob job, CancellationToken cancellationToken);

	/// <summary>
	/// Removes a cron job from the store.
	/// </summary>
	/// <param name="jobId"> The ID of the job to remove. </param>
	/// <param name="cancellationToken"> The cancellation token. </param>
	/// <returns> True if the job was removed; otherwise, false. </returns>
	Task<bool> RemoveJobAsync(string jobId, CancellationToken cancellationToken);

	/// <summary>
	/// Gets a cron job by its ID.
	/// </summary>
	/// <param name="jobId"> The job ID. </param>
	/// <param name="cancellationToken"> The cancellation token. </param>
	/// <returns> The job if found; otherwise, null. </returns>
	Task<RecurringCronJob?> GetJobAsync(string jobId, CancellationToken cancellationToken);

	/// <summary>
	/// Gets all active cron jobs.
	/// </summary>
	/// <param name="cancellationToken"> The cancellation token. </param>
	/// <returns> A collection of active jobs. </returns>
	Task<IEnumerable<RecurringCronJob>> GetActiveJobsAsync(CancellationToken cancellationToken);
}
