// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Saga.Correlation;
using Excalibur.Saga.Handlers;
using Excalibur.Saga.Hosting;
using Excalibur.Saga.Idempotency;
using Excalibur.Saga.Inspection;
using Excalibur.Saga.Reminders;
using Excalibur.Saga.Snapshots;

using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for registering Sprint 572 saga enhancement services.
/// </summary>
public static class SagaEnhancementsServiceCollectionExtensions
{
	/// <summary>
	/// Adds the convention-based saga message correlator for automatic message-to-saga routing.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <returns>The service collection for chaining.</returns>
	public static IServiceCollection AddSagaCorrelation(this IServiceCollection services)
	{
		ArgumentNullException.ThrowIfNull(services);

		services.TryAddSingleton<ConventionBasedCorrelator>();

		return services;
	}

	/// <summary>
	/// Adds the default logging-based saga not-found handler for a specific saga type.
	/// </summary>
	/// <typeparam name="TSaga">The saga type to register the handler for.</typeparam>
	/// <param name="services">The service collection.</param>
	/// <returns>The service collection for chaining.</returns>
	public static IServiceCollection AddSagaNotFoundHandler<TSaga>(this IServiceCollection services)
		where TSaga : class
	{
		ArgumentNullException.ThrowIfNull(services);

		services.TryAddSingleton<ISagaNotFoundHandler<TSaga>, LoggingNotFoundHandler<TSaga>>();

		return services;
	}

	/// <summary>
	/// Adds a custom saga not-found handler for a specific saga type.
	/// </summary>
	/// <typeparam name="TSaga">The saga type to register the handler for.</typeparam>
	/// <typeparam name="THandler">The handler implementation type.</typeparam>
	/// <param name="services">The service collection.</param>
	/// <returns>The service collection for chaining.</returns>
	public static IServiceCollection AddSagaNotFoundHandler<TSaga, THandler>(this IServiceCollection services)
		where TSaga : class
		where THandler : class, ISagaNotFoundHandler<TSaga>
	{
		ArgumentNullException.ThrowIfNull(services);

		services.TryAddSingleton<ISagaNotFoundHandler<TSaga>, THandler>();

		return services;
	}

	/// <summary>
	/// Adds the saga state inspection service backed by the registered state store.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <returns>The service collection for chaining.</returns>
	public static IServiceCollection AddSagaStateInspection(this IServiceCollection services)
	{
		ArgumentNullException.ThrowIfNull(services);

		services.TryAddSingleton<ISagaStateInspector, InMemorySagaStateInspector>();

		return services;
	}

	/// <summary>
	/// Adds saga reminder services with default options.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <returns>The service collection for chaining.</returns>
	public static IServiceCollection AddSagaReminders(this IServiceCollection services)
	{
		return services.AddSagaReminders(_ => { });
	}

	/// <summary>
	/// Adds saga reminder services with the specified configuration.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="configure">An action to configure reminder options.</param>
	/// <returns>The service collection for chaining.</returns>
	public static IServiceCollection AddSagaReminders(
		this IServiceCollection services,
		Action<SagaReminderOptions> configure)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configure);

		_ = services.AddOptions<SagaReminderOptions>()
			.Configure(configure)
			.ValidateDataAnnotations()
			.ValidateOnStart();

		return services;
	}

	/// <summary>
	/// Adds saga state snapshot services with default options.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <returns>The service collection for chaining.</returns>
	public static IServiceCollection AddSagaSnapshots(this IServiceCollection services)
	{
		return services.AddSagaSnapshots(_ => { });
	}

	/// <summary>
	/// Adds saga state snapshot services with the specified configuration.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="configure">An action to configure snapshot options.</param>
	/// <returns>The service collection for chaining.</returns>
	public static IServiceCollection AddSagaSnapshots(
		this IServiceCollection services,
		Action<SagaSnapshotOptions> configure)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configure);

		_ = services.AddOptions<SagaSnapshotOptions>()
			.Configure(configure)
			.ValidateDataAnnotations()
			.ValidateOnStart();

		return services;
	}

	/// <summary>
	/// Adds saga idempotency tracking services.
	/// </summary>
	/// <typeparam name="TProvider">The idempotency provider implementation type.</typeparam>
	/// <param name="services">The service collection.</param>
	/// <returns>The service collection for chaining.</returns>
	public static IServiceCollection AddSagaIdempotency<TProvider>(this IServiceCollection services)
		where TProvider : class, ISagaIdempotencyProvider
	{
		ArgumentNullException.ThrowIfNull(services);

		services.TryAddSingleton<ISagaIdempotencyProvider, TProvider>();

		return services;
	}

	/// <summary>
	/// Adds the saga timeout cleanup background service with default options.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <returns>The service collection for chaining.</returns>
	public static IServiceCollection AddSagaTimeoutCleanup(this IServiceCollection services)
	{
		return services.AddSagaTimeoutCleanup(_ => { });
	}

	/// <summary>
	/// Adds the saga timeout cleanup background service with the specified configuration.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="configure">An action to configure cleanup options.</param>
	/// <returns>The service collection for chaining.</returns>
	public static IServiceCollection AddSagaTimeoutCleanup(
		this IServiceCollection services,
		Action<SagaTimeoutCleanupOptions> configure)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configure);

		_ = services.AddOptions<SagaTimeoutCleanupOptions>()
			.Configure(configure)
			.ValidateDataAnnotations()
			.ValidateOnStart();

		_ = services.AddHostedService<SagaTimeoutCleanupService>();

		return services;
	}
}
