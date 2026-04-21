// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Delivery;

/// <summary>
/// Query operations for cron job storage: due jobs, tag filtering, and execution history.
/// </summary>
/// <remarks>
/// <para>
/// This is a sub-interface of <see cref="ICronJobStore"/>.
/// For CRUD operations, see <see cref="ICronJobStoreCrud"/>.
/// For operational actions, see <see cref="ICronJobStoreOperations"/>.
/// </para>
/// </remarks>
public interface ICronJobStoreQuery
{
	/// <summary>
	/// Gets cron jobs that are due to run.
	/// </summary>
	/// <param name="cutoffTime"> The cutoff time for determining due jobs. </param>
	/// <param name="cancellationToken"> The cancellation token. </param>
	/// <returns> A collection of jobs that are due. </returns>
	Task<IEnumerable<RecurringCronJob>> GetDueJobsAsync(DateTimeOffset cutoffTime, CancellationToken cancellationToken);

	/// <summary>
	/// Gets cron jobs by tag.
	/// </summary>
	/// <param name="tag"> The tag to filter by. </param>
	/// <param name="cancellationToken"> The cancellation token. </param>
	/// <returns> A collection of jobs with the specified tag. </returns>
	Task<IEnumerable<RecurringCronJob>> GetJobsByTagAsync(string tag, CancellationToken cancellationToken);

	/// <summary>
	/// Gets job execution history.
	/// </summary>
	/// <param name="jobId"> The job ID. </param>
	/// <param name="cancellationToken"> The cancellation token. </param>
	/// <param name="limit"> Maximum number of history entries to return. </param>
	/// <returns> A collection of execution history entries. </returns>
	Task<IEnumerable<JobExecutionHistory>> GetJobHistoryAsync(string jobId, CancellationToken cancellationToken, int limit = 100);
}
