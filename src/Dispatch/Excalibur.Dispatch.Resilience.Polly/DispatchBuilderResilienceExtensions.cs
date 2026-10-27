// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0


using System.Diagnostics.CodeAnalysis;

using Excalibur.Dispatch.Configuration;
using Excalibur.Dispatch.Resilience;
using Excalibur.Dispatch.Resilience.Polly;

using Microsoft.Extensions.DependencyInjection.Extensions;

using Polly;

using BackoffStrategy = Excalibur.Dispatch.Resilience.BackoffStrategy;
using CircuitBreakerOptions = Excalibur.Dispatch.Options.Resilience.CircuitBreakerOptions;
using ITransportCircuitBreakerRegistry = Excalibur.Dispatch.Resilience.ITransportCircuitBreakerRegistry;

namespace Microsoft.Extensions.DependencyInjection;

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
	private static IDispatchBuilder UseResilienceCore(this IDispatchBuilder builder)
	{
		ArgumentNullException.ThrowIfNull(builder);

		// Add Polly resilience services
		_ = builder.Services.AddPollyResilience();

		// Register resilience middleware if needed builder.Use<ResilienceMiddleware>();
		return builder;
	}

	/// <summary>
	/// Adds Dispatch resilience (retry, circuit breaker, bulkhead, timeout) via the builder.
	/// </summary>
	/// <param name="builder">The dispatch builder.</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <exception cref="ArgumentNullException">
	/// Thrown when <paramref name="builder"/> is null.
	/// </exception>
	/// <remarks>
	/// Register named policies through their subsystem configuration, then resolve and execute them at
	/// the dependency boundary. Registering services does not install retry middleware:
	/// <code>
	/// services.AddDispatch(dispatch =&gt; dispatch.UseResilience());
	/// services.AddPollyRetryPolicy("orders", o =&gt; o.MaxRetryAttempts = 3);
	/// services.AddPollyCircuitBreaker("orders-circuit", o =&gt; o.MinimumThroughput = 5);
	/// </code>
	/// </remarks>
	[RequiresUnreferencedCode(
		"Configuration binding may reference types not preserved during trimming. Ensure resilience options models are annotated accordingly.")]
	[RequiresDynamicCode(
		"Configuration binding for resilience settings requires dynamic code generation for property reflection and value conversion.")]
	public static IDispatchBuilder UseResilience(this IDispatchBuilder builder) => builder.UseResilienceCore();

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
	public static IDispatchBuilder UsePollyResilienceAdapters(
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
			var retryOpts = options.RetryOptions ?? new PollyRetryOptions();
			var logger = sp.GetService<Microsoft.Extensions.Logging.ILogger<PollyRetryPolicyAdapter>>();
			return new PollyRetryPolicyAdapter(Microsoft.Extensions.Options.Options.Create(retryOpts), logger);
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
			var retryOpts = options.RetryOptions ?? new PollyRetryOptions();
			return new PollyBackoffCalculatorAdapter(
				// ONE-TO-ONE AND EXHAUSTIVE. BackoffStrategy names the same three growth laws as Polly's
				// DelayBackoffType, so this mapping is total and has no default arm. That is the point:
				// a `_ =>` here previously absorbed four strategy members and silently computed
				// exponential for all of them, so a consumer who configured one of those got a curve
				// they did not select, with no error and no log. With no default, adding a member to
				// BackoffStrategy is a COMPILE ERROR here until somebody decides what it maps to, which
				// is where that decision belongs.
				retryOpts.BackoffStrategy switch
				{
					BackoffStrategy.Constant => DelayBackoffType.Constant,
					BackoffStrategy.Linear => DelayBackoffType.Linear,
					BackoffStrategy.Exponential => DelayBackoffType.Exponential,
					_ => throw new ArgumentOutOfRangeException(
						nameof(configureOptions),
						retryOpts.BackoffStrategy,
						Resources.DispatchBuilderResilienceExtensions_UnknownBackoffStrategy),
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
