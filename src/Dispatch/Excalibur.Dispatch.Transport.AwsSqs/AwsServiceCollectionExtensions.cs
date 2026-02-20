// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Amazon;
using Amazon.Scheduler;

using Excalibur.Dispatch.Transport;
using Excalibur.Dispatch.Transport.Aws;

using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for registering AWS scheduler services.
/// </summary>
/// <remarks>
/// <para>
/// For AWS SQS transport configuration, use <see cref="AwsSqsTransportServiceCollectionExtensions.AddAwsSqsTransport(IServiceCollection, string, Action{IAwsSqsTransportBuilder})"/>
/// which provides the single entry point.
/// </para>
/// </remarks>
public static class AwsServiceCollectionExtensions
{
	/// <summary>
	/// Adds AWS EventBridge Scheduler services to the service collection.
	/// </summary>
	/// <param name="services"> The service collection. </param>
	/// <param name="configure"> Optional action to configure scheduler options. </param>
	/// <returns> The service collection for chaining. </returns>
	public static IServiceCollection AddAwsEventBridgeScheduler(
			this IServiceCollection services,
			Action<AwsEventBridgeSchedulerOptions>? configure = null)
	{
		ArgumentNullException.ThrowIfNull(services);

		if (configure != null)
		{
			_ = services.Configure(configure);
		}

		services.TryAddSingleton<IAmazonScheduler>(sp =>
		{
			var schedulerOptions =
							sp.GetRequiredService<IOptions<AwsEventBridgeSchedulerOptions>>().Value;

			var config = new AmazonSchedulerConfig
			{
				RegionEndpoint = RegionEndpoint.GetBySystemName(
									schedulerOptions.Region),
			};

			return new AmazonSchedulerClient(config);
		});

		services.TryAddSingleton<AwsEventBridgeScheduler>();
		services.TryAddSingleton<IMessageScheduler>(sp =>
				sp.GetRequiredService<AwsEventBridgeScheduler>());

		return services;
	}
}
