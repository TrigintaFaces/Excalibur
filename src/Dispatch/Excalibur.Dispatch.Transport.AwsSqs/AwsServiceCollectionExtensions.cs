// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.CodeAnalysis;
using Amazon;
using Amazon.Scheduler;

using Excalibur.Dispatch.Transport;
using Excalibur.Dispatch.Transport.Aws;

using Microsoft.Extensions.Configuration;
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

		var optionsBuilder = services.AddOptions<AwsEventBridgeSchedulerOptions>();
		if (configure != null)
		{
			_ = optionsBuilder.Configure(configure);
		}

		_ = optionsBuilder
			.ValidateOnStart();

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

	/// <summary>
	/// Adds AWS EventBridge Scheduler services to the service collection using an <see cref="IConfiguration"/> section.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="configuration">The configuration section to bind to <see cref="AwsEventBridgeSchedulerOptions"/>.</param>
	/// <returns>The service collection for chaining.</returns>
	[UnconditionalSuppressMessage("AOT", "IL2026:RequiresUnreferencedCode",
		Justification = "Options binding uses reflection by design. AOT consumers should use source-generated alternatives.")]
	[UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCode",
		Justification = "Configuration binding uses reflection by design. AOT consumers should use source-generated alternatives.")]
	public static IServiceCollection AddAwsEventBridgeScheduler(
			this IServiceCollection services,
			IConfiguration configuration)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configuration);

		_ = services.AddOptions<AwsEventBridgeSchedulerOptions>()
			.Bind(configuration)
			.ValidateOnStart();

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
