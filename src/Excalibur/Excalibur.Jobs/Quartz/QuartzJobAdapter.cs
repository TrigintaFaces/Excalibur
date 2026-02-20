// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics.CodeAnalysis;
using System.Globalization;

using Excalibur.Jobs.Abstractions;
using Excalibur.Jobs.Diagnostics;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Quartz;

namespace Excalibur.Jobs.Quartz;

/// <summary>
/// Adapter that bridges Excalibur background jobs with Quartz.NET.
/// </summary>
/// <inheritdoc />
[DisallowConcurrentExecution]
public sealed partial class QuartzJobAdapter(
	IServiceScopeFactory scopeFactory,
	ILogger<QuartzJobAdapter> logger) : IJob
{
	private readonly IServiceScopeFactory _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
	private readonly ILogger<QuartzJobAdapter> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

	/// <inheritdoc />
	[RequiresUnreferencedCode("Type.GetType(string) is used to resolve job types from JobDataMap string values. " +
		"Pass Type objects in JobDataMap instead of type name strings for AOT compatibility.")]
	public async Task Execute(IJobExecutionContext context)
	{
		ArgumentNullException.ThrowIfNull(context);

		// Handle both Type objects and string type names
		var jobTypeData = context.JobDetail.JobDataMap["JobType"];
		var jobType = jobTypeData switch
		{
			Type type => type,
			string typeName => Type.GetType(typeName),
			_ => null,
		};

		if (jobType == null)
		{
			LogJobTypeNotFoundOrInvalid(context.JobDetail.Key, jobTypeData);
			throw new InvalidOperationException(string.Format(
				CultureInfo.InvariantCulture,
				"Job type not found or invalid for job '{0}'.",
				context.JobDetail.Key));
		}

		using var scope = _scopeFactory.CreateScope();
		var job = scope.ServiceProvider.GetService(jobType);

		if (job == null)
		{
			LogCouldNotResolveJobType(jobType);
			throw new InvalidOperationException(string.Format(
				CultureInfo.InvariantCulture,
				"Could not resolve job type '{0}'.",
				jobType));
		}

		LogExecutingJob(jobType.Name, context.JobDetail.Key);

		try
		{
			if (job is IBackgroundJob backgroundJob)
			{
				await backgroundJob.ExecuteAsync(context.CancellationToken).ConfigureAwait(false);
			}
			else
			{
				LogJobDoesNotImplementInterface(jobType);
				throw new InvalidOperationException(
					string.Format(CultureInfo.InvariantCulture, "Job type '{0}' does not implement required interface.", jobType));
			}

			LogJobCompletedSuccessfully(jobType.Name, context.JobDetail.Key);
		}
		catch (Exception ex)
		{
			LogErrorExecutingJob(jobType.Name, context.JobDetail.Key, ex);
			throw;
		}
	}

	// Source-generated logging methods
	[LoggerMessage(JobsEventId.JobTypeNotFoundOrInvalid, LogLevel.Error,
		"JobType not found or invalid in JobDataMap for job {JobKey}. Value: {JobTypeData}")]
	private partial void LogJobTypeNotFoundOrInvalid(object jobKey, object? jobTypeData);

	[LoggerMessage(JobsEventId.CouldNotResolveJobType, LogLevel.Error,
		"Could not resolve job of type {JobType} from DI container")]
	private partial void LogCouldNotResolveJobType(Type jobType);

	[LoggerMessage(JobsEventId.ExecutingJob, LogLevel.Information,
		"Executing job {JobType} with key {JobKey}")]
	private partial void LogExecutingJob(string jobType, object jobKey);

	[LoggerMessage(JobsEventId.JobDoesNotImplementInterface, LogLevel.Error,
		"Job {JobType} does not implement IBackgroundJob")]
	private partial void LogJobDoesNotImplementInterface(Type jobType);

	[LoggerMessage(JobsEventId.JobCompletedSuccessfully, LogLevel.Information,
		"Job {JobType} with key {JobKey} completed successfully")]
	private partial void LogJobCompletedSuccessfully(string jobType, object jobKey);

	[LoggerMessage(JobsEventId.ErrorExecutingJob, LogLevel.Error,
		"Error executing job {JobType} with key {JobKey}")]
	private partial void LogErrorExecutingJob(string jobType, object jobKey, Exception ex);
}
