// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Jobs.GoogleCloud;

using Google.Cloud.Scheduler.V1;

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

		_ = services.Configure(configure);
		_ = services.AddSingleton(static _ => CloudSchedulerClient.Create());
		_ = services.AddSingleton<GoogleCloudSchedulerJobProvider>();

		return services;
	}
}
