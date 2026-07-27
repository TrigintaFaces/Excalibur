// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Jobs;

/// <summary>
/// Represents a cloud-provider integration that schedules and removes recurring background jobs on the
/// provider's native scheduling infrastructure (for example AWS EventBridge Scheduler, Google Cloud
/// Scheduler, or Azure Logic Apps).
/// </summary>
/// <remarks>
/// Implementations MUST honor the supplied cron expression when creating the underlying schedule. If a
/// cron expression cannot be represented by the provider's native scheduling primitives, an
/// implementation MUST fail with <see cref="NotSupportedException"/> rather than silently substituting a
/// different schedule.
/// </remarks>
public interface IJobSchedulerProvider
{
	/// <summary>
	/// Schedules a background job to run on a recurring basis according to the given cron expression.
	/// </summary>
	/// <typeparam name="TJob"> The type of job to schedule. </typeparam>
	/// <param name="jobName"> The name of the job. </param>
	/// <param name="cronExpression"> The 5-field cron expression describing the recurrence schedule. </param>
	/// <param name="cancellationToken"> Cancellation token. </param>
	/// <returns> A task that represents the asynchronous operation. </returns>
	Task ScheduleJobAsync<TJob>(string jobName, string cronExpression, CancellationToken cancellationToken)
		where TJob : class, IBackgroundJob;

	/// <summary>
	/// Deletes a previously scheduled job from the provider's scheduling infrastructure.
	/// </summary>
	/// <param name="jobName"> The name of the job to delete. </param>
	/// <param name="cancellationToken"> Cancellation token. </param>
	/// <returns> A task that represents the asynchronous operation. </returns>
	Task DeleteJobAsync(string jobName, CancellationToken cancellationToken);
}
