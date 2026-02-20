// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics.CodeAnalysis;

using Amazon.SQS;
using Amazon.SQS.Model;

using Excalibur.Dispatch.Transport.Aws;

using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for configuring AWS SQS channel-based components.
/// </summary>
public static class SqsChannelServiceCollectionExtensions
{
	/// <summary>
	/// Adds high-throughput SQS channel adapter to the service collection.
	/// </summary>
	public static IServiceCollection AddSqsChannelAdapter(
		this IServiceCollection services,
		Action<SqsChannelOptions> configureOptions)
	{
		_ = services.Configure(configureOptions);

		_ = services.AddSingleton(static provider =>
		{
			var sqsClient = provider.GetRequiredService<IAmazonSQS>();
			var options = provider.GetRequiredService<IOptions<SqsChannelOptions>>().Value;
			var logger = provider.GetRequiredService<ILogger<SqsChannelAdapter>>();

			return new SqsChannelAdapter(sqsClient, options, logger);
		});

		return services;
	}

	/// <summary>
	/// Adds SQS channel message processor to the service collection.
	/// </summary>
	public static IServiceCollection AddSqsChannelProcessor<
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TProcessor>(
		this IServiceCollection services,
		Action<SqsProcessorOptions> configureOptions)
		where TProcessor : class, IMessageProcessor<Message>
	{
		_ = services.Configure(configureOptions);
		_ = services.AddTransient<IMessageProcessor<Message>, TProcessor>();

		_ = services.AddSingleton(static provider =>
		{
			var sqsClient = provider.GetRequiredService<IAmazonSQS>();
			var channelAdapter = provider.GetRequiredService<SqsChannelAdapter>();
			var messageProcessor = provider.GetRequiredService<IMessageProcessor<Message>>();
			var options = provider.GetRequiredService<IOptions<SqsProcessorOptions>>().Value;
			var logger = provider.GetRequiredService<ILogger<SqsChannelMessageProcessor>>();

			return new SqsChannelMessageProcessor(
				sqsClient, channelAdapter, messageProcessor, options, logger);
		});

		_ = services.AddHostedService<SqsChannelProcessorHostedService>();

		return services;
	}

	/// <summary>
	/// Adds SQS batch processor to the service collection.
	/// </summary>
	public static IServiceCollection AddSqsBatchProcessor(
		this IServiceCollection services,
		Action<SqsBatchOptions> configureOptions)
	{
		_ = services.Configure(configureOptions);

		_ = services.AddSingleton(static provider =>
		{
			var sqsClient = provider.GetRequiredService<IAmazonSQS>();
			var options = provider.GetRequiredService<IOptions<SqsBatchOptions>>().Value;
			var logger = provider.GetRequiredService<ILogger<SqsBatchProcessor>>();

			return new SqsBatchProcessor(sqsClient, options, logger);
		});

		return services;
	}

	/// <summary>
	/// Adds SQS long polling receiver to the service collection.
	/// </summary>
	public static IServiceCollection AddSqsLongPollingReceiver(
		this IServiceCollection services,
		Action<LongPollingOptions> configureOptions)
	{
		_ = services.Configure(configureOptions);

		_ = services.AddSingleton(static provider =>
		{
			var sqsClient = provider.GetRequiredService<IAmazonSQS>();
			var options = provider.GetRequiredService<IOptions<LongPollingOptions>>().Value;
			var logger = provider.GetRequiredService<ILogger<ChannelLongPollingReceiver>>();

			return new ChannelLongPollingReceiver(sqsClient, options, logger);
		});

		return services;
	}

	/// <summary>
	/// Adds complete SQS channel infrastructure with all optimizations.
	/// </summary>
	public static IServiceCollection AddSqsChannelInfrastructure<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TProcessor>(
		this IServiceCollection services,
		Action<SqsChannelInfrastructureOptions> configureOptions)
		where TProcessor : class, IMessageProcessor<Message>
	{
		ArgumentNullException.ThrowIfNull(configureOptions);

		var infrastructureOptions = new SqsChannelInfrastructureOptions();
		configureOptions(infrastructureOptions);

		// Add AWS SQS client if not already registered
		services.TryAddSingleton<IAmazonSQS>(provider =>
		{
			var config = new AmazonSQSConfig();
			if (infrastructureOptions.ServiceUrl != null)
			{
				config.ServiceURL = infrastructureOptions.ServiceUrl.ToString();
			}

			return new AmazonSQSClient(config);
		});

		// Add channel adapter
		_ = services.AddSqsChannelAdapter(options =>
		{
			options.QueueUrl = infrastructureOptions.QueueUrl;
			options.ConcurrentPollers = infrastructureOptions.ConcurrentPollers;
			options.MaxConcurrentPollers = infrastructureOptions.MaxConcurrentPollers;
			options.ReceiveChannelCapacity = infrastructureOptions.ReceiveChannelCapacity;
			options.VisibilityTimeout = infrastructureOptions.VisibilityTimeout;
			options.BatchIntervalMs = infrastructureOptions.BatchIntervalMs;
		});

		// Add message processor
		_ = services.AddSqsChannelProcessor<TProcessor>(options =>
		{
			options.QueueUrl = infrastructureOptions.QueueUrl;
			options.ProcessorCount = infrastructureOptions.ProcessorCount;
			options.MaxConcurrentMessages = infrastructureOptions.MaxConcurrentMessages;
			options.DeleteBatchIntervalMs = infrastructureOptions.DeleteBatchIntervalMs;
		});

		// Add batch processor
		_ = services.AddSqsBatchProcessor(options =>
		{
			options.QueueUrl = infrastructureOptions.QueueUrl;
			options.MaxConcurrentReceiveBatches = infrastructureOptions.MaxConcurrentReceiveBatches;
			options.MaxConcurrentSendBatches = infrastructureOptions.MaxConcurrentSendBatches;
			options.LongPollingSeconds = infrastructureOptions.LongPollingSeconds;
			options.VisibilityTimeout = infrastructureOptions.VisibilityTimeout;
			options.BatchFlushIntervalMs = infrastructureOptions.BatchFlushIntervalMs;
		});

		// Add metrics collection
		_ = services.AddSingleton<ISqsChannelMetricsCollector, SqsChannelMetricsCollector>();

		return services;
	}
}
