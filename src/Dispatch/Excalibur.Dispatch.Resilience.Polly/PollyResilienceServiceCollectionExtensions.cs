// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Microsoft.Extensions.Logging;
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
	[UnconditionalSuppressMessage("AOT", "IL2026:RequiresUnreferencedCode",
		Justification = "Options binding uses reflection by design. AOT consumers should use source-generated alternatives.")]
	[UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCode",
		Justification = "Options binding uses reflection by design. AOT consumers should use source-generated alternatives.")]
	public static IServiceCollection AddPollyResilience(
		this IServiceCollection services,
		IConfiguration? configuration = null)
	{
		// Core resilience services
		services.TryAddTransient<PollyRetryPolicyAdapter>();
		_ = services.AddOptions<PollyRetryOptions>().ValidateOnStart();
		services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IValidateOptions<PollyRetryOptions>, RetryOptionsValidator>());

		// Register validators
		services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IValidateOptions<BulkheadOptions>, BulkheadOptionsValidator>());
		services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IValidateOptions<DistributedCircuitBreakerOptions>, DistributedCircuitBreakerOptionsValidator>());
		services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IValidateOptions<TimeoutManagerOptions>, TimeoutManagerOptionsValidator>());
		services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IValidateOptions<GracefulDegradationOptions>, GracefulDegradationOptionsValidator>());

		// Timeout management
		services.TryAddSingleton<ITimeoutManager, TimeoutManager>();
		var timeoutOptions = services.AddOptions<TimeoutManagerOptions>().ValidateOnStart();
		if (configuration != null)
		{
			_ = timeoutOptions.Bind(configuration.GetSection("Resilience:Timeouts"));
		}

		// Bulkhead management
		services.TryAddSingleton<BulkheadManager>();

		// Graceful degradation
		services.TryAddSingleton<IGracefulDegradationService>(sp => new GracefulDegradationService(
			sp.GetRequiredService<IOptions<GracefulDegradationOptions>>(),
			sp.GetRequiredService<ILogger<GracefulDegradationService>>(),
			sp.GetService<TimeProvider>()));
		var gracefulDegradationOptions = services.AddOptions<GracefulDegradationOptions>().ValidateOnStart();
		if (configuration != null)
		{
			_ = gracefulDegradationOptions.Bind(configuration.GetSection("Resilience:GracefulDegradation"));
		}

		// Distributed circuit breaker options. The factory itself is NOT registered here: it requires an
		// IDistributedCache and this path guarantees none, so registering it left a service in the container
		// that could not be constructed. It is seated by AddDistributedCircuitBreaker, beside the cache.
		var distributedCircuitBreakerOptions = services.AddOptions<DistributedCircuitBreakerOptions>().ValidateOnStart();
		if (configuration != null)
		{
			_ = distributedCircuitBreakerOptions.Bind(configuration.GetSection("Resilience:DistributedCircuitBreaker"));
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
	[UnconditionalSuppressMessage("AOT", "IL2026:RequiresUnreferencedCode",
		Justification = "Delegates to AddPollyResilience which is already annotated with RequiresUnreferencedCode.")]
	[UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCode",
		Justification = "Delegates to AddPollyResilience which is already annotated with RequiresDynamicCode.")]
	public static IServiceCollection AddPollyCircuitBreaker(
		this IServiceCollection services,
		string name,
		Action<CircuitBreakerOptions>? configureOptions = null)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(name);

		_ = services.AddPollyResilience();

		_ = services.AddOptions<CircuitBreakerOptions>(name).Configure(options => configureOptions?.Invoke(options)).ValidateOnStart();
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
	[UnconditionalSuppressMessage("AOT", "IL2026:RequiresUnreferencedCode",
		Justification = "Delegates to AddPollyResilience which is already annotated with RequiresUnreferencedCode.")]
	[UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCode",
		Justification = "Delegates to AddPollyResilience which is already annotated with RequiresDynamicCode.")]
	public static IServiceCollection AddPollyRetryPolicy(
		this IServiceCollection services,
		string name,
		Action<PollyRetryOptions>? configureOptions = null)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(name);

		_ = services.AddPollyResilience();

		_ = services.AddOptions<PollyRetryOptions>(name)
			.Configure(options => configureOptions?.Invoke(options))
			.ValidateOnStart();
		services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IValidateOptions<PollyRetryOptions>, RetryOptionsValidator>());

		return services;
	}

	/// <summary>
	/// Adds retry policy with jitter to the service collection.
	/// </summary>
	/// <param name="services"> The service collection. </param>
	/// <param name="name"> The name of the retry policy. </param>
	/// <param name="configureOptions"> Action to configure retry options. </param>
	/// <returns> The service collection for method chaining. </returns>
	[UnconditionalSuppressMessage("AOT", "IL2026:RequiresUnreferencedCode",
		Justification = "Delegates to AddPollyResilience which is already annotated with RequiresUnreferencedCode.")]
	[UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCode",
		Justification = "Delegates to AddPollyResilience which is already annotated with RequiresDynamicCode.")]
	public static IServiceCollection AddRetryPolicyWithJitter(
		this IServiceCollection services,
		string name,
		Action<PollyRetryOptions>? configureOptions = null)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(name);

		_ = services.AddPollyResilience();

		_ = services.AddOptions<PollyRetryOptions>(name)
			.Configure(options =>
			{
				// Set smart defaults for jitter
				options.JitterStrategy = JitterStrategy.Equal;
				options.UseJitter = true;
				configureOptions?.Invoke(options);
			})
			.ValidateOnStart();
		services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IValidateOptions<PollyRetryOptions>, RetryOptionsValidator>());

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
	[UnconditionalSuppressMessage("AOT", "IL2026:RequiresUnreferencedCode",
		Justification = "Delegates to AddPollyResilience which is already annotated with RequiresUnreferencedCode.")]
	[UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCode",
		Justification = "Delegates to AddPollyResilience which is already annotated with RequiresDynamicCode.")]
	public static IServiceCollection AddBulkhead(
		this IServiceCollection services,
		string resourceName,
		Action<BulkheadOptions>? configureOptions = null)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(resourceName);

		_ = services.AddPollyResilience();

		_ = services.AddOptions<BulkheadOptions>(resourceName).Configure(options => configureOptions?.Invoke(options)).ValidateOnStart();

		return services;
	}

	/// <summary>
	/// Adds a named circuit breaker whose open/half-open/closed state is shared with every instance
	/// configured against the same store and name.
	/// </summary>
	/// <remarks>
	/// <para>
	/// Requires an <see cref="Microsoft.Extensions.Caching.Distributed.IDistributedCache"/> that is
	/// genuinely shared across instances, registered by the caller — for example
	/// <c>AddStackExchangeRedisCache(...)</c> or <c>AddDistributedSqlServerCache(...)</c>. This method
	/// deliberately does not supply one: the in-process default would leave every replica tripping its
	/// own circuit while the registration claimed otherwise. A host that starts without a shared store,
	/// or with the in-process one, fails at startup naming the remedy rather than degrading silently.
	/// For a per-instance breaker, use <see cref="AddPollyCircuitBreaker"/>.
	/// </para>
	/// <para>
	/// Resolve the breaker as a keyed service:
	/// <c>serviceProvider.GetRequiredKeyedService&lt;IDistributedCircuitBreaker&gt;(name)</c>.
	/// </para>
	/// </remarks>
	/// <param name="services"> The service collection. </param>
	/// <param name="name"> The name of the circuit breaker, and the service key it is resolved by. </param>
	/// <param name="configureOptions"> Action to configure distributed circuit breaker options. </param>
	/// <returns> The service collection for method chaining. </returns>
	[UnconditionalSuppressMessage("AOT", "IL2026:RequiresUnreferencedCode",
		Justification = "Delegates to AddPollyResilience which is already annotated with RequiresUnreferencedCode.")]
	[UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCode",
		Justification = "Delegates to AddPollyResilience which is already annotated with RequiresDynamicCode.")]
	public static IServiceCollection AddDistributedCircuitBreaker(
		this IServiceCollection services,
		string name,
		Action<DistributedCircuitBreakerOptions>? configureOptions = null)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(name);

		_ = services.AddPollyResilience();

		// No cache is registered here on purpose. Seating AddDistributedMemoryCache() as a default handed
		// a consumer who overrode nothing a per-instance breaker under a distributed name, and nothing
		// about that failure was observable at runtime. The guard below refuses that composition at boot.
		services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IValidateOptions<DistributedCircuitBreakerCacheGuardOptions>, DistributedCircuitBreakerCacheGuard>());
		_ = services.AddOptions<DistributedCircuitBreakerCacheGuardOptions>().ValidateOnStart();

		services.TryAddSingleton<DistributedCircuitBreakerFactory>();

		_ = services.AddOptions<DistributedCircuitBreakerOptions>(name)
			.Configure(options => configureOptions?.Invoke(options))
			.ValidateOnStart();

		// Without this the method registered an internal factory a consumer cannot resolve and named
		// options nothing reads — the breaker it configured was unreachable. The key is the breaker name.
		services.TryAddKeyedSingleton<IDistributedCircuitBreaker>(
			name,
			static (sp, key) => sp.GetRequiredService<DistributedCircuitBreakerFactory>().GetOrCreate((string)key!));

		return services;
	}

	/// <summary>
	/// Configures timeout management with custom timeouts.
	/// </summary>
	/// <param name="services"> The service collection. </param>
	/// <param name="configureOptions"> Action to configure timeout manager options. </param>
	/// <returns> The service collection for method chaining. </returns>
	[UnconditionalSuppressMessage("AOT", "IL2026:RequiresUnreferencedCode",
		Justification = "Delegates to AddPollyResilience which is already annotated with RequiresUnreferencedCode.")]
	[UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCode",
		Justification = "Delegates to AddPollyResilience which is already annotated with RequiresDynamicCode.")]
	public static IServiceCollection ConfigureTimeoutManager(
		this IServiceCollection services,
		Action<TimeoutManagerOptions> configureOptions)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configureOptions);

		_ = services.AddPollyResilience();
		_ = services.AddOptions<TimeoutManagerOptions>()
			.Configure(configureOptions)
			.ValidateOnStart();

		return services;
	}

	/// <summary>
	/// Configures graceful degradation with custom levels and thresholds.
	/// </summary>
	/// <param name="services"> The service collection. </param>
	/// <param name="configureOptions"> Action to configure graceful degradation options. </param>
	/// <returns> The service collection for method chaining. </returns>
	[UnconditionalSuppressMessage("AOT", "IL2026:RequiresUnreferencedCode",
		Justification = "Delegates to AddPollyResilience which is already annotated with RequiresUnreferencedCode.")]
	[UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCode",
		Justification = "Delegates to AddPollyResilience which is already annotated with RequiresDynamicCode.")]
	public static IServiceCollection ConfigureGracefulDegradation(
		this IServiceCollection services,
		Action<GracefulDegradationOptions> configureOptions)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configureOptions);

		_ = services.AddPollyResilience();
		_ = services.AddOptions<GracefulDegradationOptions>()
			.Configure(configureOptions)
			.ValidateOnStart();

		return services;
	}
}
