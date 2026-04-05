// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Jobs.GoogleCloud;

using Google.Cloud.Scheduler.V1;

using Microsoft.Extensions.Configuration;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for configuring Google Cloud Scheduler job provider services.
/// </summary>
public static class GoogleCloudJobsServiceCollectionExtensions
{
	/// <summary>
	/// Adds Google Cloud Scheduler job provider to the service collection.
	/// </summary>
	/// <param name="services"> The service collection. </param>
	/// <param name="configure"> The configuration action for Google Cloud Scheduler options. </param>
	/// <returns> The service collection for method chaining. </returns>
	public static IServiceCollection AddGoogleCloudScheduler(
		this IServiceCollection services,
		Action<GoogleCloudSchedulerOptions> configure)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configure);

		_ = services.AddOptions<GoogleCloudSchedulerOptions>()
			.Configure(configure)
			.ValidateDataAnnotations()
			.ValidateOnStart();
		_ = services.AddSingleton(static _ => CloudSchedulerClient.Create());
		_ = services.AddSingleton<GoogleCloudSchedulerJobProvider>();

		return services;
	}

	/// <summary>
	/// Adds Google Cloud Scheduler job provider to the service collection
	/// using an <see cref="IConfiguration"/> section.
	/// </summary>
	/// <param name="services"> The service collection. </param>
	/// <param name="configuration"> The configuration section to bind options from. </param>
	/// <returns> The service collection for method chaining. </returns>
	public static IServiceCollection AddGoogleCloudScheduler(
		this IServiceCollection services,
		IConfiguration configuration)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configuration);

		_ = services.AddOptions<GoogleCloudSchedulerOptions>()
			.Bind(configuration)
			.ValidateDataAnnotations()
			.ValidateOnStart();
		_ = services.AddSingleton(static _ => CloudSchedulerClient.Create());
		_ = services.AddSingleton<GoogleCloudSchedulerJobProvider>();

		return services;
	}
}
