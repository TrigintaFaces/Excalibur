// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Saga;
using Excalibur.Saga.DependencyInjection;
using Excalibur.Saga.Orchestration;
using Excalibur.Saga.Outbox;

using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods on <see cref="ISagaBuilder"/> for configuring saga sub-features.
/// </summary>
public static class SagaBuilderExtensions
{
	/// <summary>
	/// Adds advanced saga orchestration services including state management, retry policies,
	/// and the <see cref="AdvancedSagaMiddleware"/>.
	/// </summary>
	/// <param name="builder">The saga builder.</param>
	/// <param name="configure">Optional action to configure advanced saga options.</param>
	/// <returns>The saga builder for chaining.</returns>
	/// <remarks>
	/// This is equivalent to calling <see cref="AdvancedSagaServiceCollectionExtensions.AddDispatchAdvancedSagas(IServiceCollection, Action{AdvancedSagaOptions}?)"/>
	/// but integrated into the builder pattern for discoverability.
	/// </remarks>
	public static ISagaBuilder WithOrchestration(
		this ISagaBuilder builder,
		Action<AdvancedSagaOptions>? configure = null)
	{
		ArgumentNullException.ThrowIfNull(builder);

		_ = builder.Services.AddDispatchAdvancedSagas(configure);

		return builder;
	}

	/// <summary>
	/// Adds advanced saga orchestration services with fluent builder configuration.
	/// </summary>
	/// <param name="builder">The saga builder.</param>
	/// <param name="configure">Action to configure the advanced saga builder.</param>
	/// <returns>The saga builder for chaining.</returns>
	public static ISagaBuilder WithOrchestration(
		this ISagaBuilder builder,
		Action<AdvancedSagaBuilder> configure)
	{
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentNullException.ThrowIfNull(configure);

		_ = builder.Services.AddDispatchAdvancedSagas(configure);

		return builder;
	}

	/// <summary>
	/// Adds saga event coordination services including the in-memory saga store,
	/// saga coordinator, and the <see cref="SagaHandlingMiddleware"/>.
	/// </summary>
	/// <param name="builder">The saga builder.</param>
	/// <returns>The saga builder for chaining.</returns>
	/// <remarks>
	/// <para>
	/// This is equivalent to calling <see cref="DispatchOrchestrationExtensions.AddDispatchOrchestration"/>
	/// but integrated into the builder pattern for discoverability.
	/// </para>
	/// <para>
	/// The coordination middleware runs at the <see cref="Excalibur.Dispatch.Abstractions.DispatchMiddlewareStage.End"/>
	/// stage and processes <see cref="Excalibur.Saga.Orchestration.ISagaEvent"/> messages
	/// through the <see cref="SagaCoordinator"/>.
	/// </para>
	/// </remarks>
	public static ISagaBuilder WithCoordination(this ISagaBuilder builder)
	{
		ArgumentNullException.ThrowIfNull(builder);

		_ = builder.Services.AddDispatchOrchestration();

		return builder;
	}

	/// <summary>
	/// Adds saga timeout delivery services with in-memory storage.
	/// </summary>
	/// <param name="builder">The saga builder.</param>
	/// <param name="configure">Optional action to configure timeout options.</param>
	/// <returns>The saga builder for chaining.</returns>
	/// <remarks>
	/// This registers the <see cref="Excalibur.Saga.Storage.InMemorySagaTimeoutStore"/> as the default
	/// timeout store. For production use, configure a durable store (e.g., SQL Server) separately.
	/// </remarks>
	public static ISagaBuilder WithTimeouts(
		this ISagaBuilder builder,
		Action<SagaTimeoutOptions>? configure = null)
	{
		ArgumentNullException.ThrowIfNull(builder);

		if (configure is not null)
		{
			_ = builder.Services.AddSagaTimeoutDelivery(configure);
		}
		else
		{
			_ = builder.Services.AddSagaTimeoutDelivery();
		}

		return builder;
	}

	/// <summary>
	/// Adds saga telemetry instrumentation for OpenTelemetry metrics and tracing.
	/// </summary>
	/// <param name="builder">The saga builder.</param>
	/// <returns>The saga builder for chaining.</returns>
	/// <remarks>
	/// <para>
	/// Consumers still need to configure OpenTelemetry SDK to collect the metrics and traces:
	/// </para>
	/// <code>
	/// services.AddOpenTelemetry()
	///     .WithMetrics(m => m.AddMeter("Excalibur.Dispatch.Sagas"))
	///     .WithTracing(t => t.AddSource("Excalibur.Dispatch.Sagas"));
	/// </code>
	/// </remarks>
	public static ISagaBuilder WithInstrumentation(this ISagaBuilder builder)
	{
		ArgumentNullException.ThrowIfNull(builder);

		_ = builder.Services.AddSagaInstrumentation();

		return builder;
	}

	/// <summary>
	/// Adds saga outbox integration for reliable event publishing.
	/// </summary>
	/// <param name="builder">The saga builder.</param>
	/// <param name="configure">Optional action to configure outbox options including the publish delegate.</param>
	/// <returns>The saga builder for chaining.</returns>
	/// <remarks>
	/// <para>
	/// The outbox mediator ensures that events produced by saga steps are published
	/// reliably through a host-configured outbox implementation.
	/// </para>
	/// <para>
	/// The host must configure the <see cref="SagaOutboxOptions.PublishDelegate"/>
	/// to integrate with their chosen outbox store.
	/// </para>
	/// </remarks>
	/// <example>
	/// <code>
	/// services.AddExcaliburSaga(saga => saga
	///     .WithOutbox(options =>
	///     {
	///         options.PublishDelegate = async (events, sagaId, ct) =>
	///         {
	///             // publish events through outbox store
	///         };
	///     }));
	/// </code>
	/// </example>
	public static ISagaBuilder WithOutbox(
		this ISagaBuilder builder,
		Action<SagaOutboxOptions>? configure = null)
	{
		ArgumentNullException.ThrowIfNull(builder);

		builder.Services.TryAddSingleton<ISagaOutboxMediator, SagaOutboxMediator>();

		if (configure is not null)
		{
			_ = builder.Services.Configure(configure);
		}

		return builder;
	}
}
