// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0


using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Microsoft.Extensions.DependencyInjection;

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

	private readonly IServiceProvider? _services;

	/// <summary>Creates an adapter with access to job-specific JSON metadata.</summary>
	/// <param name="job">The scoped job.</param>
	/// <param name="logger">The logger.</param>
	/// <param name="services">The execution scope service provider.</param>
	[ActivatorUtilitiesConstructor]
	public QuartzGenericJobAdapter(TJob job, ILogger<QuartzGenericJobAdapter<TJob, TContext>> logger, IServiceProvider services)
		: this(job, logger)
	{
		_services = services ?? throw new ArgumentNullException(nameof(services));
	}

	/// <inheritdoc />
	public async ValueTask Execute(IJobExecutionContext context, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(context);

		TContext? jobContext = null;

		// Try to get context from JobDataMap - could be direct object or serialized JSON
		var data = context.JobDetail.JobDataMap;
		if (data.ContainsKey("ContextType") && data["ContextType"] is string contextType
			&& !new RegisteredJobType(typeof(TContext)).Matches(contextType))
		{
			throw new InvalidOperationException($"Persisted context type does not match job '{context.JobDetail.Key}'.");
		}
		var contextData = data.ContainsKey("ContextData") ? data["ContextData"] : data["Context"];
		if (contextData is TContext directContext)
		{
			jobContext = directContext;
		}
		else if (contextData is string jsonContext)
		{
			try
			{
#pragma warning disable IL2026, IL3050 // Quartz IJob.Execute cannot carry Requires* attributes; the context type is only known to the caller
				var mode = data.ContainsKey("ContextSerialization") ? data["ContextSerialization"] as string : "reflection-v1";
				if (mode is not null && mode.StartsWith("metadata-v1:", StringComparison.Ordinal))
				{
					var typeInfo = _services?.GetKeyedService<JsonTypeInfo<TContext>>((typeof(TJob), context.JobDetail.Key, mode[12..]))
						?? throw new InvalidOperationException($"Register JSON metadata version '{mode[12..]}' for job '{context.JobDetail.Key}' on every startup, or explicitly migrate its persisted context.");
					jobContext = JsonSerializer.Deserialize(jsonContext, typeInfo);
				}
				else if (mode == "reflection-v1" && JsonSerializer.IsReflectionEnabledByDefault)
				{
					jobContext = JsonSerializer.Deserialize<TContext>(jsonContext);
				}
				else
				{
					throw new InvalidOperationException($"Unsupported persisted context serialization for job '{context.JobDetail.Key}'. Migrate legacy JSON before Native AOT execution.");
				}
#pragma warning restore IL2026, IL3050
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
			cancellationToken.ThrowIfCancellationRequested();
			await _job.ExecuteAsync(jobContext, cancellationToken).ConfigureAwait(false);
			cancellationToken.ThrowIfCancellationRequested();
			QuartzGenericJobAdapterLog.GenericJobCompletedSuccessfully(_logger, typeof(TJob).Name, context.JobDetail.Key);
		}
		catch (Exception ex)
		{
			QuartzGenericJobAdapterLog.GenericJobExecutionFailed(_logger, ex, typeof(TJob).Name, context.JobDetail.Key);
			throw;
		}
	}
	/// <summary>Executes using the cancellation token supplied by the execution context.</summary>
	/// <param name="context">The Quartz execution context.</param>
	/// <returns>The asynchronous execution.</returns>
	public Task Execute(IJobExecutionContext context)
	{
		ArgumentNullException.ThrowIfNull(context);
		return Execute(context, context.CancellationToken).AsTask();
	}
}
