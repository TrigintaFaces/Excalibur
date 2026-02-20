// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics.CodeAnalysis;

using Amazon.CloudWatch;
using Amazon.SQS;

using Excalibur.Dispatch.Transport;
using Excalibur.Dispatch.Transport.Aws;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;

using LocalPollingMetricsCollector = Excalibur.Dispatch.Transport.Aws.IPollingMetricsCollector;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for configuring AWS SQS long polling services.
/// </summary>
public static class LongPollingServiceCollectionExtensions
{
	/// <summary>
	/// Adds AWS SQS long polling services to the service collection.
	/// </summary>
	/// <param name="services"> The service collection. </param>
	/// <param name="configuration"> The configuration section for long polling. </param>
	/// <returns> The service collection for chaining. </returns>
	[RequiresUnreferencedCode("Configuration binding may require unreferenced types for reflection-based operations")]
	[RequiresDynamicCode("Configuration binding uses reflection to dynamically access and populate configuration types")]
	public static IServiceCollection AddAwsLongPolling(
		this IServiceCollection services,
		IConfiguration configuration)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configuration);

		var config = new LongPollingConfiguration();
		configuration.Bind(config);

		return services.AddAwsLongPolling(config);
	}

	/// <summary>
	/// Adds AWS SQS long polling services to the service collection.
	/// </summary>
	/// <param name="services"> The service collection. </param>
	/// <param name="configureOptions"> Action to configure long polling options. </param>
	/// <returns> The service collection for chaining. </returns>
	public static IServiceCollection AddAwsLongPolling(
		this IServiceCollection services,
		Action<LongPollingConfiguration> configureOptions)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configureOptions);

		var config = new LongPollingConfiguration();
		configureOptions(config);

		return services.AddAwsLongPolling(config);
	}

	/// <summary>
	/// Adds AWS SQS long polling services to the service collection.
	/// </summary>
	/// <param name="services"> The service collection. </param>
	/// <param name="configuration"> The long polling configuration. </param>
	/// <returns> The service collection for chaining. </returns>
	public static IServiceCollection AddAwsLongPolling(
		this IServiceCollection services,
		LongPollingConfiguration configuration)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configuration);

		configuration.Validate();

		// Register configuration
		services.TryAddSingleton(configuration);

		// Register AWS clients if not already registered
		services.TryAddSingleton<IAmazonSQS>(static sp => new AmazonSQSClient());
		services.TryAddSingleton<IAmazonCloudWatch>(static sp => new AmazonCloudWatchClient());

		// Register polling strategy
		if (configuration.EnableAdaptivePolling)
		{
			services.TryAddSingleton<ILongPollingStrategy, AdaptiveLongPollingStrategy>();
		}
		else
		{
			services.TryAddSingleton<ILongPollingStrategy, FixedLongPollingStrategy>();
		}

		// Register metrics collector
		services.TryAddSingleton<LocalPollingMetricsCollector, PollingMetricsCollector>();

		// Register receiver
		services.TryAddScoped<ILongPollingReceiver, SqsLongPollingReceiver>();

		// Register optimizer
		services.TryAddScoped<LongPollingOptimizer>();

		return services;
	}

	/// <summary>
	/// Adds AWS SQS long polling services with a custom strategy.
	/// </summary>
	/// <typeparam name="TStrategy"> The type of polling strategy to use. </typeparam>
	/// <param name="services"> The service collection. </param>
	/// <param name="configuration"> The long polling configuration. </param>
	/// <returns> The service collection for chaining. </returns>
	public static IServiceCollection AddAwsLongPolling<
	[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
	TStrategy>(
		this IServiceCollection services,
		LongPollingConfiguration configuration)
		where TStrategy : class, ILongPollingStrategy
	{
		_ = services.AddAwsLongPolling(configuration);

		// Replace the default strategy
		_ = services.Replace(ServiceDescriptor.Singleton<ILongPollingStrategy, TStrategy>());

		return services;
	}

	/// <summary>
	/// Adds AWS SQS long polling services with a custom metrics collector.
	/// </summary>
	/// <typeparam name="TMetricsCollector"> The type of metrics collector to use. </typeparam>
	/// <param name="services"> The service collection. </param>
	/// <param name="configuration"> The long polling configuration. </param>
	/// <returns> The service collection for chaining. </returns>
	public static IServiceCollection AddAwsLongPollingWithMetrics<
	[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
	TMetricsCollector>(
		this IServiceCollection services,
		LongPollingConfiguration configuration)
		where TMetricsCollector : class, LocalPollingMetricsCollector
	{
		_ = services.AddAwsLongPolling(configuration);

		// Replace the default metrics collector
		_ = services.Replace(ServiceDescriptor.Singleton<LocalPollingMetricsCollector, TMetricsCollector>());

		return services;
	}
}
