// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Dispatch.Abstractions;
using Excalibur.Saga;
using Excalibur.Saga.Abstractions;
using Excalibur.Saga.Idempotency;
using Excalibur.Saga.Implementation;

using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for registering advanced saga services in the dependency injection container.
/// </summary>
public static class AdvancedSagaServiceCollectionExtensions
{
	/// <summary>
	/// Adds advanced saga orchestration services to the service collection.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="configure">Optional action to configure saga options.</param>
	/// <returns>The service collection for chaining.</returns>
	/// <remarks>
	/// This registers the saga orchestrator, state store, retry policy, and middleware.
	/// Use the <see cref="AdvancedSagaBuilder"/> returned by this method's overload
	/// for advanced configuration including custom state stores and retry policies.
	/// </remarks>
	public static IServiceCollection AddDispatchAdvancedSagas(
		this IServiceCollection services,
		Action<AdvancedSagaOptions>? configure = null)
	{
		ArgumentNullException.ThrowIfNull(services);

		// Configure options
		var optionsBuilder = services.AddOptions<AdvancedSagaOptions>();
		if (configure != null)
		{
			_ = optionsBuilder.Configure(configure);
		}

		_ = optionsBuilder.ValidateDataAnnotations().ValidateOnStart();

		// Register core services
		services.TryAddSingleton<ISagaRetryPolicy, DefaultSagaRetryPolicy>();
		services.TryAddSingleton<ISagaIdempotencyProvider, InMemorySagaIdempotencyProvider>();
		services.TryAddSingleton<IDispatchMiddleware, AdvancedSagaMiddleware>();

		return services;
	}

	/// <summary>
	/// Adds advanced saga orchestration services with builder configuration.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="configure">Action to configure the saga builder.</param>
	/// <returns>The service collection for chaining.</returns>
	/// <remarks>
	/// Use this overload to configure custom state stores, retry policies,
	/// and other advanced saga settings through the fluent builder.
	/// </remarks>
	public static IServiceCollection AddDispatchAdvancedSagas(
		this IServiceCollection services,
		Action<AdvancedSagaBuilder> configure)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configure);

		var options = new AdvancedSagaOptions();
		var builder = new AdvancedSagaBuilder(services, options);

		configure(builder);

		// Register configured options
		_ = services.AddOptions<AdvancedSagaOptions>()
			.Configure(opt =>
			{
				opt.DefaultTimeout = options.DefaultTimeout;
				opt.DefaultStepTimeout = options.DefaultStepTimeout;
				opt.MaxRetryAttempts = options.MaxRetryAttempts;
				opt.RetryBaseDelay = options.RetryBaseDelay;
				opt.EnableAutoCompensation = options.EnableAutoCompensation;
				opt.EnableStatePersistence = options.EnableStatePersistence;
				opt.MaxDegreeOfParallelism = options.MaxDegreeOfParallelism;
				opt.EnableMetrics = options.EnableMetrics;
				opt.CleanupInterval = options.CleanupInterval;
				opt.CompletedSagaRetention = options.CompletedSagaRetention;
			})
			.ValidateDataAnnotations()
			.ValidateOnStart();

		// Register core services with fallbacks
		services.TryAddSingleton<ISagaRetryPolicy, DefaultSagaRetryPolicy>();
		services.TryAddSingleton<ISagaIdempotencyProvider, InMemorySagaIdempotencyProvider>();
		services.TryAddSingleton<IDispatchMiddleware, AdvancedSagaMiddleware>();

		return services;
	}
}
