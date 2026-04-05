// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics.CodeAnalysis;

using Amazon.SQS;
using Amazon.SQS.Model;

using Excalibur.Dispatch.Transport.Aws;

using Microsoft.Extensions.Configuration;
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
	/// Adds high-throughput SQS channel adapter to the service collection using an <see cref="IConfiguration"/> section.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="configuration">The configuration section to bind to <see cref="SqsChannelOptions"/>.</param>
	/// <returns>The service collection for chaining.</returns>
	public static IServiceCollection AddSqsChannelAdapter(
		this IServiceCollection services,
		IConfiguration configuration)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configuration);

		_ = services.AddOptions<SqsChannelOptions>().Bind(configuration);

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
	/// Adds SQS channel message processor to the service collection using an <see cref="IConfiguration"/> section.
	/// </summary>
	/// <typeparam name="TProcessor">The message processor implementation type.</typeparam>
	/// <param name="services">The service collection.</param>
	/// <param name="configuration">The configuration section to bind to <see cref="SqsProcessorOptions"/>.</param>
	/// <returns>The service collection for chaining.</returns>
	public static IServiceCollection AddSqsChannelProcessor<
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TProcessor>(
		this IServiceCollection services,
		IConfiguration configuration)
		where TProcessor : class, IMessageProcessor<Message>
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configuration);

		_ = services.AddOptions<SqsProcessorOptions>().Bind(configuration);
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
	/// Adds SQS batch processor to the service collection using an <see cref="IConfiguration"/> section.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="configuration">The configuration section to bind to <see cref="SqsBatchOptions"/>.</param>
	/// <returns>The service collection for chaining.</returns>
	public static IServiceCollection AddSqsBatchProcessor(
		this IServiceCollection services,
		IConfiguration configuration)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configuration);

		_ = services.AddOptions<SqsBatchOptions>().Bind(configuration);

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
		Action<ChannelLongPollingOptions> configureOptions)
	{
		_ = services.Configure(configureOptions);

		_ = services.AddSingleton(static provider =>
		{
			var sqsClient = provider.GetRequiredService<IAmazonSQS>();
			var options = provider.GetRequiredService<IOptions<ChannelLongPollingOptions>>().Value;
			var logger = provider.GetRequiredService<ILogger<ChannelLongPollingReceiver>>();

			return new ChannelLongPollingReceiver(sqsClient, options, logger);
		});

		return services;
	}

	/// <summary>
	/// Adds SQS long polling receiver to the service collection using an <see cref="IConfiguration"/> section.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="configuration">The configuration section to bind to <see cref="ChannelLongPollingOptions"/>.</param>
	/// <returns>The service collection for chaining.</returns>
	public static IServiceCollection AddSqsLongPollingReceiver(
		this IServiceCollection services,
		IConfiguration configuration)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configuration);

		_ = services.AddOptions<ChannelLongPollingOptions>().Bind(configuration);

		_ = services.AddSingleton(static provider =>
		{
			var sqsClient = provider.GetRequiredService<IAmazonSQS>();
			var options = provider.GetRequiredService<IOptions<ChannelLongPollingOptions>>().Value;
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
			options.ConcurrentPollers = infrastructureOptions.ChannelAdapter.ConcurrentPollers;
			options.MaxConcurrentPollers = infrastructureOptions.ChannelAdapter.MaxConcurrentPollers;
			options.ReceiveChannelCapacity = infrastructureOptions.ChannelAdapter.ReceiveChannelCapacity;
			options.VisibilityTimeout = infrastructureOptions.VisibilityTimeout;
			options.BatchIntervalMs = infrastructureOptions.ChannelAdapter.BatchIntervalMs;
		});

		// Add message processor
		_ = services.AddSqsChannelProcessor<TProcessor>(options =>
		{
			options.QueueUrl = infrastructureOptions.QueueUrl;
			options.ProcessorCount = infrastructureOptions.Processing.ProcessorCount;
			options.MaxConcurrentMessages = infrastructureOptions.Processing.MaxConcurrentMessages;
			options.DeleteBatchIntervalMs = infrastructureOptions.Processing.DeleteBatchIntervalMs;
		});

		// Add batch processor
		_ = services.AddSqsBatchProcessor(options =>
		{
			options.QueueUrl = infrastructureOptions.QueueUrl;
			options.MaxConcurrentReceiveBatches = infrastructureOptions.Batch.MaxConcurrentReceiveBatches;
			options.MaxConcurrentSendBatches = infrastructureOptions.Batch.MaxConcurrentSendBatches;
			options.LongPollingSeconds = infrastructureOptions.Batch.LongPollingSeconds;
			options.VisibilityTimeout = infrastructureOptions.VisibilityTimeout;
			options.BatchFlushIntervalMs = infrastructureOptions.Batch.BatchFlushIntervalMs;
		});

		// Add metrics collection
		_ = services.AddSingleton<ISqsChannelMetricsCollector, SqsChannelMetricsCollector>();

		return services;
	}
}
