// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Azure.Identity;
using Azure.ResourceManager;

using Excalibur.Jobs.Azure;

using Microsoft.Extensions.Configuration;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for configuring Azure Logic Apps job provider services.
/// </summary>
public static class AzureJobsServiceCollectionExtensions
{
	/// <summary>
	/// Adds Azure Logic Apps job provider to the service collection.
	/// </summary>
	/// <param name="services"> The service collection. </param>
	/// <param name="configure"> The configuration action for Azure Logic Apps options. </param>
	/// <returns> The service collection for method chaining. </returns>
	public static IServiceCollection AddAzureLogicApps(
		this IServiceCollection services,
		Action<AzureLogicAppsOptions> configure)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configure);

		_ = services.Configure(configure);
		_ = services.AddSingleton(static provider =>
			new ArmClient(new DefaultAzureCredential()));
		_ = services.AddSingleton<AzureLogicAppsJobProvider>();

		return services;
	}

	/// <summary>
	/// Adds Azure Logic Apps job provider to the service collection
	/// using an <see cref="IConfiguration"/> section.
	/// </summary>
	/// <param name="services"> The service collection. </param>
	/// <param name="configuration"> The configuration section to bind options from. </param>
	/// <returns> The service collection for method chaining. </returns>
	public static IServiceCollection AddAzureLogicApps(
		this IServiceCollection services,
		IConfiguration configuration)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configuration);

		_ = services.AddOptions<AzureLogicAppsOptions>()
			.Bind(configuration);
		_ = services.AddSingleton(static provider =>
			new ArmClient(new DefaultAzureCredential()));
		_ = services.AddSingleton<AzureLogicAppsJobProvider>();

		return services;
	}
}
