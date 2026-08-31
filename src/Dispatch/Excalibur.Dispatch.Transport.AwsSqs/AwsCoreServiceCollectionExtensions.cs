// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Amazon.EventBridge;
using Amazon.SimpleNotificationService;
using Amazon.SQS;

using Excalibur.Dispatch.Transport.Aws;
using Excalibur.Dispatch.Transport.AwsSqs;

using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for registering AWS cloud provider services.
/// </summary>
/// <remarks>
/// <para>
/// For transport-level configuration with fluent builder support, prefer using
/// <see cref="AwsSqsTransportServiceCollectionExtensions.AddAwsSqsTransport(IServiceCollection, string, Action{IAwsSqsTransportBuilder})"/>.
/// </para>
/// <para>
/// This class provides the lower-level <see cref="AddAwsMessageBus"/> method for
/// scenarios that require direct message bus registration without transport abstraction.
/// </para>
/// </remarks>
public static class AwsCoreServiceCollectionExtensions
{
	/// <summary>
	/// Adds AWS message bus services to the service collection.
	/// </summary>
	/// <param name="services"> The service collection. </param>
	/// <param name="configure"> Optional action to configure AWS options. </param>
	/// <returns> The service collection for chaining. </returns>
	/// <remarks>
	/// <para>
	/// For most scenarios, prefer using <see cref="AwsSqsTransportServiceCollectionExtensions.AddAwsSqsTransport(IServiceCollection, string, Action{IAwsSqsTransportBuilder})"/>
	/// which provides the single entry point with full fluent builder support.
	/// </para>
	/// </remarks>
	public static IServiceCollection AddAwsMessageBus(
		this IServiceCollection services,
		Action<AwsMessageBusOptions>? configure = null)
	{
		ArgumentNullException.ThrowIfNull(services);

		// Configure options
		var options = new AwsMessageBusOptions();
		configure?.Invoke(options);
		services.TryAddSingleton(options);

		// Configure AWS-specific options. These values reach the SDK clients registered below through
		// AwsClientConfiguration; a value set here that no client read would be a host pointing at an
		// emulator while talking to real AWS.
		_ = services.AddOptions<AwsProviderOptions>()
			.Configure(awsOptions =>
			{
				if (options.ServiceUrl != null)
				{
					awsOptions.Connection.ServiceUrl = options.ServiceUrl;
				}

				awsOptions.Region = options.Region;
				awsOptions.Connection.UseLocalStack = options.UseLocalStack;
			})
			.ValidateOnStart();

		services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IValidateOptions<AwsProviderOptions>, AwsProviderOptionsValidator>());

		// Register AWS services based on configuration
		if (options.EnableSqs)
		{
			RegisterSqsServices(services);
		}

		if (options.EnableSns)
		{
			RegisterSnsServices(services);
		}

		if (options.EnableEventBridge)
		{
			RegisterEventBridgeServices(services);
		}

		return services;
	}

	/// <summary>
	/// Registers AWS SQS services directly.
	/// </summary>
	private static void RegisterSqsServices(IServiceCollection services)
	{
		services.TryAddSingleton<IAmazonSQS>(static sp =>
		{
			var options = sp.GetRequiredService<IOptions<AwsProviderOptions>>().Value;
			var config = new AmazonSQSConfig();
			AwsClientConfiguration.Apply(config, options);
			return options.Connection.Credentials is { } credentials
				? new AmazonSQSClient(credentials, config)
				: new AmazonSQSClient(config);
		});
		services.TryAddSingleton<AwsSqsMessageBus>();
		_ = services.AddRemoteMessageBus("sqs", static sp => sp.GetRequiredService<AwsSqsMessageBus>());
	}

	/// <summary>
	/// Registers AWS SNS services directly.
	/// </summary>
	private static void RegisterSnsServices(IServiceCollection services)
	{
		services.TryAddSingleton<IAmazonSimpleNotificationService>(static sp =>
		{
			var options = sp.GetRequiredService<IOptions<AwsProviderOptions>>().Value;
			var config = new AmazonSimpleNotificationServiceConfig();
			AwsClientConfiguration.Apply(config, options);
			return options.Connection.Credentials is { } credentials
				? new AmazonSimpleNotificationServiceClient(credentials, config)
				: new AmazonSimpleNotificationServiceClient(config);
		});
		services.TryAddSingleton<AwsSnsMessageBus>();
		_ = services.AddRemoteMessageBus("sns", static sp => sp.GetRequiredService<AwsSnsMessageBus>());
	}

	/// <summary>
	/// Registers AWS EventBridge services directly.
	/// </summary>
	private static void RegisterEventBridgeServices(IServiceCollection services)
	{
		services.TryAddSingleton<IAmazonEventBridge>(static sp =>
		{
			var options = sp.GetRequiredService<IOptions<AwsProviderOptions>>().Value;
			var config = new AmazonEventBridgeConfig();
			AwsClientConfiguration.Apply(config, options);
			return options.Connection.Credentials is { } credentials
				? new AmazonEventBridgeClient(credentials, config)
				: new AmazonEventBridgeClient(config);
		});
		services.TryAddSingleton<AwsEventBridgeMessageBus>();
		_ = services.AddRemoteMessageBus("eventbridge", static sp => sp.GetRequiredService<AwsEventBridgeMessageBus>());
	}
}
