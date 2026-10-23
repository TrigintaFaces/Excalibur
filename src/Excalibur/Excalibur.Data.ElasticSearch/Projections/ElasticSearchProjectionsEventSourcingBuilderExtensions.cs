// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Diagnostics.CodeAnalysis;

using Excalibur.Data.ElasticSearch.Projections;
using Excalibur.EventSourcing.DependencyInjection;
using Excalibur.EventSourcing.Projections;

using Microsoft.Extensions.Configuration;

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
	/// Registers a projection and the ElasticSearch store it lives in, in one call.
	/// </summary>
	/// <typeparam name="TProjection">The projection type.</typeparam>
	/// <param name="builder">The event-sourcing builder.</param>
	/// <param name="configure">Configures what the projection folds — its mode and event handlers.</param>
	/// <param name="configureStore">
	/// Configures where it is stored — the cluster, and this projection's own index.
	/// </param>
	/// <returns>The same builder for fluent chaining.</returns>
	/// <remarks>
	/// <para>
	/// A stored projection needs two things said about it: what it folds, and where it goes. Said
	/// separately they can be forgotten separately — a projection registered with no store folds into
	/// nothing — so this says both, and cannot be half made.
	/// </para>
	/// <para>
	/// <b><paramref name="configureStore"/> is required on purpose.</b> The store options default their
	/// node to <c>http://localhost:9200</c>, so a one-call API that let it be omitted would start
	/// anywhere and reach a cluster only on a developer's own machine — a worse trap than the two calls
	/// it replaces, because it fails where nobody is looking rather than at the keyboard.
	/// </para>
	/// <code>
	/// services.AddExcalibur(x => x.AddEventSourcing(es => es
	///     .AddElasticSearchProjection&lt;OrderSummary&gt;(
	///         p => p.Async().When&lt;OrderPlaced&gt;((proj, e) => proj.Total = e.Amount),
	///         o =>
	///         {
	///             o.NodeUris = [new Uri("http://es01:9200"), new Uri("http://es02:9200")];
	///             o.Index.IndexName = "ordersummary";
	///         })));
	/// </code>
	/// </remarks>
	[RequiresUnreferencedCode("Implementations serialize the projection type reflectively; supply JsonSerializerOptions with a source-generated resolver for trimming and AOT.")]
	[RequiresDynamicCode("Implementations serialize the projection type reflectively; supply JsonSerializerOptions with a source-generated resolver for trimming and AOT.")]
	public static IEventSourcingBuilder AddElasticSearchProjection<
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.Interfaces)] TProjection>(
		this IEventSourcingBuilder builder,
		Action<IProjectionBuilder<TProjection>> configure,
		Action<ElasticSearchProjectionStoreOptions> configureStore)
		where TProjection : class, new()
	{
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentNullException.ThrowIfNull(configure);
		ArgumentNullException.ThrowIfNull(configureStore);

		// What it folds, then where it goes. Both on the builder the consumer already has, and neither
		// reachable without the other.
		_ = builder.AddProjection(configure);
		_ = builder.Services.AddElasticSearchProjectionStore<TProjection>(configureStore);

		return builder;
	}

	/// <summary>
	/// Registers multiple ElasticSearch projection stores sharing one configured cluster.
	/// </summary>
	/// <param name="builder">The event-sourcing builder.</param>
	/// <param name="section">The configuration section describing the shared cluster.</param>
	/// <param name="configure">Action to register individual projection stores.</param>
	/// <returns>The same builder for fluent chaining.</returns>
	/// <remarks>
	/// The cluster is described once, in configuration, and every projection registered inside
	/// <paramref name="configure"/> binds it while keeping its own index.
	/// </remarks>
	[RequiresUnreferencedCode("Binds configuration to the options type reflectively; configure the stores in code for trimming and AOT.")]
	[RequiresDynamicCode("Binds configuration to the options type reflectively; configure the stores in code for trimming and AOT.")]
	public static IEventSourcingBuilder AddElasticSearchProjections(
		this IEventSourcingBuilder builder,
		IConfiguration section,
		Action<ElasticSearchProjectionRegistrar> configure)
	{
		ArgumentNullException.ThrowIfNull(builder);

		_ = builder.Services.AddElasticSearchProjections(section, configure);
		return builder;
	}

	/// <summary>
	/// Registers one ElasticSearch projection store, taking its cluster from a configuration section.
	/// </summary>
	/// <typeparam name="TProjection">The projection type to store.</typeparam>
	/// <param name="builder">The event-sourcing builder.</param>
	/// <param name="section">The configuration section describing the cluster.</param>
	/// <param name="configureOptions">Optional code configuration, applied after the section so it wins.</param>
	/// <returns>The same builder for fluent chaining.</returns>
	[RequiresUnreferencedCode("Binds configuration to the options type reflectively; configure the store in code for trimming and AOT.")]
	[RequiresDynamicCode("Binds configuration to the options type reflectively; configure the store in code for trimming and AOT.")]
	public static IEventSourcingBuilder AddElasticSearchProjectionStore<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.Interfaces)] TProjection>(
		this IEventSourcingBuilder builder,
		IConfiguration section,
		Action<ElasticSearchProjectionStoreOptions>? configureOptions = null)
		where TProjection : class
	{
		ArgumentNullException.ThrowIfNull(builder);

		_ = builder.Services.AddElasticSearchProjectionStore<TProjection>(section, configureOptions);
		return builder;
	}

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
	///         opts.Index.IndexPrefix = "orders";
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
