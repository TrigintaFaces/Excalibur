// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics.CodeAnalysis;

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Delivery;
using Excalibur.Dispatch.Options.Scheduling;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for configuring time-aware scheduling services with integrated TimePolicy support. R7.4: Service registration for
/// configurable timeout handling in scheduled message processing.
/// </summary>
public static class TimeAwareSchedulingServiceCollectionExtensions
{
	/// <summary>
	/// Adds time-aware scheduled message service with default timeout configuration.
	/// </summary>
	/// <param name="services"> The service collection. </param>
	/// <returns> The service collection for method chaining. </returns>
	public static IServiceCollection AddTimeAwareScheduling(this IServiceCollection services)
	{
		// Add TimePolicy services
		_ = services.AddTimePolicy();

		// Register the time-aware scheduled message service
		_ = services.AddHostedService<TimeAwareScheduledMessageService>();

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
	[UnconditionalSuppressMessage("Trimming", "IL2026:Members annotated with RequiresUnreferencedCode may break with trimming",
		Justification = "Options types are preserved through DI registration and configuration binding")]
	[RequiresDynamicCode(
		"Configuration binding for time-aware scheduling requires dynamic code generation for property reflection and value conversion.")]
	[UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCode",
		Justification = "Configuration binding uses reflection by design. AOT consumers should use source-generated alternatives.")]
	public static IServiceCollection AddTimeAwareScheduling(this IServiceCollection services, IConfiguration configuration)
	{
		// Add TimePolicy services with configuration
		_ = services.AddTimePolicy(configuration);

		// Register the time-aware scheduled message service
		_ = services.AddHostedService<TimeAwareScheduledMessageService>();

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
		_ = services.AddHostedService<TimeAwareScheduledMessageService>();

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
	public static IServiceCollection AddAdaptiveTimeAwareScheduling(
		this IServiceCollection services,
		Action<TimeAwareSchedulerOptions>? configureScheduler = null)
	{
		// Add adaptive TimePolicy services
		_ = services.AddAdaptiveTimeouts();

		// Register the time-aware scheduled message service
		_ = services.AddHostedService<TimeAwareScheduledMessageService>();

		services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IValidateOptions<TimeAwareSchedulerOptions>, TimeAwareSchedulerOptionsValidator>());

		// Configure scheduler options with adaptive timeouts enabled
		_ = services.AddOptions<TimeAwareSchedulerOptions>()
			.Configure(options =>
			{
				options.Adaptive.EnableAdaptiveTimeouts = true;
				options.Timeouts.IncludeTimeoutMetrics = true;
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
	public static IServiceCollection AddLightweightTimeAwareScheduling(
		this IServiceCollection services,
		Action<TimeAwareSchedulerOptions>? configureScheduler = null)
	{
		// Add TimePolicy services without monitoring
		_ = services.AddTimePolicyWithoutMonitoring();

		// Register the time-aware scheduled message service
		_ = services.AddHostedService<TimeAwareScheduledMessageService>();

		services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IValidateOptions<TimeAwareSchedulerOptions>, TimeAwareSchedulerOptionsValidator>());

		// Configure scheduler options for lightweight operation
		_ = services.AddOptions<TimeAwareSchedulerOptions>()
			.Configure(options =>
			{
				options.Timeouts.EnableTimeoutPolicies = true;
				options.Adaptive.EnableAdaptiveTimeouts = false;
				options.Timeouts.IncludeTimeoutMetrics = false;
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
			options.Observability.IncludeTimeoutMetrics = true;
			options.Observability.LogTimeoutEvents = true;
		});

		// Register the time-aware scheduled message service
		_ = services.AddHostedService<TimeAwareScheduledMessageService>();

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
				options.Timeouts.IncludeTimeoutMetrics = true;
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
	/// Replaces the default scheduled message service with the time-aware implementation.
	/// </summary>
	/// <param name="services"> The service collection. </param>
	/// <returns> The service collection for method chaining. </returns>
	public static IServiceCollection ReplaceWithTimeAwareScheduling(this IServiceCollection services)
	{
		ArgumentNullException.ThrowIfNull(services);

		// Remove the default scheduled message service if it exists
		var serviceDescriptor = services.FirstOrDefault(static s => s.ServiceType == typeof(ScheduledMessageService));
		if (serviceDescriptor != null)
		{
			_ = services.Remove(serviceDescriptor);
		}

		// Add the time-aware implementation
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
			options.Timeouts.IncludeTimeoutMetrics = true;
		});

		_ = services.Configure<TimePolicyOptions>(static options =>
		{
			options.Observability.LogTimeoutEvents = true;
			options.Observability.IncludeTimeoutMetrics = true;
		});

		return services;
	}
}
