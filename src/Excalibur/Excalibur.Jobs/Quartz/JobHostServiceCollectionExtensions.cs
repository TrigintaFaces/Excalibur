// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Reflection;

using Excalibur.Dispatch.Abstractions;

using Excalibur.Jobs.Core;
using Excalibur.Jobs.Quartz;

using Quartz;

using IJobConfigurator = Excalibur.Jobs.Quartz.IJobConfigurator;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Provides extension methods for configuring Excalibur job hosting services in an <see cref="IServiceCollection" />.
/// </summary>
public static class JobHostServiceCollectionExtensions
{
	/// <summary>
	/// Adds Excalibur Job Host services to the specified service collection with Quartz.NET scheduling.
	/// </summary>
	/// <param name="services"> The service collection to configure. </param>
	/// <param name="assemblies"> An array of assemblies to scan for services and jobs. </param>
	/// <returns> The configured <see cref="IServiceCollection" />. </returns>
	/// <exception cref="ArgumentNullException"> Thrown if <paramref name="services" /> is null. </exception>
	/// <remarks>
	/// This method sets up a complete job hosting environment including:
	/// <list type="bullet">
	/// <item>
	/// <description> Excalibur base services (data, application, domain layers) </description>
	/// </item>
	/// <item>
	/// <description> Context services (TenantId, CorrelationId, ETag, ClientAddress) </description>
	/// </item>
	/// <item>
	/// <description> Quartz.NET job scheduling with dependency injection </description>
	/// </item>
	/// <item>
	/// <description> Health checks for job monitoring </description>
	/// </item>
	/// </list>
	/// </remarks>
	public static IServiceCollection AddExcaliburJobHost(this IServiceCollection services,
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
	public static IServiceCollection AddExcaliburJobHost(this IServiceCollection services,
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
	public static IServiceCollection AddExcaliburJobHost(this IServiceCollection services,
		Action<IJobConfigurator> configureJobs,
		params Assembly[] assemblies)
	{
		ArgumentNullException.ThrowIfNull(configureJobs);

		return services.AddExcaliburJobHost(configureQuartz: null, configureJobs, assemblies);
	}

	/// <summary>
	/// Adds Excalibur Job Host services with both Quartz and job configuration.
	/// This is the recommended unified entry point for configuring job hosting.
	/// </summary>
	/// <param name="services"> The service collection to configure. </param>
	/// <param name="configureQuartz"> Optional action to configure Quartz services. </param>
	/// <param name="configureJobs"> Optional action to configure specific jobs via <see cref="IJobConfigurator" />. </param>
	/// <param name="assemblies"> An array of assemblies to scan for services and jobs. </param>
	/// <returns> The configured <see cref="IServiceCollection" />. </returns>
	/// <exception cref="ArgumentNullException"> Thrown if <paramref name="services" /> is null. </exception>
	/// <remarks>
	/// This method sets up a complete job hosting environment including:
	/// <list type="bullet">
	/// <item>
	/// <description> Excalibur base services (data, application, domain layers) </description>
	/// </item>
	/// <item>
	/// <description> Quartz.NET job scheduling with dependency injection </description>
	/// </item>
	/// <item>
	/// <description> Custom job configuration via fluent API </description>
	/// </item>
	/// <item>
	/// <description> Health checks for job monitoring </description>
	/// </item>
	/// </list>
	/// <para>
	/// Migration from separate calls:
	/// <code>
	/// // Before (deprecated):
	/// services.AddExcaliburJobHost(assemblies);
	/// services.AddExcaliburJobs(q =&gt; { ... });
	/// services.AddExcaliburJobsWithConfiguration(jobs =&gt; { ... });
	///
	/// // After (unified):
	/// services.AddExcaliburJobHost(
	///     configureQuartz: q =&gt; { ... },
	///     configureJobs: jobs =&gt; { ... },
	///     assemblies);
	/// </code>
	/// </para>
	/// </remarks>
	public static IServiceCollection AddExcaliburJobHost(this IServiceCollection services,
		Action<IServiceCollectionQuartzConfigurator>? configureQuartz,
		Action<IJobConfigurator>? configureJobs,
		params Assembly[] assemblies)
	{
		ArgumentNullException.ThrowIfNull(services);

		// Add core Excalibur services
		_ = services.AddExcaliburBaseServices(assemblies, useLocalClientAddress: true, tenantId: TenantDefaults.AllTenants);

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

		// Register the heartbeat tracker as singleton
		_ = services.AddSingleton<JobHeartbeatTracker>();

		// Register the job adapters
		_ = services.AddTransient<QuartzJobAdapter>();
		_ = services.AddTransient(typeof(QuartzGenericJobAdapter<,>));

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
