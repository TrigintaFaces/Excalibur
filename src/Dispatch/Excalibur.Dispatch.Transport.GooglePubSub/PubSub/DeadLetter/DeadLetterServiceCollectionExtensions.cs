// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics.CodeAnalysis;

using Excalibur.Dispatch.Transport.Google;

using Google.Cloud.PubSub.V1;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;

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
			.ValidateDataAnnotations()
			.ValidateOnStart();

		// Register core services — shared Transport.Abstractions interface
		services.TryAddSingleton<Excalibur.Dispatch.Transport.IDeadLetterQueueManager, PubSubDeadLetterQueueManager>();

		// Register poison detection
		_ = services.AddPoisonMessageDetection();

		// Register retry policies
		_ = services.AddRetryPolicies();

		// Register analytics (optional - can be enabled separately);
		services.TryAddSingleton<DeadLetterAnalyticsService>();

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
	/// Adds poison message detection services.
	/// </summary>
	/// <param name="services"> The service collection. </param>
	/// <param name="configure"> Optional configuration action. </param>
	/// <returns> The service collection for chaining. </returns>
	public static IServiceCollection AddPoisonMessageDetection(
		this IServiceCollection services,
		Action<PoisonDetectionOptions>? configure = null)
	{
		ArgumentNullException.ThrowIfNull(services);

		// Register options
		_ = services.AddOptions<PoisonDetectionOptions>()
			.Configure(options =>
			{
				// Set defaults
				options.MaxFailuresBeforePoison = 5;
				options.RapidFailureCount = 3;
				options.RapidFailureWindow = TimeSpan.FromMinutes(1);
				options.ConsistentExceptionThreshold = 0.8;
				options.TimeoutThreshold = 0.7;
				options.LoopDetectionThreshold = 10;
				options.HistoryRetentionPeriod = TimeSpan.FromHours(24);

				configure?.Invoke(options);
			})
			.ValidateDataAnnotations()
			.ValidateOnStart();

		// Register detector
		services.TryAddSingleton<AdvancedPoisonMessageDetector>();

		return services;
	}

	/// <summary>
	/// Adds retry policy management services.
	/// </summary>
	/// <param name="services"> The service collection. </param>
	/// <param name="configure"> Optional configuration action. </param>
	/// <returns> The service collection for chaining. </returns>
	public static IServiceCollection AddRetryPolicies(
		this IServiceCollection services,
		Action<RetryPolicyOptions>? configure = null)
	{
		ArgumentNullException.ThrowIfNull(services);

		// Register options
		_ = services.AddOptions<RetryPolicyOptions>()
			.Configure(options => configure?.Invoke(options))
			.ValidateDataAnnotations()
			.ValidateOnStart();

		// Register manager
		services.TryAddSingleton<RetryPolicyManager>();

		return services;
	}

	/// <summary>
	/// Adds dead letter analytics services as a hosted service.
	/// </summary>
	/// <param name="services"> The service collection. </param>
	/// <param name="configure"> Optional configuration action. </param>
	/// <returns> The service collection for chaining. </returns>
	public static IServiceCollection AddDeadLetterAnalytics(
		this IServiceCollection services,
		Action<DeadLetterAnalyticsOptions>? configure = null)
	{
		ArgumentNullException.ThrowIfNull(services);

		// Register options
		_ = services.AddOptions<DeadLetterAnalyticsOptions>()
			.Configure(options => configure?.Invoke(options))
			.ValidateDataAnnotations()
			.ValidateOnStart();

		// Register analytics service as hosted service
		_ = services.AddSingleton<DeadLetterAnalyticsService>();
		_ = services.AddHostedService(provider => provider.GetRequiredService<DeadLetterAnalyticsService>());

		return services;
	}

	/// <summary>
	/// Configures dead letter topic and subscription.
	/// </summary>
	/// <param name="services"> The service collection. </param>
	/// <param name="projectId"> The Google Cloud project ID. </param>
	/// <param name="deadLetterTopicName"> The dead letter topic name. </param>
	/// <param name="deadLetterSubscriptionName"> The dead letter subscription name. </param>
	/// <returns> The service collection for chaining. </returns>
	public static IServiceCollection ConfigureDeadLetterDestination(
		this IServiceCollection services,
		string projectId,
		string deadLetterTopicName,
		string? deadLetterSubscriptionName = null)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(projectId);
		ArgumentNullException.ThrowIfNull(deadLetterTopicName);

		_ = services.Configure<DeadLetterOptions>(options =>
			options.DeadLetterTopicName = new TopicName(projectId, deadLetterTopicName));

		if (!string.IsNullOrEmpty(deadLetterSubscriptionName))
		{
			_ = services.Configure<DeadLetterAnalyticsOptions>(options =>
				options.DeadLetterSubscription = new SubscriptionName(projectId, deadLetterSubscriptionName));
		}

		return services;
	}

	/// <summary>
	/// Adds custom poison detection rule.
	/// </summary>
	/// <param name="services"> The service collection. </param>
	/// <param name="rule"> The custom rule to add. </param>
	/// <returns> The service collection for chaining. </returns>
	public static IServiceCollection AddPoisonDetectionRule(
		this IServiceCollection services,
		IPoisonDetectionRule rule)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(rule);

		_ = services.AddSingleton(rule);

		// Register a startup action to add the rule to the detector
		_ = services.AddHostedService<PoisonRuleRegistrationService>();

		return services;
	}

	/// <summary>
	/// Adds custom retry strategy for a specific message type.
	/// </summary>
	/// <param name="services"> The service collection. </param>
	/// <param name="messageType"> The message type. </param>
	/// <param name="strategy"> The retry strategy. </param>
	/// <returns> The service collection for chaining. </returns>
	public static IServiceCollection AddCustomRetryStrategy(
		this IServiceCollection services,
		string messageType,
		RetryStrategy strategy)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(messageType);
		ArgumentNullException.ThrowIfNull(strategy);

		_ = services.Configure<RetryPolicyOptions>(options => options.CustomStrategies[messageType] = strategy);

		return services;
	}

	/// <summary>
	/// Configures dead letter queue with builder pattern.
	/// </summary>
	/// <param name="services"> The service collection. </param>
	/// <param name="configure"> Configuration builder action. </param>
	/// <returns> The service collection for chaining. </returns>
	public static IServiceCollection AddGooglePubSubDeadLetterQueue(
		this IServiceCollection services,
		Action<DeadLetterQueueBuilder> configure)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configure);

		var builder = new DeadLetterQueueBuilder(services);
		configure(builder);
		builder.Build();

		return services;
	}
}
