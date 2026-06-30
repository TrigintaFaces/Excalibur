// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.CodeAnalysis;

using Excalibur.Data.ElasticSearch.Projections;
using Excalibur.EventSourcing.DependencyInjection;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods bridging ElasticSearch projection registration onto
/// <see cref="IEventSourcingBuilder"/> so consumers can configure projection
/// stores inside a single <c>AddExcalibur(excalibur =&gt; excalibur.AddEventSourcing(es =&gt; ...))</c>
/// composition root.
/// </summary>
public static class ElasticSearchProjectionsEventSourcingBuilderExtensions
{
	/// <summary>
	/// Registers multiple ElasticSearch projection stores sharing a single node URI.
	/// </summary>
	/// <param name="builder">The event-sourcing builder.</param>
	/// <param name="nodeUri">The shared ElasticSearch node URI.</param>
	/// <param name="configure">Action to register individual projection stores.</param>
	/// <returns>The same builder for fluent chaining.</returns>
	public static IEventSourcingBuilder AddElasticSearchProjections(
		this IEventSourcingBuilder builder,
		string nodeUri,
		Action<ElasticSearchProjectionRegistrar> configure)
	{
		ArgumentNullException.ThrowIfNull(builder);

		_ = builder.Services.AddElasticSearchProjections(nodeUri, configure);
		return builder;
	}

	/// <summary>
	/// Registers multiple ElasticSearch projection stores with shared options configuration.
	/// </summary>
	/// <param name="builder">The event-sourcing builder.</param>
	/// <param name="configureShared">Shared options applied to all projections.</param>
	/// <param name="configure">Action to register individual projection stores.</param>
	/// <returns>The same builder for fluent chaining.</returns>
	public static IEventSourcingBuilder AddElasticSearchProjections(
		this IEventSourcingBuilder builder,
		Action<ElasticSearchProjectionStoreOptions> configureShared,
		Action<ElasticSearchProjectionRegistrar> configure)
	{
		ArgumentNullException.ThrowIfNull(builder);

		_ = builder.Services.AddElasticSearchProjections(configureShared, configure);
		return builder;
	}

	/// <summary>
	/// Registers a single ElasticSearch projection store for the specified projection type.
	/// </summary>
	/// <typeparam name="TProjection">The projection type to store in ElasticSearch.</typeparam>
	/// <param name="builder">The event-sourcing builder.</param>
	/// <param name="configureOptions">Action to configure projection store options (node URI, index prefix, etc.).</param>
	/// <returns>The same builder for fluent chaining.</returns>
	/// <remarks>
	/// <para>
	/// This registers <c>IProjectionStore&lt;TProjection&gt;</c> backed by
	/// <see cref="ElasticSearchProjectionStore{TProjection}"/>.
	/// Use <see cref="AddElasticSearchProjections(IEventSourcingBuilder, string, Action{ElasticSearchProjectionRegistrar})"/>
	/// when registering multiple projections that share the same node URI.
	/// </para>
	/// <para>
	/// <b>Usage:</b>
	/// <code>
	/// services.AddExcalibur(x =&gt; x.AddEventSourcing(es =&gt;
	/// {
	///     es.AddElasticSearchProjectionStore&lt;OrderSummary&gt;(opts =&gt;
	///     {
	///         opts.NodeUri = "https://es.example.com:9200";
	///         opts.IndexPrefix = "orders";
	///     });
	/// }));
	/// </code>
	/// </para>
	/// </remarks>
	public static IEventSourcingBuilder AddElasticSearchProjectionStore<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.Interfaces)] TProjection>(
		this IEventSourcingBuilder builder,
		Action<ElasticSearchProjectionStoreOptions> configureOptions)
		where TProjection : class
	{
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentNullException.ThrowIfNull(configureOptions);

		_ = builder.Services.AddElasticSearchProjectionStore<TProjection>(configureOptions);
		return builder;
	}

	/// <summary>
	/// Registers a single ElasticSearch projection store for the specified projection type
	/// using the given ElasticSearch node URI.
	/// </summary>
	/// <typeparam name="TProjection">The projection type to store in ElasticSearch.</typeparam>
	/// <param name="builder">The event-sourcing builder.</param>
	/// <param name="nodeUri">The ElasticSearch node URI.</param>
	/// <param name="configureOptions">Optional action to further configure projection store options.</param>
	/// <returns>The same builder for fluent chaining.</returns>
	public static IEventSourcingBuilder AddElasticSearchProjectionStore<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.Interfaces)] TProjection>(
		this IEventSourcingBuilder builder,
		string nodeUri,
		Action<ElasticSearchProjectionStoreOptions>? configureOptions = null)
		where TProjection : class
	{
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentException.ThrowIfNullOrWhiteSpace(nodeUri);

		_ = builder.Services.AddElasticSearchProjectionStore<TProjection>(nodeUri, configureOptions);
		return builder;
	}
}
