// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Azure.Identity;
using Azure.ResourceManager;

using Excalibur.Jobs.CloudProviders.Azure;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for configuring Azure cloud provider services.
/// </summary>
public static class AzureServiceCollectionExtensions
{
	/// <summary>
	/// Adds Azure Logic Apps job provider to the service collection.
	/// </summary>
	/// <param name="services"> The service collection to add the services Excalibur.Dispatch.Transport.Aws.Sqs.LongPolling.Configuration. </param>
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
}
