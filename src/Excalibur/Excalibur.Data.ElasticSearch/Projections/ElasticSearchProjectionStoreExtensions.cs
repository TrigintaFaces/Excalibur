// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Diagnostics.CodeAnalysis;

using Elastic.Clients.Elasticsearch;

using Excalibur.Data.ElasticSearch.Projections;
using Excalibur.EventSourcing;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for registering ElasticSearch projection store services.
/// </summary>
/// <remarks>
/// <para>
/// Each projection type gets its own named options instance keyed by the projection type name.
/// This allows multiple projection stores to coexist with independent configurations
/// (different URIs, index prefixes, shard counts, etc.).
/// </para>
/// </remarks>
public static class ElasticSearchProjectionStoreExtensions
{
	/// <summary>
	/// Adds the ElasticSearch projection store to the service collection.
	/// </summary>
	/// <typeparam name="TProjection">The projection type to store.</typeparam>
	/// <param name="services">The service collection.</param>
	/// <param name="configureOptions">Action to configure projection store options.</param>
	/// <returns>The service collection for chaining.</returns>
	public static IServiceCollection AddElasticSearchProjectionStore<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.Interfaces)] TProjection>(
		this IServiceCollection services,
		Action<ElasticSearchProjectionStoreOptions> configureOptions)
		where TProjection : class
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configureOptions);

		// Named options keyed by the projection's unqualified type name, through the one function that
		// produces that key -- and refusing the case where two different types produce the same one.
		var optionsName = ElasticSearchProjectionIndexConvention.GetOptionsName<TProjection>();
		ClaimOptionsName(services, optionsName, typeof(TProjection));
		_ = services.Configure(optionsName, configureOptions);
		WireOptionsValidation(services, optionsName);

		// Register projection store -- uses IOptionsMonitor to resolve named options
		services.TryAddScoped<IProjectionStore<TProjection>>(sp =>
		{
			var optionsMonitor = sp.GetRequiredService<IOptionsMonitor<ElasticSearchProjectionStoreOptions>>();
			var logger = sp.GetRequiredService<ILogger<ElasticSearchProjectionStore<TProjection>>>();

			return new ElasticSearchProjectionStore<TProjection>(optionsMonitor, logger);
		});

		return services;
	}

	/// <summary>
	/// Adds the ElasticSearch projection store to the service collection with a node URI.
	/// </summary>
	/// <typeparam name="TProjection">The projection type to store.</typeparam>
	/// <param name="services">The service collection.</param>
	/// <param name="nodeUri">The ElasticSearch node URI.</param>
	/// <param name="configureOptions">Optional action to further configure projection store options.</param>
	/// <returns>The service collection for chaining.</returns>
	public static IServiceCollection AddElasticSearchProjectionStore<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.Interfaces)] TProjection>(
		this IServiceCollection services,
		string nodeUri,
		Action<ElasticSearchProjectionStoreOptions>? configureOptions = null)
		where TProjection : class
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentException.ThrowIfNullOrWhiteSpace(nodeUri);

		return services.AddElasticSearchProjectionStore<TProjection>(options =>
		{
			options.NodeUri = nodeUri;
			configureOptions?.Invoke(options);
		});
	}

	/// <summary>
	/// Adds the ElasticSearch projection store to the service collection with an existing client.
	/// </summary>
	/// <typeparam name="TProjection">The projection type to store.</typeparam>
	/// <param name="services">The service collection.</param>
	/// <param name="clientFactory">Factory function that provides an ElasticSearch client.</param>
	/// <param name="configureOptions">Action to configure projection store options.</param>
	/// <returns>The service collection for chaining.</returns>
	/// <remarks>
	/// Use this overload for advanced scenarios like shared client instances,
	/// custom connection pooling, or integration with existing ElasticSearch infrastructure.
	/// </remarks>
	public static IServiceCollection AddElasticSearchProjectionStore<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.Interfaces)] TProjection>(
		this IServiceCollection services,
		Func<IServiceProvider, ElasticsearchClient> clientFactory,
		Action<ElasticSearchProjectionStoreOptions> configureOptions)
		where TProjection : class
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(clientFactory);
		ArgumentNullException.ThrowIfNull(configureOptions);

		// Named options keyed by the projection's unqualified type name, through the one function that
		// produces that key -- and refusing the case where two different types produce the same one.
		var optionsName = ElasticSearchProjectionIndexConvention.GetOptionsName<TProjection>();
		ClaimOptionsName(services, optionsName, typeof(TProjection));
		_ = services.Configure(optionsName, configureOptions);

		// Recorded AFTER the consumer's action, so it cannot be cleared by one: this path always supplies
		// the client, which makes the node address dead and the required-node check wrong here.
		_ = services.Configure<ElasticSearchProjectionStoreOptions>(
			optionsName, static o => o.ClientSuppliedByRegistration = true);
		WireOptionsValidation(services, optionsName);

		// Register projection store with client factory
		services.TryAddScoped<IProjectionStore<TProjection>>(sp =>
		{
			var client = clientFactory(sp);
			var optionsMonitor = sp.GetRequiredService<IOptionsMonitor<ElasticSearchProjectionStoreOptions>>();
			var logger = sp.GetRequiredService<ILogger<ElasticSearchProjectionStore<TProjection>>>();

			return new ElasticSearchProjectionStore<TProjection>(client, optionsMonitor, logger);
		});

		return services;
	}

	/// <summary>
	/// Registers multiple ElasticSearch projection stores that share a common node URI,
	/// reducing boilerplate when multiple projections target the same cluster.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="nodeUri">The shared ElasticSearch node URI.</param>
	/// <param name="configure">Action to register individual projection stores.</param>
	/// <returns>The service collection for chaining.</returns>
	/// <example>
	/// <code>
	/// services.AddElasticSearchProjections("https://es.example.com:9200", projections =>
	/// {
	///     projections.Add&lt;OrderSummary&gt;();
	///     projections.Add&lt;CustomerProfile&gt;(options => options.Index.IndexPrefix = "customers");
	///     projections.Add&lt;ProductCatalog&gt;(options => options.Index.NumberOfShards = 3);
	/// });
	/// </code>
	/// </example>
	public static IServiceCollection AddElasticSearchProjections(
		this IServiceCollection services,
		string nodeUri,
		Action<ElasticSearchProjectionRegistrar> configure)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentException.ThrowIfNullOrWhiteSpace(nodeUri);
		ArgumentNullException.ThrowIfNull(configure);

		var registrar = new ElasticSearchProjectionRegistrar(services, nodeUri);
		configure(registrar);

		return services;
	}

	/// <summary>
	/// Registers multiple ElasticSearch projection stores with shared options configuration,
	/// allowing advanced settings like custom node URIs, index prefixes, and shard counts.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="configureShared">Shared options applied to all projections before per-projection overrides.</param>
	/// <param name="configure">Action to register individual projection stores.</param>
	/// <returns>The service collection for chaining.</returns>
	/// <example>
	/// <code>
	/// services.AddElasticSearchProjections(
	///     shared => { shared.NodeUri = "https://es.example.com:9200"; shared.Index.NumberOfReplicas = 2; },
	///     projections =>
	///     {
	///         projections.Add&lt;OrderSummary&gt;();
	///         projections.Add&lt;CustomerProfile&gt;(options => options.Index.IndexPrefix = "customers");
	///     });
	/// </code>
	/// </example>
	public static IServiceCollection AddElasticSearchProjections(
		this IServiceCollection services,
		Action<ElasticSearchProjectionStoreOptions> configureShared,
		Action<ElasticSearchProjectionRegistrar> configure)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configureShared);
		ArgumentNullException.ThrowIfNull(configure);

		var registrar = new ElasticSearchProjectionRegistrar(services, configureShared);
		configure(registrar);

		return services;
	}

	/// <summary>
	/// Adds the ElasticSearch projection store, taking its cluster from a configuration section.
	/// </summary>
	/// <typeparam name="TProjection">The projection type to store.</typeparam>
	/// <param name="services">The service collection.</param>
	/// <param name="section">
	/// The section describing the cluster: <c>NodeUris</c> as an array of node addresses, plus
	/// <c>Auth</c> and <c>Index</c>.
	/// </param>
	/// <param name="configureOptions">Optional code configuration, applied AFTER the section so it wins.</param>
	/// <returns>The service collection for chaining.</returns>
	/// <remarks>
	/// <para>
	/// The section is bound BEFORE <paramref name="configureOptions"/> runs, because
	/// <c>IConfigureOptions&lt;T&gt;</c> execute in registration order and code overriding
	/// configuration is both the conventional precedence and the only order that lets a
	/// per-projection override mean anything when a shared section already set the same property.
	/// </para>
	/// <para>
	/// There is no node default, so a section naming none is refused at start-up rather than
	/// silently addressing the local machine.
	/// </para>
	/// <code>
	/// // appsettings.json
	/// // "Elasticsearch": {
	/// //   "NodeUris": [ "http://es01:9200", "http://es02:9200" ],
	/// //   "Auth": { "ApiKey": "..." },
	/// //   "Index": { "IndexPrefix": "readmodels", "NumberOfReplicas": 1 }
	/// // }
	/// services.AddElasticSearchProjectionStore&lt;OrderSummary&gt;(
	///     configuration.GetSection("Elasticsearch"),
	///     o =&gt; o.Index.IndexName = "ordersummary");
	/// </code>
	/// </remarks>
	[RequiresUnreferencedCode("Binds configuration to the options type reflectively; configure the store in code for trimming and AOT.")]
	[RequiresDynamicCode("Binds configuration to the options type reflectively; configure the store in code for trimming and AOT.")]
	public static IServiceCollection AddElasticSearchProjectionStore<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.Interfaces)] TProjection>(
		this IServiceCollection services,
		IConfiguration section,
		Action<ElasticSearchProjectionStoreOptions>? configureOptions = null)
		where TProjection : class
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(section);

		var optionsName = ElasticSearchProjectionIndexConvention.GetOptionsName<TProjection>();

		// Bound first, so the code action registered by the call below runs after it and wins.
		_ = services.AddOptions<ElasticSearchProjectionStoreOptions>(optionsName).Bind(section);

		return services.AddElasticSearchProjectionStore<TProjection>(
			configureOptions ?? (static _ => { }));
	}

	/// <summary>
	/// Registers multiple ElasticSearch projection stores sharing one configured cluster.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="section">The configuration section describing the shared cluster.</param>
	/// <param name="configure">Action to register individual projection stores.</param>
	/// <returns>The service collection for chaining.</returns>
	/// <remarks>
	/// <para>
	/// The cluster is described once and every projection registered inside
	/// <paramref name="configure"/> binds it, while keeping its own named options -- so a
	/// per-projection index name does not leak across read models.
	/// </para>
	/// <code>
	/// services.AddElasticSearchProjections(
	///     configuration.GetSection("Elasticsearch"),
	///     projections =&gt; projections
	///         .Add&lt;OrderSummary&gt;(o =&gt; o.Index.IndexName = "ordersummary")
	///         .Add&lt;CustomerProfile&gt;(o =&gt; o.Index.IndexName = "customerprofile"));
	/// </code>
	/// </remarks>
	[RequiresUnreferencedCode("Binds configuration to the options type reflectively; configure the stores in code for trimming and AOT.")]
	[RequiresDynamicCode("Binds configuration to the options type reflectively; configure the stores in code for trimming and AOT.")]
	public static IServiceCollection AddElasticSearchProjections(
		this IServiceCollection services,
		IConfiguration section,
		Action<ElasticSearchProjectionRegistrar> configure)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(section);
		ArgumentNullException.ThrowIfNull(configure);

		var registrar = new ElasticSearchProjectionRegistrar(services, section);
		configure(registrar);

		return services;
	}

	/// <summary>
	/// Wires eager (fail-fast at startup) validation for a named projection-store options instance.
	/// Registers the cross-property validator once (idempotent) and marks the named instance for
	/// <c>ValidateOnStart</c>, so a misconfiguration surfaces at host start rather than only at the
	/// store's first use.
	/// </summary>
	private static void WireOptionsValidation(IServiceCollection services, string optionsName)
	{
		_ = services.AddOptions<ElasticSearchProjectionStoreOptions>(optionsName).ValidateOnStart();
		services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IValidateOptions<ElasticSearchProjectionStoreOptions>,
				ElasticSearchProjectionStoreOptionsValidator>());
	}

	/// <summary>
	/// Records which projection type owns an options name, and refuses a second type claiming the same one.
	/// </summary>
	/// <remarks>
	/// <para>
	/// The options name is the projection's UNQUALIFIED type name, so two read models called
	/// <c>Summary</c> in different namespaces would share one options instance and therefore one index:
	/// each would read the other's documents and overwrite them on the same id. Nothing reported it --
	/// the registrations look correct, the types are plainly distinct, and the collision lives in a
	/// string neither of them mentions.
	/// </para>
	/// <para>
	/// Refused at REGISTRATION rather than at start-up, because that is where the second type is named
	/// and where the stack points at the line a developer has to change. Re-registering the SAME
	/// projection is not a collision: the last call wins, as elsewhere in the framework.
	/// </para>
	/// </remarks>
	private static void ClaimOptionsName(IServiceCollection services, string optionsName, Type projectionType)
	{
		// Held on the collection rather than in a static, so two containers in one process -- a test
		// suite, a multi-tenant host -- cannot see each other's registrations.
		var claims = (ProjectionOptionsNameClaims?)services
			.FirstOrDefault(static d => !d.IsKeyedService && d.ServiceType == typeof(ProjectionOptionsNameClaims))
			?.GetImplementationInstance();

		if (claims is null)
		{
			claims = new ProjectionOptionsNameClaims();
			_ = services.AddSingleton(claims);
		}

		if (claims.Owners.TryGetValue(optionsName, out var owner) && owner != projectionType)
		{
			throw new InvalidOperationException(
				$"Two projection types would share the options instance and the index named "
				+ $"'{optionsName}': '{owner.FullName}' and '{projectionType.FullName}'. A projection's "
				+ "store options and its index name are keyed on the UNQUALIFIED type name, so these two "
				+ "would read each other's documents and overwrite them on the same id. Give at least one "
				+ "of them its own index with Index.IndexName, or rename one of the types.");
		}

		claims.Owners[optionsName] = projectionType;
	}

	/// <summary>Which projection type owns each options name, for one service collection.</summary>
	internal sealed class ProjectionOptionsNameClaims
	{
		internal Dictionary<string, Type> Owners { get; } = new(StringComparer.Ordinal);
	}
}
