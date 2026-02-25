// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics.CodeAnalysis;

using Excalibur.Dispatch.Abstractions.Configuration;
using Excalibur.Dispatch.Options.Resilience;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

using Polly;

namespace Excalibur.Dispatch.Resilience.Polly;

/// <summary>
/// Extension methods for adding resilience to IDispatchBuilder.
/// </summary>
public static class DispatchBuilderResilienceExtensions
{
	/// <summary>
	/// Adds Dispatch resilience patterns to the dispatcher.
	/// </summary>
	/// <param name="builder"> The dispatch builder. </param>
	/// <returns> The dispatch builder for method chaining. </returns>
	[RequiresUnreferencedCode(
		"Configuration binding may reference types not preserved during trimming. Ensure resilience options models are annotated accordingly.")]
	[RequiresDynamicCode(
		"Configuration binding for resilience settings requires dynamic code generation for property reflection and value conversion.")]
	public static IDispatchBuilder AddDispatchResilience(this IDispatchBuilder builder)
	{
		ArgumentNullException.ThrowIfNull(builder);

		// Add Polly resilience services
		_ = builder.Services.AddPollyResilience();

		// Register resilience middleware if needed builder.Use<ResilienceMiddleware>();
		return builder;
	}

	/// <summary>
	/// Adds Dispatch resilience patterns with configuration.
	/// </summary>
	/// <param name="builder"> The dispatch builder. </param>
	/// <param name="configureOptions"> Action to configure resilience options. </param>
	/// <returns> The dispatch builder for method chaining. </returns>
	[RequiresUnreferencedCode(
		"Configuration binding may reference types not preserved during trimming. Ensure resilience options models are annotated accordingly.")]
	[RequiresDynamicCode(
		"Configuration binding for resilience settings requires dynamic code generation for property reflection and value conversion.")]
	public static IDispatchBuilder AddDispatchResilience(
		this IDispatchBuilder builder,
		Action<ResilienceOptions> configureOptions)
	{
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentNullException.ThrowIfNull(configureOptions);

		// Add basic resilience
		_ = builder.AddDispatchResilience();

		// Configure options
		_ = builder.Services.Configure(configureOptions);

		return builder;
	}

	/// <summary>
	/// Adds Dispatch resilience (retry, circuit breaker, bulkhead, timeout) via the builder.
	/// </summary>
	/// <param name="builder">The dispatch builder.</param>
	/// <param name="configure">Optional action to configure resilience options.</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <exception cref="ArgumentNullException">
	/// Thrown when <paramref name="builder"/> is null.
	/// </exception>
	/// <example>
	/// <code>
	/// services.AddDispatch(dispatch =>
	/// {
	///     dispatch.AddResilience(res => res.DefaultRetryCount = 3);
	/// });
	/// </code>
	/// </example>
	[RequiresUnreferencedCode(
		"Configuration binding may reference types not preserved during trimming. Ensure resilience options models are annotated accordingly.")]
	[RequiresDynamicCode(
		"Configuration binding for resilience settings requires dynamic code generation for property reflection and value conversion.")]
	public static IDispatchBuilder AddResilience(
		this IDispatchBuilder builder,
		Action<ResilienceOptions>? configure = null)
	{
		ArgumentNullException.ThrowIfNull(builder);

		if (configure != null)
		{
			return builder.AddDispatchResilience(configure);
		}

		return builder.AddDispatchResilience();
	}

	/// <summary>
	/// Adds Polly-based adapters for all resilience interfaces.
	/// </summary>
	/// <remarks>
	/// <para>
	/// This method cleanly replaces all default resilience implementations with Polly-based adapters:
	/// </para>
	/// <list type="bullet">
	///   <item><see cref="IRetryPolicy"/> → <see cref="PollyRetryPolicyAdapter"/></item>
	///   <item><see cref="ICircuitBreakerPolicy"/> → <see cref="PollyCircuitBreakerPolicyAdapter"/></item>
	///   <item><see cref="IBackoffCalculator"/> → <see cref="PollyBackoffCalculatorAdapter"/></item>
	///   <item><see cref="ITransportCircuitBreakerRegistry"/> → <see cref="PollyTransportCircuitBreakerRegistry"/></item>
	/// </list>
	/// <para>
	/// Consumers who want to use Polly's advanced features (decorrelated jitter, advanced circuit breaker strategies,
	/// telemetry hooks) can call this method to opt-in to Polly implementations.
	/// </para>
	/// </remarks>
	/// <param name="builder">The dispatch builder.</param>
	/// <param name="configureOptions">Optional action to configure Polly resilience options.</param>
	/// <returns>The dispatch builder for method chaining.</returns>
	public static IDispatchBuilder AddPollyResilienceAdapters(
		this IDispatchBuilder builder,
		Action<PollyResilienceAdapterOptions>? configureOptions = null)
	{
		ArgumentNullException.ThrowIfNull(builder);

		_ = builder.Services.AddPollyResilienceAdapters(configureOptions);

		return builder;
	}

	/// <summary>
	/// Adds Polly-based adapters for all resilience interfaces to the service collection.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="configureOptions">Optional action to configure Polly resilience options.</param>
	/// <returns>The service collection for method chaining.</returns>
	public static IServiceCollection AddPollyResilienceAdapters(
		this IServiceCollection services,
		Action<PollyResilienceAdapterOptions>? configureOptions = null)
	{
		ArgumentNullException.ThrowIfNull(services);

		var options = new PollyResilienceAdapterOptions();
		configureOptions?.Invoke(options);

		// Replace all default implementations with Polly adapters
		_ = services.RemoveAll<IRetryPolicy>();
		_ = services.AddSingleton<IRetryPolicy>(sp =>
		{
			var retryOpts = options.RetryOptions ?? new RetryOptions();
			var logger = sp.GetService<Microsoft.Extensions.Logging.ILogger<PollyRetryPolicyAdapter>>();
			return new PollyRetryPolicyAdapter(retryOpts, logger);
		});

		_ = services.RemoveAll<ICircuitBreakerPolicy>();
		_ = services.AddSingleton<ICircuitBreakerPolicy>(sp =>
		{
			var cbOpts = options.CircuitBreakerOptions ?? new CircuitBreakerOptions();
			var logger = sp.GetService<Microsoft.Extensions.Logging.ILogger<PollyCircuitBreakerPolicyAdapter>>();
			return new PollyCircuitBreakerPolicyAdapter(cbOpts, "default", logger);
		});

		_ = services.RemoveAll<IBackoffCalculator>();
		_ = services.AddSingleton<IBackoffCalculator>(sp =>
		{
			var retryOpts = options.RetryOptions ?? new RetryOptions();
			return new PollyBackoffCalculatorAdapter(
				retryOpts.BackoffStrategy switch
				{
					BackoffStrategy.Fixed => DelayBackoffType.Constant,
					BackoffStrategy.Linear => DelayBackoffType.Linear,
					BackoffStrategy.Exponential => DelayBackoffType.Exponential,
					_ => DelayBackoffType.Exponential,
				},
				retryOpts.BaseDelay,
				options.MaxBackoffDelay,
				retryOpts.UseJitter);
		});

		_ = services.RemoveAll<ITransportCircuitBreakerRegistry>();
		_ = services.AddSingleton<ITransportCircuitBreakerRegistry, PollyTransportCircuitBreakerRegistry>();

		return services;
	}
}
