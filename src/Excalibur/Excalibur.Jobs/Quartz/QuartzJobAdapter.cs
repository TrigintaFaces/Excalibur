// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0


using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Reflection;
using System.Runtime.Loader;
using System.Runtime.CompilerServices;

using Excalibur.Jobs.Core;
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
	public async ValueTask Execute(IJobExecutionContext context, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(context);

		try
		{
			var (jobType, heartbeat) = await ExecuteInScopeAsync(context, cancellationToken).ConfigureAwait(false);
			cancellationToken.ThrowIfCancellationRequested();
			heartbeat?.RecordHeartbeat(context.JobDetail.Key.Name);
			LogJobCompletedSuccessfully(jobType.Name, context.JobDetail.Key);
		}
		catch (Exception ex)
		{
			LogErrorExecutingJob(nameof(QuartzJobAdapter), context.JobDetail.Key, ex);
			throw;
		}
	}

	private async Task<(Type JobType, JobHeartbeatTracker? Heartbeat)> ExecuteInScopeAsync(
		IJobExecutionContext context, CancellationToken cancellationToken)
	{
		await using var scope = _scopeFactory.CreateAsyncScope();
		var jobTypeData = context.JobDetail.JobDataMap["JobType"];
		var jobType = jobTypeData switch
		{
			Type type => type,
			string typeName => ResolveRegisteredType(scope.ServiceProvider, typeName)
				?? (RuntimeFeature.IsDynamicCodeSupported ? ResolveJobType(typeName) : null),
			_ => null,
		};
		if (jobType is null)
		{
			LogJobTypeNotFoundOrInvalid(context.JobDetail.Key, jobTypeData);
			throw new InvalidOperationException($"Job type not found or invalid for job '{context.JobDetail.Key}'.");
		}
		var job = scope.ServiceProvider.GetService(jobType);
		if (job is not IBackgroundJob backgroundJob)
		{
			throw new InvalidOperationException($"Could not resolve background job type '{jobType}'.");
		}
		LogExecutingJob(jobType.Name, context.JobDetail.Key);
		cancellationToken.ThrowIfCancellationRequested();
		await backgroundJob.ExecuteAsync(cancellationToken).ConfigureAwait(false);
		return (jobType, scope.ServiceProvider.GetService<JobHeartbeatTracker>());
	}

	private static Type? ResolveRegisteredType(IServiceProvider services, string identity)
	{
		var matches = services.GetServices<RegisteredJobType>().Where(r => r.Matches(identity)).Select(r => r.Type).Distinct().ToArray();
		return matches.Length switch
		{
			0 => null,
			1 => matches[0],
			_ => throw new InvalidOperationException($"Ambiguous registered job identity '{identity}'."),
		};
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

	// Caches resolved job types by their JobDataMap type-name string. Resolution walks the whole
	// AppDomain, so without this every trigger fire re-scanned all loaded assemblies via reflection.
	// Job types are stable for the process lifetime, so a successful resolution is cached permanently
	// (which also makes duplicate-type-name resolution deterministic after the first fire).
	private static readonly ConcurrentDictionary<string, Type> ResolvedTypeCache = new(StringComparer.Ordinal);

	private static Type? ResolveJobType(string typeName)
	{
		if (string.IsNullOrWhiteSpace(typeName))
		{
			return null;
		}

		if (ResolvedTypeCache.TryGetValue(typeName, out var cached))
		{
			return cached;
		}

		var resolved = ResolveJobTypeCore(typeName);
		if (resolved is not null)
		{
			ResolvedTypeCache[typeName] = resolved;
		}

		return resolved;
	}

	// DISP003 is suppressed on the reflective lookups below rather than satisfied.
	//
	// Quartz identifies a job by a type NAME supplied in configuration, so resolving it is reflection
	// by definition -- there is no compile-time symbol to bind. The analyzer asks for
	// [RequiresUnreferencedCode]/[RequiresDynamicCode] on this method, which would propagate to its
	// caller and out to the adapter's public surface; the project standard is that consumer-facing APIs
	// never carry those attributes, so satisfying it here would annotate the public API to quiet a
	// diagnostic about a private helper.
	//
	// The same lines already suppress IL2026 for the same reason. The residual risk is genuine and
	// narrow: under native AOT a job type not otherwise referenced may fail to resolve, and the method
	// returns null rather than throwing, so the caller reports an unresolvable job.
	private static Type? ResolveJobTypeCore(string typeName)
	{
		var simpleTypeName = typeName;
		var assemblyName = string.Empty;
		var commaIndex = typeName.IndexOf(',', StringComparison.Ordinal);
		if (commaIndex > 0)
		{
			simpleTypeName = typeName[..commaIndex].Trim();
			var assemblySegment = typeName[(commaIndex + 1)..];
			var nextComma = assemblySegment.IndexOf(',', StringComparison.Ordinal);
			assemblyName = nextComma >= 0
				? assemblySegment[..nextComma].Trim()
				: assemblySegment.Trim();
		}

		if (!string.IsNullOrWhiteSpace(assemblyName))
		{
			var assembly = FindLoadedAssembly(assemblyName) ?? TryLoadAssembly(assemblyName);
#pragma warning disable IL2026, DISP003 // Quartz job types are named in configuration; see note above
			var resolvedFromAssembly = assembly?.GetType(simpleTypeName, throwOnError: false, ignoreCase: false);
#pragma warning restore IL2026, DISP003
			if (resolvedFromAssembly is not null)
			{
				return resolvedFromAssembly;
			}
		}

		foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
		{
			if (assembly.IsDynamic)
			{
				continue;
#pragma warning disable IL2026, DISP003 // Quartz job types are named in configuration; see note above
			}

			var resolved = assembly.GetType(typeName, throwOnError: false, ignoreCase: false)
				?? assembly.GetType(simpleTypeName, throwOnError: false, ignoreCase: false);
#pragma warning restore IL2026, DISP003
			if (resolved is not null)
			{
				return resolved;
			}
		}

		return null;
	}

	private static Assembly? FindLoadedAssembly(string assemblyName)
	{
		foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
		{
			if (string.Equals(assembly.GetName().Name, assemblyName, StringComparison.Ordinal))
			{
				return assembly;
			}
		}

		return null;
	}

	private static Assembly? TryLoadAssembly(string assemblyName)
	{
		try
		{
			return AssemblyLoadContext.Default.LoadFromAssemblyName(new AssemblyName(assemblyName));
		}
		catch (Exception ex) when (ex is FileNotFoundException or FileLoadException or BadImageFormatException)
		{
			return null;
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
