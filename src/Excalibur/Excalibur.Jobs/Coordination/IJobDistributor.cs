// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Jobs.Coordination;

/// <summary>
/// Distributes jobs to available instances and tracks completion.
/// </summary>
public interface IJobDistributor
{
	/// <summary>
	/// Distributes a job to an available instance for processing.
	/// </summary>
	/// <param name="jobKey"> The unique key identifying the job. </param>
	/// <param name="jobData"> The job data to be processed. </param>
	/// <param name="cancellationToken"> A token to monitor for cancellation requests. </param>
	/// <returns> A task containing the instance ID that was assigned the job, or null if no instances are available. </returns>
	Task<string?> DistributeJobAsync(string jobKey, object jobData, CancellationToken cancellationToken);

	/// <summary>
	/// Reports the completion status of a job back to the coordination system.
	/// </summary>
	/// <param name="jobKey"> The unique key identifying the job. </param>
	/// <param name="instanceId"> The instance that processed the job. </param>
	/// <param name="success"> Whether the job completed successfully. </param>
	/// <param name="result"> Optional result data from the job execution. </param>
	/// <param name="cancellationToken"> A token to monitor for cancellation requests. </param>
	/// <returns> A task representing the asynchronous operation. </returns>
	Task ReportJobCompletionAsync(string jobKey, string instanceId, bool success, object? result,
		CancellationToken cancellationToken);
}
