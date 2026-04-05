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
}
