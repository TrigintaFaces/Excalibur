// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Amazon.EventBridge;
using Amazon.SimpleNotificationService;
using Amazon.SQS;

using Excalibur.Dispatch.Serialization;
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
		services.TryAddSingleton<AwsSqsClientRegistration>();
		services.TryAddSingleton<IAmazonSQS>(static sp =>
		{
			var options = sp.GetRequiredService<IOptions<AwsProviderOptions>>().Value;
			var config = new AmazonSQSConfig();
			AwsClientConfiguration.Apply(config, options);
			var credentials = options.Connection.Credentials;
			var sendConfig = new AmazonSQSConfig();
			AwsClientConfiguration.Apply(sendConfig, options);
			sendConfig.MaxErrorRetry = 0;
			sendConfig.MaxStaleConnectionRetries = 0;
			sendConfig.DefaultAWSCredentials = config.DefaultAWSCredentials;
			sendConfig.IdentityResolverConfiguration = config.IdentityResolverConfiguration;
			var receive = credentials is not null
				? new AmazonSQSClient(credentials, config)
				: new AmazonSQSClient(config);
			sp.GetRequiredService<AwsSqsClientRegistration>().Register(receive,
				() => credentials is not null ? new AmazonSQSClient(credentials, sendConfig) : new AmazonSQSClient(sendConfig),
				options.MaxRetryAttempts, options.RequestTimeout);
			return receive;
		});
		services.TryAddSingleton(static sp =>
		{
			var actual = sp.GetRequiredService<IAmazonSQS>();
			var configured = sp.GetRequiredService<IOptions<AwsSqsOptions>>().Value;
			var options = new AwsSqsOptions
			{
				QueueUrl = configured.QueueUrl,
				MaxRetryAttempts = configured.MaxRetryAttempts,
				RequestTimeout = configured.RequestTimeout,
				RetryPolicy = configured.RetryPolicy,
			};
			var send = sp.GetRequiredService<AwsSqsClientRegistration>().Resolve(actual, options);
			return new AwsSqsMessageBus(send, sp.GetRequiredService<IPayloadSerializer>(),
				Microsoft.Extensions.Options.Options.Create(options), sp.GetRequiredService<IOptions<AwsSqsFifoOptions>>(),
				sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<AwsSqsMessageBus>>(), ownsClient: false,
				failureClassifier: sp.GetService<Excalibur.Dispatch.IMessageFailureClassifier>());
		});
		_ = services.AddRemoteMessageBus("sqs", static sp => sp.GetRequiredService<AwsSqsMessageBus>());
	}

	/// <summary>
	/// Registers AWS SNS services directly.
	/// </summary>
	private static void RegisterSnsServices(IServiceCollection services)
	{
		services.TryAddSingleton<AwsSnsClientRegistration>();
		services.TryAddSingleton<IAmazonSimpleNotificationService>(static sp =>
		{
			var options = sp.GetRequiredService<IOptions<AwsProviderOptions>>().Value;
			var config = new AmazonSimpleNotificationServiceConfig();
			AwsClientConfiguration.Apply(config, options);
			var sendConfig = new AmazonSimpleNotificationServiceConfig();
			AwsClientConfiguration.Apply(sendConfig, options);
			sendConfig.MaxErrorRetry = 0;
			sendConfig.MaxStaleConnectionRetries = 0;
			sendConfig.DefaultAWSCredentials = config.DefaultAWSCredentials;
			sendConfig.IdentityResolverConfiguration = config.IdentityResolverConfiguration;
			var credentials = options.Connection.Credentials;
			var management = credentials is not null
				? new AmazonSimpleNotificationServiceClient(credentials, config)
				: new AmazonSimpleNotificationServiceClient(config);
			sp.GetRequiredService<AwsSnsClientRegistration>().Register(management,
				() => credentials is not null ? new AmazonSimpleNotificationServiceClient(credentials, sendConfig) : new AmazonSimpleNotificationServiceClient(sendConfig));
			return management;
		});
		services.TryAddSingleton(static sp =>
		{
			var configured = sp.GetRequiredService<IOptions<AwsSnsOptions>>().Value;
			var options = new AwsSnsOptions
			{
				TopicArn = configured.TopicArn,
				MaxRetryAttempts = configured.MaxRetryAttempts,
				RequestTimeout = configured.RequestTimeout,
				RetryPolicy = configured.RetryPolicy,
			};
			var actual = sp.GetRequiredService<IAmazonSimpleNotificationService>();
			var send = sp.GetRequiredService<AwsSnsClientRegistration>().Resolve(actual);
			return new AwsSnsMessageBus(send, sp.GetRequiredService<IPayloadSerializer>(), options,
				sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<AwsSnsMessageBus>>(),
				failureClassifier: sp.GetService<Excalibur.Dispatch.IMessageFailureClassifier>());
		});
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

			// This client only ever sends (PutEvents), so the dispatch pipeline owns retry outright and
			// the SDK must own none of it. PutEventsRequestEntry carries no client token, so an SDK retry
			// is a distinct event rather than a replay: stacking SDK retries under the outbox's own
			// attempts multiplies duplicates by the product of the two budgets. SQS and SNS express the
			// same rule through a separate zero-retry send client; EventBridge has no receive side, so
			// the whole client is the send client.
			config.MaxErrorRetry = 0;
			config.MaxStaleConnectionRetries = 0;

			return options.Connection.Credentials is { } credentials
				? new AmazonEventBridgeClient(credentials, config)
				: new AmazonEventBridgeClient(config);
		});
		services.TryAddSingleton<AwsEventBridgeMessageBus>();
		_ = services.AddRemoteMessageBus("eventbridge", static sp => sp.GetRequiredService<AwsEventBridgeMessageBus>());
	}
}
