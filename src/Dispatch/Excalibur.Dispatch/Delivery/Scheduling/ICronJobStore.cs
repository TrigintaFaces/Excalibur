// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Delivery;

/// <summary>
/// Provides persistent storage for recurring cron jobs.
/// </summary>
public interface ICronJobStore
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
	/// Updates the next run time for a job.
	/// </summary>
	/// <param name="jobId"> The job ID. </param>
	/// <param name="nextRunUtc"> The next run time. </param>
	/// <param name="cancellationToken"> The cancellation token. </param>
	/// <returns> A task representing the asynchronous operation. </returns>
	Task UpdateNextRunTimeAsync(string jobId, DateTimeOffset? nextRunUtc, CancellationToken cancellationToken);

	/// <summary>
	/// Records the execution result of a job.
	/// </summary>
	/// <param name="jobId"> The job ID. </param>
	/// <param name="success"> Whether the execution was successful. </param>
	/// <param name="errorMessage"> Error message if the execution failed. </param>
	/// <param name="cancellationToken"> The cancellation token. </param>
	/// <returns> A task representing the asynchronous operation. </returns>
	Task RecordExecutionAsync(string jobId, bool success, CancellationToken cancellationToken, string? errorMessage = null);

	/// <summary>
	/// Enables or disables a job.
	/// </summary>
	/// <param name="jobId"> The job ID. </param>
	/// <param name="enabled"> Whether to enable or disable the job. </param>
	/// <param name="cancellationToken"> The cancellation token. </param>
	/// <returns> True if the job was updated; otherwise, false. </returns>
	Task<bool> SetJobEnabledAsync(string jobId, bool enabled, CancellationToken cancellationToken);

	/// <summary>
	/// Gets job execution history.
	/// </summary>
	/// <param name="jobId"> The job ID. </param>
	/// <param name="limit"> Maximum number of history entries to return. </param>
	/// <param name="cancellationToken"> The cancellation token. </param>
	/// <returns> A collection of execution history entries. </returns>
	Task<IEnumerable<JobExecutionHistory>> GetJobHistoryAsync(string jobId, CancellationToken cancellationToken, int limit = 100);
}
