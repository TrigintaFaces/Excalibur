// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics.CodeAnalysis;
using System.Reflection;

using Excalibur.Dispatch.Abstractions;

using Excalibur.Jobs.Core;
using Excalibur.Jobs.Quartz;

using Microsoft.Extensions.DependencyInjection.Extensions;

using Quartz;

using IJobConfigurator = Excalibur.Jobs.Quartz.IJobConfigurator;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Provides internal extension methods for configuring Excalibur job hosting services.
/// Consumers opt-in via <c>IExcaliburBuilder.AddJobs(...)</c> or the
/// <c>IHostApplicationBuilder.AddExcaliburJobHost(...)</c> carve-out.
/// </summary>
internal static class JobHostServiceCollectionExtensions
{
	/// <summary>
	/// Adds Excalibur Job Host services to the specified service collection with Quartz.NET scheduling.
	/// </summary>
	/// <param name="services"> The service collection to configure. </param>
	/// <param name="assemblies"> An array of assemblies to scan for services and jobs. </param>
	/// <returns> The configured <see cref="IServiceCollection" />. </returns>
	/// <exception cref="ArgumentNullException"> Thrown if <paramref name="services" /> is null. </exception>
	[RequiresUnreferencedCode("Job host assembly scanning discovers handlers and validators via reflection.")]
	internal static IServiceCollection AddExcaliburJobHost(this IServiceCollection services,
		params Assembly[] assemblies)
	{
		ArgumentNullException.ThrowIfNull(services);

		// Delegate to the full overload with no configuration
		return services.AddExcaliburJobHost(configureQuartz: null, configureJobs: null, assemblies);
	}

	/// <summary>
	/// Adds Excalibur Job Host services with custom Quartz configuration.
	/// </summary>
	/// <param name="services"> The service collection to configure. </param>
	/// <param name="configureQuartz"> Optional action to configure Quartz services. </param>
	/// <param name="assemblies"> An array of assemblies to scan for services and jobs. </param>
	/// <returns> The configured <see cref="IServiceCollection" />. </returns>
	/// <exception cref="ArgumentNullException"> Thrown if <paramref name="services" /> is null. </exception>
	[RequiresUnreferencedCode("Job host assembly scanning discovers handlers and validators via reflection.")]
	internal static IServiceCollection AddExcaliburJobHost(this IServiceCollection services,
		Action<IServiceCollectionQuartzConfigurator>? configureQuartz,
		params Assembly[] assemblies)
	{
		return services.AddExcaliburJobHost(configureQuartz, configureJobs: null, assemblies);
	}

	/// <summary>
	/// Adds Excalibur Job Host services with job configuration.
	/// </summary>
	/// <param name="services"> The service collection to configure. </param>
	/// <param name="configureJobs"> Action to configure specific jobs via <see cref="IJobConfigurator" />. </param>
	/// <param name="assemblies"> An array of assemblies to scan for services and jobs. </param>
	/// <returns> The configured <see cref="IServiceCollection" />. </returns>
	/// <exception cref="ArgumentNullException"> Thrown if <paramref name="services" /> or <paramref name="configureJobs" /> is null. </exception>
	[RequiresUnreferencedCode("Job host assembly scanning discovers handlers and validators via reflection.")]
	internal static IServiceCollection AddExcaliburJobHost(this IServiceCollection services,
		Action<IJobConfigurator> configureJobs,
		params Assembly[] assemblies)
	{
		ArgumentNullException.ThrowIfNull(configureJobs);

		return services.AddExcaliburJobHost(configureQuartz: null, configureJobs, assemblies);
	}

	/// <summary>
	/// Adds Excalibur Job Host services with both Quartz and job configuration.
	/// This is the canonical internal entry point; consumers reach it via
	/// <c>IExcaliburBuilder.AddJobs(...)</c>.
	/// </summary>
	/// <param name="services"> The service collection to configure. </param>
	/// <param name="configureQuartz"> Optional action to configure Quartz services. </param>
	/// <param name="configureJobs"> Optional action to configure specific jobs via <see cref="IJobConfigurator" />. </param>
	/// <param name="assemblies"> An array of assemblies to scan for services and jobs. </param>
	/// <returns> The configured <see cref="IServiceCollection" />. </returns>
	/// <exception cref="ArgumentNullException"> Thrown if <paramref name="services" /> is null. </exception>
	[RequiresUnreferencedCode("Job host assembly scanning discovers handlers and validators via reflection.")]
	internal static IServiceCollection AddExcaliburJobHost(this IServiceCollection services,
		Action<IServiceCollectionQuartzConfigurator>? configureQuartz,
		Action<IJobConfigurator>? configureJobs,
		params Assembly[] assemblies)
	{
		ArgumentNullException.ThrowIfNull(services);

		// S804 bd-sdhocq A8: AddExcaliburBaseServices replaced by AddExcalibur + builder context.
		// Jobs pin a default tenant of AllTenants and enable local client address — these are
		// Quartz-worker semantics that consumers should not have to configure.
		_ = services.AddExcalibur(builder => builder
			.ScanAssemblies(assemblies)
			.UseTenant(TenantDefaults.AllTenants)
			.UseLocalClientAddress());

		// Add Quartz.NET with configuration
		_ = services.AddQuartz(q =>
		{
			// Apply custom Quartz configuration if provided
			configureQuartz?.Invoke(q);
		});

		// Add the Quartz hosted service
		_ = services.AddQuartzHostedService(options =>
		{
			options.WaitForJobsToComplete = true;
			options.AwaitApplicationStarted = true;
		});

		// Register the heartbeat tracker as singleton.
		// TryAdd* ensures idempotence under repeated AddJobs(...)/AddExcaliburJobHost(...) invocations
		// — surfaced by the S804 ADR-325 §Secondary paired-test discipline (bd-addjobs-idempotency).
		services.TryAddSingleton<JobHeartbeatTracker>();

		// Register the job adapters. TryAdd* for the same idempotence reason as above.
		services.TryAddTransient<QuartzJobAdapter>();
		services.TryAddTransient(typeof(QuartzGenericJobAdapter<,>));

		// Apply job configuration if provided
		if (configureJobs != null)
		{
			var jobConfigurator = new JobConfigurator(services);
			configureJobs(jobConfigurator);
		}

		// Per-job health checks are registered via each job's ConfigureHealthChecks method.
		// The JobHealthCheck class requires per-job parameters (jobName, config) that
		// cannot be resolved from DI generically.

		return services;
	}
}
