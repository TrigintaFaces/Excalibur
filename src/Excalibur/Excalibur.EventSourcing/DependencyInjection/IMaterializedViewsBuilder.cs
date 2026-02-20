// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics.CodeAnalysis;

using Excalibur.EventSourcing.Abstractions;

using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.EventSourcing.DependencyInjection;

/// <summary>
/// Fluent builder interface for configuring materialized view services.
/// </summary>
/// <remarks>
/// <para>
/// This interface follows the Microsoft-style fluent builder pattern for registering
/// materialized view builders, stores, and processors.
/// </para>
/// <para>
/// All methods return <c>this</c> for method chaining, enabling a fluent configuration experience.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// services.AddMaterializedViews(builder =>
/// {
///     builder.AddBuilder&lt;OrderSummaryView, OrderSummaryViewBuilder&gt;()
///            .AddBuilder&lt;CustomerStatsView, CustomerStatsViewBuilder&gt;()
///            .UseStore&lt;SqlServerMaterializedViewStore&gt;();
/// });
/// </code>
/// </example>
public interface IMaterializedViewsBuilder
{
	/// <summary>
	/// Gets the service collection being configured.
	/// </summary>
	/// <value>The <see cref="IServiceCollection"/>.</value>
	IServiceCollection Services { get; }

	/// <summary>
	/// Registers a materialized view builder.
	/// </summary>
	/// <typeparam name="TView">The view type.</typeparam>
	/// <typeparam name="TBuilder">The builder implementation type.</typeparam>
	/// <returns>The builder for fluent configuration.</returns>
	IMaterializedViewsBuilder AddBuilder<
		TView,
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TBuilder>()
		where TView : class, new()
		where TBuilder : class, IMaterializedViewBuilder<TView>;

	/// <summary>
	/// Registers a materialized view builder with a factory function.
	/// </summary>
	/// <typeparam name="TView">The view type.</typeparam>
	/// <param name="builderFactory">Factory function to create the builder.</param>
	/// <returns>The builder for fluent configuration.</returns>
	IMaterializedViewsBuilder AddBuilder<TView>(
		Func<IServiceProvider, IMaterializedViewBuilder<TView>> builderFactory)
		where TView : class, new();

	/// <summary>
	/// Configures the materialized view store implementation.
	/// </summary>
	/// <typeparam name="TStore">The store implementation type.</typeparam>
	/// <returns>The builder for fluent configuration.</returns>
	IMaterializedViewsBuilder UseStore<
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TStore>()
		where TStore : class, IMaterializedViewStore;

	/// <summary>
	/// Configures the materialized view store with a factory function.
	/// </summary>
	/// <param name="storeFactory">Factory function to create the store.</param>
	/// <returns>The builder for fluent configuration.</returns>
	IMaterializedViewsBuilder UseStore(Func<IServiceProvider, IMaterializedViewStore> storeFactory);

	/// <summary>
	/// Configures the materialized view processor implementation.
	/// </summary>
	/// <typeparam name="TProcessor">The processor implementation type.</typeparam>
	/// <returns>The builder for fluent configuration.</returns>
	IMaterializedViewsBuilder UseProcessor<
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TProcessor>()
		where TProcessor : class, IMaterializedViewProcessor;

	/// <summary>
	/// Enables automatic catch-up on application startup.
	/// </summary>
	/// <returns>The builder for fluent configuration.</returns>
	/// <remarks>
	/// <para>
	/// When enabled, the processor will automatically catch up all registered views
	/// from their last known positions when the application starts.
	/// </para>
	/// </remarks>
	IMaterializedViewsBuilder EnableCatchUpOnStartup();

	/// <summary>
	/// Configures the batch size for event processing.
	/// </summary>
	/// <param name="batchSize">The number of events to process in each batch. Default: 100.</param>
	/// <returns>The builder for fluent configuration.</returns>
	IMaterializedViewsBuilder WithBatchSize(int batchSize);

	/// <summary>
	/// Enables the background refresh service with default options.
	/// </summary>
	/// <returns>The builder for fluent configuration.</returns>
	/// <remarks>
	/// <para>
	/// Registers <see cref="Services.MaterializedViewRefreshService"/> as a hosted service
	/// that periodically refreshes all registered materialized views.
	/// </para>
	/// </remarks>
	IMaterializedViewsBuilder UseRefreshService();

	/// <summary>
	/// Enables the background refresh service with custom configuration.
	/// </summary>
	/// <param name="configure">Action to configure refresh options.</param>
	/// <returns>The builder for fluent configuration.</returns>
	IMaterializedViewsBuilder UseRefreshService(Action<Services.MaterializedViewRefreshOptions> configure);

	/// <summary>
	/// Adds health checks for materialized views.
	/// </summary>
	/// <returns>The builder for fluent configuration.</returns>
	/// <remarks>
	/// <para>
	/// Registers <see cref="Health.MaterializedViewHealthCheck"/> which monitors:
	/// <list type="bullet">
	/// <item>View staleness against configured threshold</item>
	/// <item>Refresh failure rate</item>
	/// <item>View registration status</item>
	/// </list>
	/// </para>
	/// </remarks>
	IMaterializedViewsBuilder WithHealthChecks();

	/// <summary>
	/// Adds health checks for materialized views with custom configuration.
	/// </summary>
	/// <param name="configure">Action to configure health check options.</param>
	/// <returns>The builder for fluent configuration.</returns>
	IMaterializedViewsBuilder WithHealthChecks(Action<Health.MaterializedViewHealthCheckOptions> configure);

	/// <summary>
	/// Enables OpenTelemetry metrics for materialized views.
	/// </summary>
	/// <returns>The builder for fluent configuration.</returns>
	/// <remarks>
	/// <para>
	/// Registers <see cref="Diagnostics.MaterializedViewMetrics"/> which provides:
	/// <list type="bullet">
	/// <item><c>materialized_view.refresh.duration</c> - Histogram of refresh durations</item>
	/// <item><c>materialized_view.staleness</c> - Gauge of view staleness</item>
	/// <item><c>materialized_view.refresh.failures</c> - Counter of failures</item>
	/// <item><c>materialized_view.state</c> - Gauge of view health state</item>
	/// </list>
	/// </para>
	/// </remarks>
	IMaterializedViewsBuilder WithMetrics();
}
