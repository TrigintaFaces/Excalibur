// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.EventSourcing.DependencyInjection;
using Excalibur.EventSourcing.Views;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for configuring materialized view services.
/// </summary>
public static class MaterializedViewsServiceCollectionExtensions
{
	/// <summary>
	/// Adds materialized view services to the service collection.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <returns>The service collection for method chaining.</returns>
	/// <remarks>
	/// <para>
	/// This method registers the core materialized view infrastructure with sensible defaults.
	/// Use <see cref="AddMaterializedViews(IServiceCollection, Action{IMaterializedViewsBuilder})"/>
	/// to register view builders and configure stores.
	/// </para>
	/// </remarks>
	public static IServiceCollection AddMaterializedViews(this IServiceCollection services)
	{
		ArgumentNullException.ThrowIfNull(services);

		// Register options
		_ = services.AddOptions<MaterializedViewOptions>()
			.ValidateDataAnnotations()
			.ValidateOnStart();

		return services;
	}

	/// <summary>
	/// Adds materialized view services with configuration.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="configure">Configuration action for the materialized views builder.</param>
	/// <returns>The service collection for method chaining.</returns>
	/// <remarks>
	/// <para>
	/// This is the primary method for configuring materialized views. It allows you to
	/// register view builders, configure stores, and set up processors.
	/// </para>
	/// <para>
	/// <b>Usage:</b>
	/// <code>
	/// services.AddMaterializedViews(builder =>
	/// {
	///     builder.AddBuilder&lt;OrderSummaryView, OrderSummaryViewBuilder&gt;()
	///            .AddBuilder&lt;CustomerStatsView, CustomerStatsViewBuilder&gt;()
	///            .UseStore&lt;SqlServerMaterializedViewStore&gt;()
	///            .EnableCatchUpOnStartup()
	///            .WithBatchSize(200);
	/// });
	/// </code>
	/// </para>
	/// </remarks>
	public static IServiceCollection AddMaterializedViews(
		this IServiceCollection services,
		Action<IMaterializedViewsBuilder> configure)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configure);

		// Ensure base services are registered
		_ = services.AddMaterializedViews();

		// Configure using the builder pattern
		var builder = new MaterializedViewsBuilder(services);
		configure(builder);

		return services;
	}

	/// <summary>
	/// Checks if materialized view services have been registered.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <returns>True if materialized view services are registered; otherwise false.</returns>
	public static bool HasMaterializedViews(this IServiceCollection services)
	{
		ArgumentNullException.ThrowIfNull(services);
		return services.Any(s => s.ServiceType == typeof(Excalibur.EventSourcing.Abstractions.IMaterializedViewStore));
	}
}
