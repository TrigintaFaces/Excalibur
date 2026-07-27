// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.EventSourcing;
using Excalibur.EventSourcing.DependencyInjection;
using Excalibur.EventSourcing.Views;

using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using System.Linq;

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
			.ValidateOnStart();

		// Register default processor (consumers can override via UseProcessor<T>)
		services.TryAddSingleton<IMaterializedViewProcessor, MaterializedViewProcessor>();
		MarkDefaultProcessorIfItWon(services);

		// Fail at host start, not at first refresh. The processor's constructor rejects a non-atomic store,
		// but the processor is resolved lazily inside the refresh service's retry loop, which catches and
		// logs — so that throw never reaches an operator. This puts the same check in the startup pipeline.
		services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IHostedService, AtomicMaterializedViewStoreValidator>());

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

		// Register options (but NOT the default processor yet — let configure run first)
		_ = services.AddOptions<MaterializedViewOptions>()
			.ValidateOnStart();

		// Configure using the builder pattern — consumer may call UseProcessor<T>()
		var builder = new MaterializedViewsBuilder(services);
		configure(builder);

		// Register default processor AFTER configure so UseProcessor<T> wins via TryAdd
		services.TryAddSingleton<IMaterializedViewProcessor, MaterializedViewProcessor>();
		MarkDefaultProcessorIfItWon(services);

		// Fail at host start, not at first refresh. The processor's constructor rejects a non-atomic store,
		// but the processor is resolved lazily inside the refresh service's retry loop, which catches and
		// logs — so that throw never reaches an operator. This puts the same check in the startup pipeline.
		services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IHostedService, AtomicMaterializedViewStoreValidator>());

		return services;
	}


	/// <summary>
	/// Records whether the built-in processor won registration, by reading the descriptor that actually won.
	/// </summary>
	/// <remarks>
	/// A consumer's <c>UseProcessor&lt;TProcessor&gt;()</c> registers first, so the <c>TryAdd</c> above is a
	/// no-op and the winning descriptor names their type. Inspecting that descriptor is a statement about the
	/// container rather than a guess about the call order that produced it.
	/// </remarks>
	private static void MarkDefaultProcessorIfItWon(IServiceCollection services)
	{
		var descriptor = services.LastOrDefault(d => d.ServiceType == typeof(IMaterializedViewProcessor));

		if (descriptor?.GetImplementationType() == typeof(MaterializedViewProcessor))
		{
			services.TryAddSingleton<DefaultMaterializedViewProcessorMarker>();
		}
	}

	/// <summary>
	/// Checks if materialized view services have been registered.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <returns>True if materialized view services are registered; otherwise false.</returns>
	public static bool HasMaterializedViews(this IServiceCollection services)
	{
		ArgumentNullException.ThrowIfNull(services);
		return services.Any(s => s.ServiceType == typeof(Excalibur.EventSourcing.IMaterializedViewStore));
	}
}
