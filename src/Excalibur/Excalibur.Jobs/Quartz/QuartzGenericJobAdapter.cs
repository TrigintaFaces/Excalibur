// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text.Json;

using Excalibur.Jobs.Abstractions;

using Microsoft.Extensions.Logging;

using Quartz;

namespace Excalibur.Jobs.Quartz;

/// <summary>
/// Generic adapter for jobs with context.
/// </summary>
/// <typeparam name="TJob"> The job type. </typeparam>
/// <typeparam name="TContext"> The context type. </typeparam>
/// <inheritdoc />
[DisallowConcurrentExecution]
public sealed class QuartzGenericJobAdapter<TJob, TContext>(
	TJob job,
	ILogger<QuartzGenericJobAdapter<TJob, TContext>> logger) : IJob
	where TJob : IBackgroundJob<TContext>
	where TContext : class
{
	private const string ContextNotFoundMessage = "Context not found or invalid in JobDataMap for job '{0}'.";

	private readonly TJob _job = job ?? throw new ArgumentNullException(nameof(job));
	private readonly ILogger<QuartzGenericJobAdapter<TJob, TContext>> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

	/// <inheritdoc />
	[RequiresUnreferencedCode("This method uses reflection and may not work correctly with trimming")]
	[RequiresDynamicCode("This method uses dynamic code generation and may not work correctly with AOT")]
	public async Task Execute(IJobExecutionContext context)
	{
		ArgumentNullException.ThrowIfNull(context);

		TContext? jobContext = null;

		// Try to get context from JobDataMap - could be direct object or serialized JSON
		var contextData = context.JobDetail.JobDataMap["Context"];
		if (contextData is TContext directContext)
		{
			jobContext = directContext;
		}
		else if (contextData is string jsonContext)
		{
			try
			{
				jobContext = JsonSerializer.Deserialize<TContext>(jsonContext);
			}
			catch (JsonException ex)
			{
				QuartzGenericJobAdapterLog.ContextDeserializationFailed(_logger, ex, context.JobDetail.Key);
#pragma warning disable CA1863 // Exception path only - CompositeFormat caching not needed
				throw new InvalidOperationException(string.Format(CultureInfo.InvariantCulture, ContextNotFoundMessage, context.JobDetail.Key), ex);
#pragma warning restore CA1863
			}
		}

		if (jobContext == null)
		{
			QuartzGenericJobAdapterLog.ContextNotFoundOrInvalid(_logger, context.JobDetail.Key);
#pragma warning disable CA1863 // Exception path only - CompositeFormat caching not needed
			throw new InvalidOperationException(string.Format(CultureInfo.InvariantCulture, ContextNotFoundMessage, context.JobDetail.Key));
#pragma warning restore CA1863
		}

		QuartzGenericJobAdapterLog.ExecutingGenericJob(_logger, typeof(TJob).Name, context.JobDetail.Key);

		try
		{
			await _job.ExecuteAsync(jobContext, context.CancellationToken).ConfigureAwait(false);
			QuartzGenericJobAdapterLog.GenericJobCompletedSuccessfully(_logger, typeof(TJob).Name, context.JobDetail.Key);
		}
		catch (Exception ex)
		{
			QuartzGenericJobAdapterLog.GenericJobExecutionFailed(_logger, ex, typeof(TJob).Name, context.JobDetail.Key);
			throw;
		}
	}
}
