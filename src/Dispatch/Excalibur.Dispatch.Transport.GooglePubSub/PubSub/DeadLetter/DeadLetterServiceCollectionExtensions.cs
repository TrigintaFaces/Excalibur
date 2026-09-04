// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics.CodeAnalysis;

using Excalibur.Dispatch.Transport.Google;

using Google.Cloud.PubSub.V1;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for configuring optimized dead letter queue services.
/// </summary>
public static class DeadLetterServiceCollectionExtensions
{
	/// <summary>
	/// Adds optimized dead letter queue services to the service collection.
	/// </summary>
	/// <param name="services"> The service collection. </param>
	/// <param name="configure"> Optional configuration action. </param>
	/// <returns> The service collection for chaining. </returns>
	public static IServiceCollection AddOptimizedDeadLetterQueue(
		this IServiceCollection services,
		Action<DeadLetterOptions>? configure = null)
	{
		ArgumentNullException.ThrowIfNull(services);

		// Register options
		_ = services.AddOptions<DeadLetterOptions>()
			.Configure(options => configure?.Invoke(options))
			.ValidateOnStart();


		// Register core services -- shared Transport.Abstractions interface (keyed by transport name)
		services.TryAddSingleton<PubSubDeadLetterQueueManager>();
		services.AddKeyedSingleton<Excalibur.Dispatch.Transport.IDeadLetterQueueManager>("googlepubsub",
			(sp, _) => sp.GetRequiredService<PubSubDeadLetterQueueManager>());

		return services;
	}

	/// <summary>
	/// Adds optimized dead letter queue services with configuration from IConfiguration.
	/// </summary>
	/// <param name="services"> The service collection. </param>
	/// <param name="configuration"> The configuration section. </param>
	/// <returns> The service collection for chaining. </returns>
	[RequiresUnreferencedCode("Configuration binding may require unreferenced types for reflection-based operations")]
	[RequiresDynamicCode("Configuration binding uses reflection to dynamically access and populate configuration types")]
	public static IServiceCollection AddOptimizedDeadLetterQueue(
		this IServiceCollection services,
		IConfiguration configuration)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configuration);

		_ = services.Configure<DeadLetterOptions>(configuration);

		return services.AddOptimizedDeadLetterQueue();
	}

	/// <summary>
	/// Configures dead letter topic and subscription.
	/// </summary>
	/// <param name="services"> The service collection. </param>
	/// <param name="projectId"> The Google Cloud project ID. </param>
	/// <param name="deadLetterTopicName"> The dead letter topic name. </param>
	/// <returns> The service collection for chaining. </returns>
	public static IServiceCollection ConfigureDeadLetterDestination(
		this IServiceCollection services,
		string projectId,
		string deadLetterTopicName)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(projectId);
		ArgumentNullException.ThrowIfNull(deadLetterTopicName);

		_ = services.Configure<DeadLetterOptions>(options =>
			options.DeadLetterTopicName = new TopicName(projectId, deadLetterTopicName));

		return services;
	}
}
