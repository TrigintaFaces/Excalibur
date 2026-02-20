// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics.CodeAnalysis;

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Timing;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for configuring time policy services. R7.4: Service registration for configurable timeout handling.
/// </summary>
public static class TimePolicyServiceCollectionExtensions
{
	/// <summary>
	/// Adds time policy services to the service collection with default configuration.
	/// </summary>
	/// <param name="services"> The service collection. </param>
	/// <returns> The service collection for method chaining. </returns>
	[UnconditionalSuppressMessage("Trimming", "IL2026:Members annotated with RequiresUnreferencedCode may break with trimming",
		Justification = "TimePolicyOptions types are preserved through DI registration and have well-defined properties")]
	public static IServiceCollection AddTimePolicy(this IServiceCollection services)
	{
		services.TryAddSingleton<ITimePolicy, DefaultTimePolicy>();
		services.TryAddSingleton<ITimeoutMonitor, DefaultTimeoutMonitor>();
		services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IValidateOptions<TimePolicyOptions>, TimePolicyOptionsValidator>());

		// Configure options with default values
		_ = services.AddOptions<TimePolicyOptions>()
			.Configure(static options =>
			{
				_ = options;
				// Default values are already set in TimePolicyOptions
			})
			.ValidateOnStart();

		return services;
	}

	/// <summary>
	/// Adds time policy services to the service collection with configuration binding.
	/// </summary>
	/// <param name="services"> The service collection. </param>
	/// <param name="configuration"> The configuration instance. </param>
	/// <returns> The service collection for method chaining. </returns>
	[UnconditionalSuppressMessage("Trimming", "IL2026:Members annotated with RequiresUnreferencedCode may break with trimming",
		Justification = "TimePolicyOptions types are preserved through DI registration and have well-defined properties")]
	[UnconditionalSuppressMessage("AotAnalysis", "IL3050:RequiresDynamicCode",
		Justification = "TimePolicyOptions has known serializable properties")]
	public static IServiceCollection AddTimePolicy(this IServiceCollection services, IConfiguration configuration)
	{
		ArgumentNullException.ThrowIfNull(configuration);

		services.TryAddSingleton<ITimePolicy, DefaultTimePolicy>();
		services.TryAddSingleton<ITimeoutMonitor, DefaultTimeoutMonitor>();
		services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IValidateOptions<TimePolicyOptions>, TimePolicyOptionsValidator>());

		// Bind configuration from appsettings
		_ = services.AddOptions<TimePolicyOptions>()
			.Bind(configuration.GetSection(TimePolicyOptions.SectionName))
			.ValidateOnStart();

		return services;
	}

	/// <summary>
	/// Adds time policy services to the service collection with custom configuration.
	/// </summary>
	/// <param name="services"> The service collection. </param>
	/// <param name="configureOptions"> Action to configure the time policy options. </param>
	/// <returns> The service collection for method chaining. </returns>
	[UnconditionalSuppressMessage("Trimming", "IL2026:Members annotated with RequiresUnreferencedCode may break with trimming",
		Justification = "TimePolicyOptions types are preserved through DI registration and have well-defined properties")]
	public static IServiceCollection AddTimePolicy(this IServiceCollection services, Action<TimePolicyOptions> configureOptions)
	{
		services.TryAddSingleton<ITimePolicy, DefaultTimePolicy>();
		services.TryAddSingleton<ITimeoutMonitor, DefaultTimeoutMonitor>();
		services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IValidateOptions<TimePolicyOptions>, TimePolicyOptionsValidator>());

		// Configure options using the provided action
		_ = services.AddOptions<TimePolicyOptions>()
			.Configure(configureOptions)
			.ValidateOnStart();

		return services;
	}

	/// <summary>
	/// Adds time policy services without timeout monitoring for minimal overhead scenarios.
	/// </summary>
	/// <param name="services"> The service collection. </param>
	/// <param name="configureOptions"> Optional action to configure the time policy options. </param>
	/// <returns> The service collection for method chaining. </returns>
	[UnconditionalSuppressMessage("Trimming", "IL2026:Members annotated with RequiresUnreferencedCode may break with trimming",
		Justification = "TimePolicyOptions types are preserved through DI registration and have well-defined properties")]
	public static IServiceCollection AddTimePolicyWithoutMonitoring(
		this IServiceCollection services,
		Action<TimePolicyOptions>? configureOptions = null)
	{
		services.TryAddSingleton<ITimePolicy, DefaultTimePolicy>();
		services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IValidateOptions<TimePolicyOptions>, TimePolicyOptionsValidator>());

		// Configure options
		var optionsBuilder = services.AddOptions<TimePolicyOptions>();

		if (configureOptions != null)
		{
			_ = optionsBuilder.Configure(configureOptions);
		}

		_ = optionsBuilder.ValidateOnStart();

		return services;
	}

	/// <summary>
	/// Adds adaptive timeout capabilities with enhanced monitoring.
	/// </summary>
	/// <param name="services"> The service collection. </param>
	/// <param name="configureOptions"> Action to configure adaptive timeout options. </param>
	/// <returns> The service collection for method chaining. </returns>
	[UnconditionalSuppressMessage("Trimming", "IL2026:Members annotated with RequiresUnreferencedCode may break with trimming",
		Justification = "TimePolicyOptions types are preserved through DI registration and have well-defined properties")]
	public static IServiceCollection AddAdaptiveTimeouts(
		this IServiceCollection services,
		Action<TimePolicyOptions>? configureOptions = null)
	{
		services.TryAddSingleton<ITimePolicy, DefaultTimePolicy>();
		services.TryAddSingleton<ITimeoutMonitor, DefaultTimeoutMonitor>();
		services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IValidateOptions<TimePolicyOptions>, TimePolicyOptionsValidator>());

		// Configure options with adaptive timeouts enabled
		_ = services.AddOptions<TimePolicyOptions>()
			.Configure(options =>
			{
				options.UseAdaptiveTimeouts = true;
				options.IncludeTimeoutMetrics = true;
				configureOptions?.Invoke(options);
			})
			.ValidateOnStart();

		return services;
	}

	/// <summary>
	/// Replaces the default time policy implementation with a custom one.
	/// </summary>
	/// <typeparam name="TTimePolicy"> The custom time policy implementation type. </typeparam>
	/// <param name="services"> The service collection. </param>
	/// <returns> The service collection for method chaining. </returns>
	public static IServiceCollection ReplaceTimePolicy<
	[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
	TTimePolicy>(this IServiceCollection services)
		where TTimePolicy : class, ITimePolicy
	{
		_ = services.RemoveAll<ITimePolicy>();
		_ = services.AddSingleton<ITimePolicy, TTimePolicy>();
		return services;
	}

	/// <summary>
	/// Replaces the default timeout monitor implementation with a custom one.
	/// </summary>
	/// <typeparam name="TTimeoutMonitor"> The custom timeout monitor implementation type. </typeparam>
	/// <param name="services"> The service collection. </param>
	/// <returns> The service collection for method chaining. </returns>
	public static IServiceCollection ReplaceTimeoutMonitor<
	[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
	TTimeoutMonitor>(this IServiceCollection services)
		where TTimeoutMonitor : class, ITimeoutMonitor
	{
		_ = services.RemoveAll<ITimeoutMonitor>();
		_ = services.AddSingleton<ITimeoutMonitor, TTimeoutMonitor>();
		return services;
	}
}
