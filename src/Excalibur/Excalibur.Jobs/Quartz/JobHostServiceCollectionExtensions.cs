// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics.CodeAnalysis;
using System.Reflection;

using Excalibur.Dispatch;

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
	[System.Diagnostics.CodeAnalysis.RequiresDynamicCode("Job host assembly scanning constructs typed invokers at runtime. Use the source-generated handler registration for an ahead-of-time compatible composition.")]
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
	[System.Diagnostics.CodeAnalysis.RequiresDynamicCode("Job host assembly scanning constructs typed invokers at runtime. Use the source-generated handler registration for an ahead-of-time compatible composition.")]
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
	[System.Diagnostics.CodeAnalysis.RequiresDynamicCode("Job host assembly scanning constructs typed invokers at runtime. Use the source-generated handler registration for an ahead-of-time compatible composition.")]
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
	[System.Diagnostics.CodeAnalysis.RequiresDynamicCode("Registers the reflection-based dispatch pipeline, which constructs typed invokers at runtime. Use the source-generated handler registration for an ahead-of-time compatible composition.")]
	internal static IServiceCollection AddExcaliburJobHost(this IServiceCollection services,
		Action<IServiceCollectionQuartzConfigurator>? configureQuartz,
		Action<IJobConfigurator>? configureJobs,
		params Assembly[] assemblies)
	{
		ArgumentNullException.ThrowIfNull(services);

		// The job host enables the local client address — a Quartz-worker semantic consumers should not have
		// to configure — and deliberately pins NO default tenant.
		//
		// It used to pin a wildcard "all tenants" identifier, which read as "this host spans every tenant"
		// and did nothing of the sort: no code compared that value against a stored tenant term, so every
		// row a job wrote landed under a literal tenant named after the wildcard, in a partition no scoped
		// read would ever return. The intent was not achievable through a tenant value at all.
		//
		// Leaving the default unresolved is the honest expression of the same intent. A job host has no
		// tenant of its own, so it names none, and its rows resolve to the reserved untenanted partition
		// that every store already understands. A job that genuinely needs to act across every tenant calls
		// the operation that says so in its name — the name is the control — and a job acting FOR a tenant
		// resolves that tenant from the work it is processing, not from a host-wide pin.
		_ = services.AddExcalibur(builder => builder
			.ScanAssemblies(assemblies)
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
		// — a repeated registration must not add a second heartbeat tracker or scheduler.
		services.TryAddSingleton(sp => new JobHeartbeatTracker(sp.GetService<TimeProvider>()));

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
