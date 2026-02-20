// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics.CodeAnalysis;

using Excalibur.Dispatch.CloudNative;
using Excalibur.Dispatch.Options.Resilience;
using Excalibur.Dispatch.Resilience.Polly;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

using PollyRetryOptions = Excalibur.Dispatch.Resilience.Polly.RetryOptions;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for configuring Polly-based resilience patterns in dependency injection.
/// </summary>
public static class PollyResilienceServiceCollectionExtensions
{
	/// <summary>
	/// Adds Polly-based resilience patterns to the service collection.
	/// </summary>
	/// <param name="services"> The service collection. </param>
	/// <param name="configuration"> Optional configuration section for resilience settings. </param>
	/// <returns> The service collection for method chaining. </returns>
	[RequiresUnreferencedCode(
		"Configuration binding may reference types not preserved during trimming. Ensure options types are annotated with DynamicallyAccessedMembers.")]
	[RequiresDynamicCode(
		"Configuration binding for resilience settings requires dynamic code generation for property reflection and value conversion.")]
	public static IServiceCollection AddPollyResilience(
		this IServiceCollection services,
		IConfiguration? configuration = null)
	{
		// Core resilience services
		services.TryAddSingleton<ICircuitBreakerFactory, PollyCircuitBreakerFactory>();
		services.TryAddTransient<PollyRetryPolicyAdapter>();

		// Timeout management
		services.TryAddSingleton<ITimeoutManager, TimeoutManager>();
		if (configuration != null)
		{
			_ = services.AddOptions<TimeoutManagerOptions>()
				.Bind(configuration.GetSection("Resilience:Timeouts"))
				.ValidateDataAnnotations()
				.ValidateOnStart();
		}

		// Bulkhead management
		services.TryAddSingleton<BulkheadManager>();

		// Graceful degradation
		services.TryAddSingleton<IGracefulDegradationService, GracefulDegradationService>();
		if (configuration != null)
		{
			_ = services.AddOptions<GracefulDegradationOptions>()
				.Bind(configuration.GetSection("Resilience:GracefulDegradation"))
				.ValidateDataAnnotations()
				.ValidateOnStart();
		}

		// Distributed circuit breaker factory
		services.TryAddSingleton<DistributedCircuitBreakerFactory>();
		if (configuration != null)
		{
			_ = services.AddOptions<DistributedCircuitBreakerOptions>()
				.Bind(configuration.GetSection("Resilience:DistributedCircuitBreaker"))
				.ValidateDataAnnotations()
				.ValidateOnStart();
		}

		return services;
	}

	/// <summary>
	/// Adds a named Polly circuit breaker to the service collection.
	/// </summary>
	/// <param name="services"> The service collection. </param>
	/// <param name="name"> The name of the circuit breaker. </param>
	/// <param name="configureOptions"> Action to configure circuit breaker options. </param>
	/// <returns> The service collection for method chaining. </returns>
	[RequiresUnreferencedCode(
		"Configuration binding may reference types not preserved during trimming. Ensure options types are annotated with DynamicallyAccessedMembers.")]
	[RequiresDynamicCode(
		"Configuration binding for resilience settings requires dynamic code generation for property reflection and value conversion.")]
	public static IServiceCollection AddPollyCircuitBreaker(
		this IServiceCollection services,
		string name,
		Action<CircuitBreakerOptions>? configureOptions = null)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(name);

		_ = services.AddPollyResilience();

		_ = services.Configure<CircuitBreakerOptions>(name, options => configureOptions?.Invoke(options));
		services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IValidateOptions<CircuitBreakerOptions>, CircuitBreakerOptionsValidator>());

		return services;
	}

	/// <summary>
	/// Adds a named Polly retry policy to the service collection.
	/// </summary>
	/// <param name="services"> The service collection. </param>
	/// <param name="name"> The name of the retry policy. </param>
	/// <param name="configureOptions"> Action to configure retry options. </param>
	/// <returns> The service collection for method chaining. </returns>
	[RequiresUnreferencedCode(
		"Configuration binding may reference types not preserved during trimming. Ensure options types are annotated with DynamicallyAccessedMembers.")]
	[RequiresDynamicCode(
		"Configuration binding for resilience settings requires dynamic code generation for property reflection and value conversion.")]
	public static IServiceCollection AddPollyRetryPolicy(
		this IServiceCollection services,
		string name,
		Action<PollyRetryOptions>? configureOptions = null)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(name);

		_ = services.AddPollyResilience();

		_ = services.Configure<PollyRetryOptions>(name, options => configureOptions?.Invoke(options));

		return services;
	}

	/// <summary>
	/// Adds retry policy with jitter to the service collection.
	/// </summary>
	/// <param name="services"> The service collection. </param>
	/// <param name="name"> The name of the retry policy. </param>
	/// <param name="configureOptions"> Action to configure retry options. </param>
	/// <returns> The service collection for method chaining. </returns>
	[RequiresUnreferencedCode(
		"Configuration binding may reference types not preserved during trimming. Ensure options types are annotated with DynamicallyAccessedMembers.")]
	[RequiresDynamicCode(
		"Configuration binding for resilience settings requires dynamic code generation for property reflection and value conversion.")]
	public static IServiceCollection AddRetryPolicyWithJitter(
		this IServiceCollection services,
		string name,
		Action<PollyRetryOptions>? configureOptions = null)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(name);

		_ = services.AddPollyResilience();

		_ = services.Configure<PollyRetryOptions>(name, options =>
		{
			// Set smart defaults for jitter
			options.JitterStrategy = JitterStrategy.Equal;
			options.UseJitter = true;
			configureOptions?.Invoke(options);
		});

		// Register factory for creating retry policies
		services.TryAddTransient<RetryPolicy>();

		return services;
	}

	/// <summary>
	/// Adds bulkhead isolation to the service collection.
	/// </summary>
	/// <param name="services"> The service collection. </param>
	/// <param name="resourceName"> The name of the resource to protect with bulkhead. </param>
	/// <param name="configureOptions"> Action to configure bulkhead options. </param>
	/// <returns> The service collection for method chaining. </returns>
	[RequiresUnreferencedCode(
		"Configuration binding may reference types not preserved during trimming. Ensure options types are annotated with DynamicallyAccessedMembers.")]
	[RequiresDynamicCode(
		"Configuration binding for resilience settings requires dynamic code generation for property reflection and value conversion.")]
	public static IServiceCollection AddBulkhead(
		this IServiceCollection services,
		string resourceName,
		Action<BulkheadOptions>? configureOptions = null)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(resourceName);

		_ = services.AddPollyResilience();

		_ = services.Configure<BulkheadOptions>(resourceName, options => configureOptions?.Invoke(options));

		return services;
	}

	/// <summary>
	/// Adds distributed circuit breaker to the service collection.
	/// </summary>
	/// <param name="services"> The service collection. </param>
	/// <param name="name"> The name of the circuit breaker. </param>
	/// <param name="configureOptions"> Action to configure distributed circuit breaker options. </param>
	/// <returns> The service collection for method chaining. </returns>
	[RequiresUnreferencedCode(
		"Configuration binding may reference types not preserved during trimming. Ensure options types are annotated with DynamicallyAccessedMembers.")]
	[RequiresDynamicCode(
		"Configuration binding for resilience settings requires dynamic code generation for property reflection and value conversion.")]
	public static IServiceCollection AddDistributedCircuitBreaker(
		this IServiceCollection services,
		string name,
		Action<DistributedCircuitBreakerOptions>? configureOptions = null)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(name);

		_ = services.AddPollyResilience();

		// Ensure distributed cache is configured
		_ = services.AddDistributedMemoryCache(); // Default to in-memory, can be overridden

		_ = services.Configure<DistributedCircuitBreakerOptions>(name, options => configureOptions?.Invoke(options));

		return services;
	}

	/// <summary>
	/// Configures timeout management with custom timeouts.
	/// </summary>
	/// <param name="services"> The service collection. </param>
	/// <param name="configureOptions"> Action to configure timeout manager options. </param>
	/// <returns> The service collection for method chaining. </returns>
	[RequiresUnreferencedCode(
		"Configuration binding may reference types not preserved during trimming. Ensure options types are annotated with DynamicallyAccessedMembers.")]
	[RequiresDynamicCode(
		"Configuration binding for resilience settings requires dynamic code generation for property reflection and value conversion.")]
	public static IServiceCollection ConfigureTimeoutManager(
		this IServiceCollection services,
		Action<TimeoutManagerOptions> configureOptions)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configureOptions);

		_ = services.AddPollyResilience();
		_ = services.AddOptions<TimeoutManagerOptions>()
			.Configure(configureOptions)
			.ValidateDataAnnotations()
			.ValidateOnStart();

		return services;
	}

	/// <summary>
	/// Configures graceful degradation with custom levels and thresholds.
	/// </summary>
	/// <param name="services"> The service collection. </param>
	/// <param name="configureOptions"> Action to configure graceful degradation options. </param>
	/// <returns> The service collection for method chaining. </returns>
	[RequiresUnreferencedCode(
		"Configuration binding may reference types not preserved during trimming. Ensure options types are annotated with DynamicallyAccessedMembers.")]
	[RequiresDynamicCode(
		"Configuration binding for resilience settings requires dynamic code generation for property reflection and value conversion.")]
	public static IServiceCollection ConfigureGracefulDegradation(
		this IServiceCollection services,
		Action<GracefulDegradationOptions> configureOptions)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configureOptions);

		_ = services.AddPollyResilience();
		_ = services.AddOptions<GracefulDegradationOptions>()
			.Configure(configureOptions)
			.ValidateDataAnnotations()
			.ValidateOnStart();

		return services;
	}
}
