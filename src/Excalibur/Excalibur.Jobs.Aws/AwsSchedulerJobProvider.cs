// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

using Amazon.Scheduler;
using Amazon.Scheduler.Model;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Excalibur.Jobs.Aws;

/// <summary>
/// Provides AWS EventBridge Scheduler integration for Excalibur background jobs.
/// </summary>
/// <remarks> Initializes a new instance of the <see cref="AwsSchedulerJobProvider" /> class. </remarks>
/// <param name="schedulerClient"> The AWS EventBridge Scheduler client. </param>
/// <param name="options"> Configuration options for the AWS scheduler. </param>
/// <param name="logger"> Logger for this provider. </param>
public sealed partial class AwsSchedulerJobProvider(
	AmazonSchedulerClient schedulerClient,
	IOptions<AwsSchedulerOptions> options,
	ILogger<AwsSchedulerJobProvider> logger) : IJobSchedulerProvider, IDisposable
{
	// CA2213 assumes a held IDisposable field is OWNED by the holder. It is not, here, and the
	// registration is the evidence: AwsJobsServiceCollectionExtensions hands this provider
	// `(AmazonSchedulerClient)provider.GetRequiredService<IAmazonScheduler>()` -- the CONTAINER'S
	// singleton, shared with every other consumer and disposed by the container. Disposing it here
	// would kill a client others still hold. This is a suppression of a wrong assumption, not of a
	// real hole: there is no unreleased resource, because this type never acquired one.
	[SuppressMessage(
		"Usage",
		"CA2213:Disposable fields should be disposed",
		Justification = "Container-owned singleton, injected; the container disposes it. See the registration in AwsJobsServiceCollectionExtensions.")]
	private readonly AmazonSchedulerClient _schedulerClient = schedulerClient ?? throw new ArgumentNullException(nameof(schedulerClient));
	private readonly ILogger<AwsSchedulerJobProvider> _logger = logger ?? throw new ArgumentNullException(nameof(logger));
	private readonly AwsSchedulerOptions _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
	private volatile bool _disposed;

	/// <summary>
	/// Schedules a background job using AWS EventBridge Scheduler.
	/// </summary>
	/// <typeparam name="TJob"> The type of job to schedule. </typeparam>
	/// <param name="jobName"> The name of the job. </param>
	/// <param name="cronExpression"> The cron expression for scheduling. </param>
	/// <param name="cancellationToken"> Cancellation token. </param>
	/// <returns> A task that represents the asynchronous operation. </returns>
	public async Task ScheduleJobAsync<TJob>(string jobName, string cronExpression, CancellationToken cancellationToken)
		where TJob : class, IBackgroundJob
	{
		ObjectDisposedException.ThrowIf(_disposed, this);

		var request = new CreateScheduleRequest
		{
			Name = jobName,
			ScheduleExpression = $"cron({cronExpression})",
			ScheduleExpressionTimezone = _options.TimeZone,
			State = ScheduleState.ENABLED,
			FlexibleTimeWindow = new FlexibleTimeWindow { Mode = FlexibleTimeWindowMode.OFF },
			Target = new Target
			{
				Arn = _options.TargetArn,
				RoleArn = _options.ExecutionRoleArn,
				Input = JsonSerializer.Serialize(
						new JobSchedulePayload { JobType = typeof(TJob).AssemblyQualifiedName!, JobName = jobName },
						JobsAwsJsonContext.Default.JobSchedulePayload),
			},
		};

		try
		{
			var response = await _schedulerClient.CreateScheduleAsync(request, cancellationToken).ConfigureAwait(false);
			LogCreatedScheduleSuccess(jobName, typeof(TJob).Name);
		}
		catch (Exception ex)
		{
			LogFailedToCreateSchedule(jobName, typeof(TJob).Name, ex);
			throw;
		}
	}

	/// <summary>
	/// Deletes a scheduled job from AWS EventBridge Scheduler.
	/// </summary>
	/// <param name="jobName"> The name of the job to delete. </param>
	/// <param name="cancellationToken"> Cancellation token. </param>
	/// <returns> A task that represents the asynchronous operation. </returns>
	public async Task DeleteJobAsync(string jobName, CancellationToken cancellationToken)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);

		try
		{
			_ = await _schedulerClient.DeleteScheduleAsync(new DeleteScheduleRequest { Name = jobName }, cancellationToken)
				.ConfigureAwait(false);

			LogDeletedScheduleSuccess(jobName);
		}
		catch (ResourceNotFoundException)
		{
			LogScheduleNotFoundForDeletion(jobName);
		}
		catch (Exception ex)
		{
			LogFailedToDeleteSchedule(jobName, ex);
			throw;
		}
	}

	/// <summary>
	/// Marks this provider unusable. It holds no resource of its own to release.
	/// </summary>
	/// <remarks>
	/// No <c>Dispose(bool)</c> overload and no finalizer: the type is sealed and owns nothing disposable,
	/// so the virtual-dispose pattern would be ceremony around a single flag. The client it uses is the
	/// container's singleton -- injected as
	/// <c>(AmazonSchedulerClient)provider.GetRequiredService&lt;IAmazonScheduler&gt;()</c>, shared with every
	/// other consumer, and disposed by the container. Disposing it here killed a client others still held.
	/// </remarks>
	public void Dispose() => _disposed = true;
}
