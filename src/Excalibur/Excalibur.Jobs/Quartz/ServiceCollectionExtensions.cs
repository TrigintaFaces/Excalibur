// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics.CodeAnalysis;

using Excalibur.Jobs.Abstractions;
using Excalibur.Jobs.Quartz;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

using Quartz;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Provides extension methods for configuring Quartz services in an <see cref="IServiceCollection" />.
/// </summary>
public static class QuartzServiceCollectionExtensions
{
	/// <summary>
	/// Adds Quartz services to the specified service collection and registers the Quartz hosted service.
	/// </summary>
	/// <param name="services"> The service collection to configure. </param>
	/// <param name="withJobs"> An optional action to configure Quartz jobs via <see cref="IServiceCollectionQuartzConfigurator" />. </param>
	/// <returns> The configured <see cref="IServiceCollection" />. </returns>
	/// <exception cref="ArgumentNullException"> Thrown if <paramref name="services" /> is null. </exception>
	public static IServiceCollection AddQuartzWithJobs(
		this IServiceCollection services,
		Action<IServiceCollectionQuartzConfigurator>? withJobs)
	{
		ArgumentNullException.ThrowIfNull(services);

		_ = services.AddQuartz(withJobs);
		_ = services.AddQuartzHostedService(static config => config.WaitForJobsToComplete = true);

		return services;
	}

	/// <summary>
	/// Adds a job watcher service for monitoring job configuration changes.
	/// </summary>
	/// <typeparam name="TJob"> The type of the job to monitor. </typeparam>
	/// <typeparam name="TConfig"> The type of the job configuration. </typeparam>
	/// <param name="services"> The service collection to configure. </param>
	/// <param name="configurationSection"> The configuration section for the job. </param>
	/// <exception cref="ArgumentNullException">
	/// Thrown if <paramref name="services" /> or <paramref name="configurationSection" /> is null.
	/// </exception>
	[RequiresUnreferencedCode("This method uses reflection and may not work correctly with trimming")]
	[RequiresDynamicCode("This method uses dynamic code generation and may not work correctly with AOT")]
	public static void AddJobWatcher<TJob, TConfig>(this IServiceCollection services, IConfigurationSection configurationSection)
		where TJob : IConfigurableJob<TConfig>
		where TConfig : class, IJobConfig
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configurationSection);

		_ = services.Configure<TConfig>(configurationSection);

		// Register a factory-based hosted service that creates the actual service asynchronously This avoids blocking async calls during DI registration
		_ = services.AddSingleton<IHostedService>(static sp => new AsyncFactoryHostedService<TJob, TConfig>(sp));
	}

}
