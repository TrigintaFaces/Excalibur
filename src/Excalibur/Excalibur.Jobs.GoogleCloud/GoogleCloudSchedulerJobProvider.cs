// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Text.Json;

using Excalibur.Jobs.Abstractions;

using Google.Api.Gax.ResourceNames;
using Google.Cloud.Scheduler.V1;
using Google.Protobuf;

using Microsoft.Extensions.Logging;

namespace Excalibur.Jobs.GoogleCloud;

/// <summary>
/// Provides Google Cloud Scheduler integration for Excalibur background jobs.
/// </summary>
/// <remarks> Initializes a new instance of the <see cref="GoogleCloudSchedulerJobProvider" /> class. </remarks>
/// <param name="schedulerClient"> The Google Cloud Scheduler client. </param>
/// <param name="options"> Configuration options for Google Cloud Scheduler. </param>
/// <param name="logger"> Logger for this provider. </param>
public sealed partial class GoogleCloudSchedulerJobProvider(
	CloudSchedulerClient schedulerClient,
	GoogleCloudSchedulerOptions options,
	ILogger<GoogleCloudSchedulerJobProvider> logger) : IDisposable
{
	private readonly CloudSchedulerClient _schedulerClient = schedulerClient ?? throw new ArgumentNullException(nameof(schedulerClient));
	private readonly GoogleCloudSchedulerOptions _options = options ?? throw new ArgumentNullException(nameof(options));
	private readonly ILogger<GoogleCloudSchedulerJobProvider> _logger = logger ?? throw new ArgumentNullException(nameof(logger));
	private volatile bool _disposed;

	/// <summary>
	/// Schedules a background job using Google Cloud Scheduler.
	/// </summary>
	/// <typeparam name="TJob"> The type of job to schedule. </typeparam>
	/// <param name="jobName"> The name of the job. </param>
	/// <param name="cronExpression"> The cron expression for scheduling (unix-cron format). </param>
	/// <param name="cancellationToken"> Cancellation token. </param>
	/// <returns> A task that represents the asynchronous operation. </returns>
	public async Task ScheduleJobAsync<TJob>(string jobName, string cronExpression, CancellationToken cancellationToken)
		where TJob : class, IBackgroundJob
	{
		ObjectDisposedException.ThrowIf(_disposed, this);

		var parent = new LocationName(_options.ProjectId, _options.LocationId);
		var job = new Job
		{
			Name = $"projects/{_options.ProjectId}/locations/{_options.LocationId}/jobs/{jobName}",
			Schedule = cronExpression,
			TimeZone = _options.TimeZone,
			HttpTarget = new HttpTarget
			{
				Uri = _options.TargetUrl,
				HttpMethod = Google.Cloud.Scheduler.V1.HttpMethod.Post,
				Body = ByteString.CopyFromUtf8(
					JsonSerializer.Serialize(
						new JobSchedulePayload { JobType = typeof(TJob).AssemblyQualifiedName!, JobName = jobName },
						JobsGcfJsonContext.Default.JobSchedulePayload)),
				Headers = { { "Content-Type", "application/json" } },
			},
		};

		try
		{
			_ = await _schedulerClient.CreateJobAsync(parent, job, cancellationToken).ConfigureAwait(false);
			LogCreatedJobSuccess(jobName, typeof(TJob).Name);
		}
		catch (Exception ex)
		{
			LogFailedToCreateJob(jobName, typeof(TJob).Name, ex);
			throw;
		}
	}

	/// <summary>
	/// Deletes a scheduled job from Google Cloud Scheduler.
	/// </summary>
	/// <param name="jobName"> The name of the job to delete. </param>
	/// <param name="cancellationToken"> Cancellation token. </param>
	/// <returns> A task that represents the asynchronous operation. </returns>
	public async Task DeleteJobAsync(string jobName, CancellationToken cancellationToken)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);

		var jobResourceName = new JobName(_options.ProjectId, _options.LocationId, jobName);

		try
		{
			await _schedulerClient.DeleteJobAsync(jobResourceName, cancellationToken).ConfigureAwait(false);
			LogDeletedJobSuccess(jobName);
		}
		catch (Grpc.Core.RpcException ex) when (ex.StatusCode == Grpc.Core.StatusCode.NotFound)
		{
			LogJobNotFoundForDeletion(jobName);
		}
		catch (Exception ex)
		{
			LogFailedToDeleteJob(jobName, ex);
			throw;
		}
	}

	/// <summary>
	/// Disposes the Google Cloud Scheduler client.
	/// </summary>
	public void Dispose()
	{
		if (!_disposed)
		{
			_disposed = true;
		}
	}
}
