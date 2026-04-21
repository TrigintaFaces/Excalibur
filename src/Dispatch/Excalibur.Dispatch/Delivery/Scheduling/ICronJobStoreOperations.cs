// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Delivery;

/// <summary>
/// Operational actions for cron job storage: scheduling updates, execution recording,
/// and job enable/disable.
/// </summary>
/// <remarks>
/// <para>
/// This is a sub-interface of <see cref="ICronJobStore"/>.
/// For CRUD operations, see <see cref="ICronJobStoreCrud"/>.
/// For query operations, see <see cref="ICronJobStoreQuery"/>.
/// </para>
/// </remarks>
public interface ICronJobStoreOperations
{
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
	/// <param name="cancellationToken"> The cancellation token. </param>
	/// <param name="errorMessage"> Error message if the execution failed. </param>
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
}
