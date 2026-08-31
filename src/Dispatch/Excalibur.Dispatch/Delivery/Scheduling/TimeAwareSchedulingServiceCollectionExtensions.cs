// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics.CodeAnalysis;

using Excalibur.Dispatch;
using Excalibur.Dispatch.Delivery;
using Excalibur.Dispatch.Options.Scheduling;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for configuring time-aware scheduling services with integrated TimePolicy support. Service registration for
/// configurable timeout handling in scheduled message processing.
/// </summary>
public static class TimeAwareSchedulingServiceCollectionExtensions
{
	/// <summary>
	/// Adds time-aware scheduled message service with default timeout configuration.
	/// </summary>
	/// <param name="services"> The service collection. </param>
	/// <returns> The service collection for method chaining. </returns>
	[RequiresUnreferencedCode("Registers the reflection-based dispatch pipeline, which requires types that trimming may remove. Use the source-generated handler registration for an ahead-of-time compatible composition.")]
	[RequiresDynamicCode("Registers the reflection-based dispatch pipeline, which constructs typed invokers at runtime. Use the source-generated handler registration for an ahead-of-time compatible composition.")]
	public static IServiceCollection AddTimeAwareScheduling(this IServiceCollection services)
	{
		// Add TimePolicy services
		_ = services.AddTimePolicy();

		// Register the time-aware scheduled message service
		_ = AddScheduledDeliveryRuntime(services);

		services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IValidateOptions<TimeAwareSchedulerOptions>, TimeAwareSchedulerOptionsValidator>());

		// Configure options with default values
		_ = services.AddOptions<TimeAwareSchedulerOptions>()
			.Configure(static options =>
			{
				// Default values are already set in TimeAwareSchedulerOptions
			})
			.ValidateOnStart();

		return services;
	}

	/// <summary>
	/// Adds time-aware scheduled message service with configuration binding.
	/// </summary>
	/// <param name="services"> The service collection. </param>
	/// <param name="configuration"> The configuration instance. </param>
	/// <returns> The service collection for method chaining. </returns>
	[RequiresDynamicCode("Registers the reflection-based dispatch pipeline, which constructs typed invokers at runtime. Use the source-generated handler registration for an ahead-of-time compatible composition.")]
	[RequiresUnreferencedCode("Registers the reflection-based dispatch pipeline, which requires types that trimming may remove. Use the source-generated handler registration for an ahead-of-time compatible composition.")]
	public static IServiceCollection AddTimeAwareScheduling(this IServiceCollection services, IConfiguration configuration)
	{
		// Add TimePolicy services with configuration
		_ = services.AddTimePolicy(configuration);

		// Register the time-aware scheduled message service
		_ = AddScheduledDeliveryRuntime(services);

		services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IValidateOptions<TimeAwareSchedulerOptions>, TimeAwareSchedulerOptionsValidator>());

		// Bind configuration from appsettings
		_ = services.AddOptions<TimeAwareSchedulerOptions>()
			.Bind(configuration.GetSection(TimeAwareSchedulerOptions.SectionName))
			.ValidateOnStart();

		return services;
	}

	/// <summary>
	/// Adds time-aware scheduled message service with custom configuration.
	/// </summary>
	/// <param name="services"> The service collection. </param>
	/// <param name="configureScheduler"> Action to configure the time-aware scheduler options. </param>
	/// <param name="configureTimePolicy"> Optional action to configure the time policy options. </param>
	/// <returns> The service collection for method chaining. </returns>
	[RequiresUnreferencedCode("Registers the reflection-based dispatch pipeline, which requires types that trimming may remove. Use the source-generated handler registration for an ahead-of-time compatible composition.")]
	[RequiresDynamicCode("Registers the reflection-based dispatch pipeline, which constructs typed invokers at runtime. Use the source-generated handler registration for an ahead-of-time compatible composition.")]
	public static IServiceCollection AddTimeAwareScheduling(
		this IServiceCollection services,
		Action<TimeAwareSchedulerOptions> configureScheduler,
		Action<TimePolicyOptions>? configureTimePolicy = null)
	{
		// Add TimePolicy services with optional configuration
		if (configureTimePolicy != null)
		{
			_ = services.AddTimePolicy(configureTimePolicy);
		}
		else
		{
			_ = services.AddTimePolicy();
		}

		// Register the time-aware scheduled message service
		_ = AddScheduledDeliveryRuntime(services);

		services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IValidateOptions<TimeAwareSchedulerOptions>, TimeAwareSchedulerOptionsValidator>());

		// Configure scheduler options using the provided action
		_ = services.AddOptions<TimeAwareSchedulerOptions>()
			.Configure(configureScheduler)
			.ValidateOnStart();

		return services;
	}

	/// <summary>
	/// Adds time-aware scheduled message service with adaptive timeout capabilities.
	/// </summary>
	/// <param name="services"> The service collection. </param>
	/// <param name="configureScheduler"> Optional action to configure additional scheduler options. </param>
	/// <returns> The service collection for method chaining. </returns>
	[RequiresUnreferencedCode("Registers the reflection-based dispatch pipeline, which requires types that trimming may remove. Use the source-generated handler registration for an ahead-of-time compatible composition.")]
	[RequiresDynamicCode("Registers the reflection-based dispatch pipeline, which constructs typed invokers at runtime. Use the source-generated handler registration for an ahead-of-time compatible composition.")]
	public static IServiceCollection AddAdaptiveTimeAwareScheduling(
		this IServiceCollection services,
		Action<TimeAwareSchedulerOptions>? configureScheduler = null)
	{
		// Add adaptive TimePolicy services
		_ = services.AddAdaptiveTimeouts();

		// Register the time-aware scheduled message service
		_ = AddScheduledDeliveryRuntime(services);

		services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IValidateOptions<TimeAwareSchedulerOptions>, TimeAwareSchedulerOptionsValidator>());

		// Configure scheduler options with adaptive timeouts enabled
		_ = services.AddOptions<TimeAwareSchedulerOptions>()
			.Configure(options =>
			{
				options.Adaptive.EnableAdaptiveTimeouts = true;
				options.Timeouts.LogSchedulingTimeouts = true;
				configureScheduler?.Invoke(options);
			})
			.ValidateOnStart();

		return services;
	}

	/// <summary>
	/// Adds time-aware scheduled message service without timeout monitoring for minimal overhead scenarios.
	/// </summary>
	/// <param name="services"> The service collection. </param>
	/// <param name="configureScheduler"> Optional action to configure the scheduler options. </param>
	/// <returns> The service collection for method chaining. </returns>
	[RequiresUnreferencedCode("Registers the reflection-based dispatch pipeline, which requires types that trimming may remove. Use the source-generated handler registration for an ahead-of-time compatible composition.")]
	[RequiresDynamicCode("Registers the reflection-based dispatch pipeline, which constructs typed invokers at runtime. Use the source-generated handler registration for an ahead-of-time compatible composition.")]
	public static IServiceCollection AddLightweightTimeAwareScheduling(
		this IServiceCollection services,
		Action<TimeAwareSchedulerOptions>? configureScheduler = null)
	{
		// Add TimePolicy services without monitoring
		_ = services.AddTimePolicyWithoutMonitoring();

		// Register the time-aware scheduled message service
		_ = AddScheduledDeliveryRuntime(services);

		services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IValidateOptions<TimeAwareSchedulerOptions>, TimeAwareSchedulerOptionsValidator>());

		// Configure scheduler options for lightweight operation
		_ = services.AddOptions<TimeAwareSchedulerOptions>()
			.Configure(options =>
			{
				options.Adaptive.EnableAdaptiveTimeouts = false;
				options.Timeouts.LogSchedulingTimeouts = false;
				configureScheduler?.Invoke(options);
			})
			.ValidateOnStart();

		return services;
	}

	/// <summary>
	/// Adds time-aware scheduled message service with throughput-optimized configuration for enterprise scenarios.
	/// </summary>
	/// <param name="services"> The service collection. </param>
	/// <param name="configureScheduler"> Optional action to configure additional scheduler options. </param>
	/// <returns> The service collection for method chaining. </returns>
	[RequiresUnreferencedCode("Registers the reflection-based dispatch pipeline, which requires types that trimming may remove. Use the source-generated handler registration for an ahead-of-time compatible composition.")]
	[RequiresDynamicCode("Registers the reflection-based dispatch pipeline, which constructs typed invokers at runtime. Use the source-generated handler registration for an ahead-of-time compatible composition.")]
	public static IServiceCollection AddThroughputTimeAwareScheduling(
		this IServiceCollection services,
		Action<TimeAwareSchedulerOptions>? configureScheduler = null)
	{
		// Add adaptive TimePolicy services with optimized configuration
		_ = services.AddAdaptiveTimeouts(options =>
		{
			options.Adaptive.UseAdaptiveTimeouts = true;
			options.Adaptive.MinimumSampleSize = 25; // Lower sample size for faster adaptation
			options.Adaptive.AdaptiveTimeoutPercentile = 90; // Slightly lower percentile for better performance
			options.Observability.LogTimeoutEvents = true;
		});

		// Register the time-aware scheduled message service
		_ = AddScheduledDeliveryRuntime(services);

		services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IValidateOptions<TimeAwareSchedulerOptions>, TimeAwareSchedulerOptionsValidator>());

		// Configure scheduler options for throughput
		_ = services.AddOptions<TimeAwareSchedulerOptions>()
			.Configure(options =>
			{
				options.Adaptive.EnableAdaptiveTimeouts = true;
				options.Adaptive.EnableTimeoutEscalation = true;
				options.Adaptive.MinimumSampleSize = 25;
				options.Adaptive.AdaptiveTimeoutPercentile = 90;
				options.Timeouts.LogSchedulingTimeouts = true;

				// Optimize timeouts for high performance
				options.PollInterval = TimeSpan.FromSeconds(15); // More frequent polling
				options.Timeouts.ScheduleRetrievalTimeout = TimeSpan.FromSeconds(20);
				options.Timeouts.DeserializationTimeout = TimeSpan.FromSeconds(5);
				options.Timeouts.ScheduleUpdateTimeout = TimeSpan.FromSeconds(10);

				configureScheduler?.Invoke(options);
			})
			.ValidateOnStart();

		return services;
	}

	/// <summary>
	/// Enables time-aware timeout policies on an already-registered <see cref="ScheduledMessageService"/>.
	/// Registers <see cref="ITimePolicy"/> services so the unified scheduler applies per-operation timeouts.
	/// </summary>
	/// <param name="services"> The service collection. </param>
	/// <returns> The service collection for method chaining. </returns>
	[RequiresUnreferencedCode("Registers the reflection-based dispatch pipeline, which requires types that trimming may remove. Use the source-generated handler registration for an ahead-of-time compatible composition.")]
	[RequiresDynamicCode("Registers the reflection-based dispatch pipeline, which constructs typed invokers at runtime. Use the source-generated handler registration for an ahead-of-time compatible composition.")]
	public static IServiceCollection ReplaceWithTimeAwareScheduling(this IServiceCollection services)
	{
		ArgumentNullException.ThrowIfNull(services);

		// The unified ScheduledMessageService detects ITimePolicy via DI automatically.
		// Just register the time policy services.
		return services.AddTimeAwareScheduling();
	}

	/// <summary>
	/// Configures message-specific timeouts for scheduled message processing.
	/// </summary>
	/// <param name="services"> The service collection. </param>
	/// <param name="configureMessageTimeouts"> Action to configure message-specific timeouts. </param>
	/// <returns> The service collection for method chaining. </returns>
	public static IServiceCollection ConfigureSchedulingMessageTimeouts(
		this IServiceCollection services,
		Action<Dictionary<string, TimeSpan>> configureMessageTimeouts)
	{
		_ = services.Configure<TimeAwareSchedulerOptions>(options => configureMessageTimeouts(options.MessageTypeSchedulingTimeouts));

		return services;
	}

	/// <summary>
	/// Configures scheduling timeouts for a specific message type.
	/// </summary>
	/// <typeparam name="TMessage"> The message type. </typeparam>
	/// <param name="services"> The service collection. </param>
	/// <param name="timeout"> The timeout to apply for this message type during scheduling. </param>
	/// <returns> The service collection for method chaining. </returns>
	public static IServiceCollection ConfigureSchedulingTimeoutFor<TMessage>(this IServiceCollection services, TimeSpan timeout)
	{
		var messageTypeName = typeof(TMessage).FullName ?? typeof(TMessage).Name;

		_ = services.Configure<TimeAwareSchedulerOptions>(options => options.MessageTypeSchedulingTimeouts[messageTypeName] = timeout);

		return services;
	}

	/// <summary>
	/// Enables comprehensive timeout logging and metrics for scheduling operations.
	/// </summary>
	/// <param name="services"> The service collection. </param>
	/// <returns> The service collection for method chaining. </returns>
	public static IServiceCollection EnableSchedulingTimeoutTelemetry(this IServiceCollection services)
	{
		_ = services.Configure<TimeAwareSchedulerOptions>(static options =>
		{
			options.Timeouts.LogSchedulingTimeouts = true;
		});

		_ = services.Configure<TimePolicyOptions>(static options =>
		{
			options.Observability.LogTimeoutEvents = true;
		});

		return services;
	}

	/// <summary>
	/// Registers the hosted service that fires scheduled deliveries and, in the same act, the boot-time gate
	/// that refuses a schedule store which cannot keep them.
	/// </summary>
	/// <remarks>
	/// The two are registered together rather than at separate call sites on purpose. Running this hosted
	/// service is what turns a schedule from a request into a promise: the host accepts a delivery now and
	/// owes it later. A volatile store breaks that promise during a restart, having already reported the
	/// schedule as accepted, and the failure appears only as an absence. Composing the gate here means every
	/// entry point that starts scheduled delivery carries the refusal with it, so a new entry point cannot be
	/// added later that runs schedules without one.
	/// </remarks>
	/// <param name="services"> The service collection. </param>
	/// <returns> The same <see cref="IServiceCollection" /> for chaining. </returns>
	[RequiresUnreferencedCode("Registers the reflection-based dispatch pipeline, which requires types that trimming may remove. Use the source-generated handler registration for an ahead-of-time compatible composition.")]
	[RequiresDynamicCode("Registers the reflection-based dispatch pipeline, which constructs typed invokers at runtime. Use the source-generated handler registration for an ahead-of-time compatible composition.")]
	private static IServiceCollection AddScheduledDeliveryRuntime(IServiceCollection services)
	{
		ArgumentNullException.ThrowIfNull(services);

		// ScheduledMessageService dispatches the messages it releases and drives a cron scheduler, so it
		// resolves IDispatcher and ICronScheduler. Seat both (all TryAdd) rather than leave a hosted service
		// registered against infrastructure the composition may not contain. Composing AddDispatchScheduling
		// first does not weaken the durability gate below: AddDurableScheduleStore uses Replace precisely so
		// a durable store still wins whichever order the two are called in.
		_ = services.AddDispatchPipeline();
		_ = services.AddDispatchScheduling();

		_ = services.AddHostedService<ScheduledMessageService>();
		_ = services.AddScheduleDurabilityGate();

		return services;
	}

}
