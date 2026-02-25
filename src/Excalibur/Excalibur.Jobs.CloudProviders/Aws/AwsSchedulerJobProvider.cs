// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

using Amazon.Scheduler;
using Amazon.Scheduler.Model;

using Excalibur.Jobs.Abstractions;

using Microsoft.Extensions.Logging;

namespace Excalibur.Jobs.CloudProviders.Aws;

/// <summary>
/// Provides AWS EventBridge Scheduler integration for Excalibur background jobs.
/// </summary>
/// <remarks> Initializes a new instance of the <see cref="AwsSchedulerJobProvider" /> class. </remarks>
/// <param name="schedulerClient"> The AWS EventBridge Scheduler client. </param>
/// <param name="options"> Configuration options for the AWS scheduler. </param>
/// <param name="logger"> Logger for this provider. </param>
public partial class AwsSchedulerJobProvider(
	AmazonSchedulerClient schedulerClient,
	AwsSchedulerOptions options,
	ILogger<AwsSchedulerJobProvider> logger) : IDisposable
{
	private readonly AmazonSchedulerClient _schedulerClient = schedulerClient ?? throw new ArgumentNullException(nameof(schedulerClient));
	private readonly ILogger<AwsSchedulerJobProvider> _logger = logger ?? throw new ArgumentNullException(nameof(logger));
	private readonly AwsSchedulerOptions _options = options ?? throw new ArgumentNullException(nameof(options));
	private volatile bool _disposed;

	/// <summary>
	/// Schedules a background job using AWS EventBridge Scheduler.
	/// </summary>
	/// <typeparam name="TJob"> The type of job to schedule. </typeparam>
	/// <param name="jobName"> The name of the job. </param>
	/// <param name="cronExpression"> The cron expression for scheduling. </param>
	/// <param name="cancellationToken"> Cancellation token. </param>
	/// <returns> A task that represents the asynchronous operation. </returns>
	[RequiresUnreferencedCode("This method uses reflection and may not work correctly with trimming")]
	[RequiresDynamicCode("This method uses dynamic code generation and may not work correctly with AOT")]
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
				Input = JsonSerializer.Serialize(new { JobType = typeof(TJob).AssemblyQualifiedName, JobName = jobName }),
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
	/// Disposes the AWS scheduler client.
	/// </summary>
	public void Dispose()
	{
		Dispose(disposing: true);
		GC.SuppressFinalize(this);
	}

	/// <summary>
	/// Releases the unmanaged resources used by the <see cref="AwsSchedulerJobProvider"/> and optionally releases the managed resources.
	/// </summary>
	/// <param name="disposing">true to release both managed and unmanaged resources; false to release only unmanaged resources.</param>
	protected virtual void Dispose(bool disposing)
	{
		if (!_disposed)
		{
			if (disposing)
			{
				_schedulerClient?.Dispose();
			}

			_disposed = true;
		}
	}
}
