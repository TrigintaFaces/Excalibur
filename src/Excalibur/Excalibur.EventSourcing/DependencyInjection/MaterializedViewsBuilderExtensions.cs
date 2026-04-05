// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.EventSourcing.Abstractions;
using Excalibur.EventSourcing.Diagnostics;
using Excalibur.EventSourcing.Health;
using Excalibur.EventSourcing.Services;
using Excalibur.EventSourcing.Views;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Excalibur.EventSourcing.DependencyInjection;

/// <summary>
/// Extension methods for <see cref="IMaterializedViewsBuilder"/>.
/// </summary>
public static class MaterializedViewsBuilderExtensions
{
	/// <summary>
	/// Registers a materialized view builder with a factory function.
	/// </summary>
	/// <typeparam name="TView">The view type.</typeparam>
	/// <param name="builder">The builder.</param>
	/// <param name="builderFactory">Factory function to create the builder.</param>
	/// <returns>The builder for fluent configuration.</returns>
	public static IMaterializedViewsBuilder AddBuilder<TView>(
		this IMaterializedViewsBuilder builder,
		Func<IServiceProvider, IMaterializedViewBuilder<TView>> builderFactory)
		where TView : class, new()
	{
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentNullException.ThrowIfNull(builderFactory);

		_ = builder.Services.AddSingleton(builderFactory);

		_ = builder.Services.AddSingleton(sp =>
			new MaterializedViewBuilderRegistration(
				typeof(TView),
				typeof(IMaterializedViewBuilder<TView>),
				sp.GetRequiredService<IMaterializedViewBuilder<TView>>()));

		return builder;
	}

	/// <summary>
	/// Enables automatic catch-up on application startup.
	/// </summary>
	/// <param name="builder">The builder.</param>
	/// <returns>The builder for fluent configuration.</returns>
	public static IMaterializedViewsBuilder EnableCatchUpOnStartup(this IMaterializedViewsBuilder builder)
	{
		ArgumentNullException.ThrowIfNull(builder);
		_ = builder.Services.Configure<MaterializedViewOptions>(options => options.CatchUpOnStartup = true);
		return builder;
	}

	/// <summary>
	/// Configures the batch size for event processing.
	/// </summary>
	/// <param name="builder">The builder.</param>
	/// <param name="batchSize">The number of events to process in each batch. Default: 100.</param>
	/// <returns>The builder for fluent configuration.</returns>
	public static IMaterializedViewsBuilder WithBatchSize(this IMaterializedViewsBuilder builder, int batchSize)
	{
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentOutOfRangeException.ThrowIfNegativeOrZero(batchSize);

		_ = builder.Services.Configure<MaterializedViewOptions>(options => options.BatchSize = batchSize);
		return builder;
	}

	/// <summary>
	/// Enables the background refresh service with default options.
	/// </summary>
	/// <param name="builder">The builder.</param>
	/// <returns>The builder for fluent configuration.</returns>
	public static IMaterializedViewsBuilder UseRefreshService(this IMaterializedViewsBuilder builder)
	{
		return UseRefreshService(builder, _ => { });
	}

	/// <summary>
	/// Enables the background refresh service with custom configuration.
	/// </summary>
	/// <param name="builder">The builder.</param>
	/// <param name="configure">Action to configure refresh options.</param>
	/// <returns>The builder for fluent configuration.</returns>
	public static IMaterializedViewsBuilder UseRefreshService(this IMaterializedViewsBuilder builder, Action<MaterializedViewRefreshOptions> configure)
	{
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentNullException.ThrowIfNull(configure);

		_ = builder.Services.AddOptions<MaterializedViewRefreshOptions>()
			.Configure(configure)
			.ValidateDataAnnotations()
			.ValidateOnStart();

		builder.Services.TryAddSingleton(TimeProvider.System);
		_ = builder.Services.AddHostedService<MaterializedViewRefreshService>();

		return builder;
	}

	/// <summary>
	/// Adds health checks for materialized views.
	/// </summary>
	/// <param name="builder">The builder.</param>
	/// <returns>The builder for fluent configuration.</returns>
	public static IMaterializedViewsBuilder WithHealthChecks(this IMaterializedViewsBuilder builder)
	{
		return WithHealthChecks(builder, _ => { });
	}

	/// <summary>
	/// Adds health checks for materialized views with custom configuration.
	/// </summary>
	/// <param name="builder">The builder.</param>
	/// <param name="configure">Action to configure health check options.</param>
	/// <returns>The builder for fluent configuration.</returns>
	public static IMaterializedViewsBuilder WithHealthChecks(this IMaterializedViewsBuilder builder, Action<MaterializedViewHealthCheckOptions> configure)
	{
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentNullException.ThrowIfNull(configure);

		_ = builder.Services.AddOptions<MaterializedViewHealthCheckOptions>()
			.Configure(configure)
			.ValidateDataAnnotations()
			.ValidateOnStart();

		builder.Services.TryAddSingleton<MaterializedViewMetrics>();
		builder.Services.TryAddSingleton(TimeProvider.System);

		var options = new MaterializedViewHealthCheckOptions();
		configure(options);

		_ = builder.Services.AddHealthChecks()
			.AddCheck<MaterializedViewHealthCheck>(
				options.Name,
				tags: options.Tags);

		return builder;
	}

	/// <summary>
	/// Enables OpenTelemetry metrics for materialized views.
	/// </summary>
	/// <param name="builder">The builder.</param>
	/// <returns>The builder for fluent configuration.</returns>
	public static IMaterializedViewsBuilder WithMetrics(this IMaterializedViewsBuilder builder)
	{
		ArgumentNullException.ThrowIfNull(builder);
		builder.Services.TryAddSingleton<MaterializedViewMetrics>();
		return builder;
	}
}
