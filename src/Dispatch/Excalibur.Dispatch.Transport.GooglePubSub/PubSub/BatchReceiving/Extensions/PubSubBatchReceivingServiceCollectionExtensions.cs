// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics.CodeAnalysis;

using Excalibur.Dispatch.Transport.Google;

using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for configuring Google Pub/Sub batch receiving.
/// </summary>
public static class PubSubBatchReceivingServiceCollectionExtensions
{
	/// <summary>
	/// Adds Google Pub/Sub batch receiving services to the service collection.
	/// </summary>
	/// <param name="services"> The service collection. </param>
	/// <param name="configure"> Configuration action. </param>
	/// <returns> The service collection for chaining. </returns>
	public static IServiceCollection AddPubSubBatchReceiving(
		this IServiceCollection services,
		Action<BatchReceivingBuilder>? configure = null)
	{
		// Register core services
		services.TryAddSingleton<BatchMetricsCollector>();
		services.TryAddSingleton<IBatchReceiver, PubSubBatchReceiver>();

		// Register default batching strategy
		services.TryAddSingleton<IBatchingStrategy, AdaptiveBatchingStrategy>();

		// Register configuration
		_ = services.Configure<BatchConfiguration>(static config =>
		{
			config.MaxMessagesPerBatch = 1000;
			config.MaxBatchWaitTime = TimeSpan.FromMilliseconds(100);
			config.EnableAdaptiveBatching = true;
			config.ConcurrentBatchProcessors = Environment.ProcessorCount;
		});

		// Apply custom configuration
		if (configure != null)
		{
			var builder = new BatchReceivingBuilder(services);
			configure(builder);
		}

		return services;
	}

	/// <summary>
	/// Adds a specific batch processor implementation.
	/// </summary>
	/// <typeparam name="TProcessor"> The processor type. </typeparam>
	public static IServiceCollection AddBatchProcessor<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TProcessor>(this IServiceCollection services)
		where TProcessor : class, IBatchProcessor
	{
		services.TryAddScoped<IBatchProcessor, TProcessor>();
		return services;
	}
}
