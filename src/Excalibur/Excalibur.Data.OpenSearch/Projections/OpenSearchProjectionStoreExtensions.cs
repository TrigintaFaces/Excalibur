// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Diagnostics.CodeAnalysis;

using Excalibur.Data.OpenSearch.Projections;
using Excalibur.EventSourcing;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using OpenSearch.Client;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for registering OpenSearch projection store services.
/// </summary>
/// <remarks>
/// <para>
/// Each projection type gets its own named options instance keyed by the projection type name.
/// This allows multiple projection stores to coexist with independent configurations.
/// </para>
/// </remarks>
public static class OpenSearchProjectionStoreExtensions
{
	/// <summary>
	/// Adds the OpenSearch projection store to the service collection.
	/// </summary>
	/// <typeparam name="TProjection">The projection type to store.</typeparam>
	/// <param name="services">The service collection.</param>
	/// <param name="configureOptions">Action to configure projection store options.</param>
	/// <returns>The service collection for chaining.</returns>
	/// <remarks>
	/// If an <see cref="IOpenSearchClient" /> is registered in the service collection, the store uses it.
	/// Otherwise the store connects to <see cref="OpenSearchProjectionStoreOptions.NodeUri" />.
	/// </remarks>
	public static IServiceCollection AddOpenSearchProjectionStore<TProjection>(
		this IServiceCollection services,
		Action<OpenSearchProjectionStoreOptions> configureOptions)
		where TProjection : class
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configureOptions);

		var optionsName = typeof(TProjection).Name;
		_ = services.Configure(optionsName, configureOptions);

		WireOptionsValidation(services, optionsName);

		services.TryAddScoped<IProjectionStore<TProjection>>(sp =>
		{
			var optionsMonitor = sp.GetRequiredService<IOptionsMonitor<OpenSearchProjectionStoreOptions>>();
			var logger = sp.GetRequiredService<ILogger<OpenSearchProjectionStore<TProjection>>>();

			// Prefer a client the consumer registered. Building one from NodeUri regardless would make a
			// deliberate registration appear to take effect while the store talked to somewhere else
			// entirely — and the default node is a local address, so that failure surfaces only if nothing
			// happens to be listening on it.
			// Accept either registration shape. A consumer may register the interface, or -- more commonly,
			// since nothing in this framework registers a client for them -- the concrete OpenSearchClient.
			// Resolving only the interface would leave the concrete registration silently unused and fall
			// back to NodeUri, which is the very substitution this lookup exists to prevent.
			var client = sp.GetService<IOpenSearchClient>() ?? sp.GetService<OpenSearchClient>();

			return client is not null
				? new OpenSearchProjectionStore<TProjection>(client, optionsMonitor, logger)
				: new OpenSearchProjectionStore<TProjection>(optionsMonitor, logger);
		});

		return services;
	}

	/// <summary>
	/// Adds the OpenSearch projection store with a node URI.
	/// </summary>
	/// <typeparam name="TProjection">The projection type to store.</typeparam>
	/// <param name="services">The service collection.</param>
	/// <param name="nodeUri">The OpenSearch node URI.</param>
	/// <param name="configureOptions">Optional action to further configure options.</param>
	/// <returns>The service collection for chaining.</returns>
	public static IServiceCollection AddOpenSearchProjectionStore<TProjection>(
		this IServiceCollection services,
		string nodeUri,
		Action<OpenSearchProjectionStoreOptions>? configureOptions = null)
		where TProjection : class
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentException.ThrowIfNullOrWhiteSpace(nodeUri);

		return services.AddOpenSearchProjectionStore<TProjection>(options =>
		{
			options.NodeUri = nodeUri;
			configureOptions?.Invoke(options);
		});
	}

	/// <summary>
	/// Adds the OpenSearch projection store with an existing client factory.
	/// </summary>
	/// <typeparam name="TProjection">The projection type to store.</typeparam>
	/// <param name="services">The service collection.</param>
	/// <param name="clientFactory">Factory function that provides an OpenSearch client.</param>
	/// <param name="configureOptions">Action to configure projection store options.</param>
	/// <returns>The service collection for chaining.</returns>
	public static IServiceCollection AddOpenSearchProjectionStore<TProjection>(
		this IServiceCollection services,
		Func<IServiceProvider, IOpenSearchClient> clientFactory,
		Action<OpenSearchProjectionStoreOptions> configureOptions)
		where TProjection : class
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(clientFactory);
		ArgumentNullException.ThrowIfNull(configureOptions);

		var optionsName = typeof(TProjection).Name;
		_ = services.Configure(optionsName, configureOptions);

		// Recorded AFTER the consumer's action, so it cannot be cleared by one: this path always supplies
		// the client, which makes the node address dead and the required-node check wrong here.
		_ = services.Configure<OpenSearchProjectionStoreOptions>(
			optionsName, static o => o.ClientSuppliedByRegistration = true);

		WireOptionsValidation(services, optionsName);

		services.TryAddScoped<IProjectionStore<TProjection>>(sp =>
		{
			var client = clientFactory(sp);
			var optionsMonitor = sp.GetRequiredService<IOptionsMonitor<OpenSearchProjectionStoreOptions>>();
			var logger = sp.GetRequiredService<ILogger<OpenSearchProjectionStore<TProjection>>>();
			return new OpenSearchProjectionStore<TProjection>(client, optionsMonitor, logger);
		});

		return services;
	}

	/// <summary>
	/// Registers multiple OpenSearch projection stores with a shared node URI.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="nodeUri">The shared OpenSearch node URI.</param>
	/// <param name="configure">Action to register individual projection stores.</param>
	/// <returns>The service collection for chaining.</returns>
	public static IServiceCollection AddOpenSearchProjections(
		this IServiceCollection services,
		string nodeUri,
		Action<OpenSearchProjectionRegistrar> configure)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentException.ThrowIfNullOrWhiteSpace(nodeUri);
		ArgumentNullException.ThrowIfNull(configure);

		var registrar = new OpenSearchProjectionRegistrar(services, nodeUri);
		configure(registrar);

		return services;
	}

	/// <summary>
	/// Registers multiple OpenSearch projection stores with shared options configuration.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="configureShared">Shared options applied to all projections.</param>
	/// <param name="configure">Action to register individual projection stores.</param>
	/// <returns>The service collection for chaining.</returns>
	public static IServiceCollection AddOpenSearchProjections(
		this IServiceCollection services,
		Action<OpenSearchProjectionStoreOptions> configureShared,
		Action<OpenSearchProjectionRegistrar> configure)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configureShared);
		ArgumentNullException.ThrowIfNull(configure);

		var registrar = new OpenSearchProjectionRegistrar(services, configureShared);
		configure(registrar);

		return services;
	}

	/// <summary>
	/// Adds the OpenSearch projection store, taking its cluster from a configuration section.
	/// </summary>
	/// <typeparam name="TProjection">The projection type to store.</typeparam>
	/// <param name="services">The service collection.</param>
	/// <param name="section">The configuration section to bind the store options from.</param>
	/// <param name="configureOptions">Optional action applied AFTER binding, so code overrides configuration.</param>
	/// <returns>The service collection for chaining.</returns>
	/// <remarks>
	/// <para>
	/// There is no node default, so a section naming none is refused at start-up rather than silently
	/// addressing the local machine.
	/// </para>
	/// <code>
	/// // appsettings.json
	/// // "OpenSearch": {
	/// //   "NodeUris": [ "http://os01:9200", "http://os02:9200" ],
	/// //   "IndexPrefix": "readmodels",
	/// //   "NumberOfReplicas": 1
	/// // }
	/// services.AddOpenSearchProjectionStore&lt;OrderSummary&gt;(
	///     configuration.GetSection("OpenSearch"),
	///     o =&gt; o.IndexName = "ordersummary");
	/// </code>
	/// </remarks>
	[RequiresUnreferencedCode("Binds configuration to the options type reflectively; configure the store in code for trimming and AOT.")]
	[RequiresDynamicCode("Binds configuration to the options type reflectively; configure the store in code for trimming and AOT.")]
	public static IServiceCollection AddOpenSearchProjectionStore<TProjection>(
		this IServiceCollection services,
		IConfiguration section,
		Action<OpenSearchProjectionStoreOptions>? configureOptions = null)
		where TProjection : class
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(section);

		var optionsName = typeof(TProjection).Name;

		// Bound first, so the code action registered by the call below runs after it and wins.
		_ = services.AddOptions<OpenSearchProjectionStoreOptions>(optionsName).Bind(section);

		return services.AddOpenSearchProjectionStore<TProjection>(configureOptions ?? (static _ => { }));
	}

	/// <summary>
	/// Registers multiple OpenSearch projection stores sharing one configured cluster.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="section">The configuration section describing the shared cluster.</param>
	/// <param name="configure">Action to register individual projection stores.</param>
	/// <returns>The service collection for chaining.</returns>
	/// <remarks>
	/// <para>
	/// The cluster is described once and every projection registered inside <paramref name="configure"/>
	/// binds it, while keeping its own named options -- so a per-projection index name does not leak
	/// across read models.
	/// </para>
	/// <code>
	/// services.AddOpenSearchProjections(
	///     configuration.GetSection("OpenSearch"),
	///     projections =&gt; projections
	///         .Add&lt;OrderSummary&gt;(o =&gt; o.IndexName = "ordersummary")
	///         .Add&lt;CustomerProfile&gt;(o =&gt; o.IndexName = "customerprofile"));
	/// </code>
	/// </remarks>
	[RequiresUnreferencedCode("Binds configuration to the options type reflectively; configure the stores in code for trimming and AOT.")]
	[RequiresDynamicCode("Binds configuration to the options type reflectively; configure the stores in code for trimming and AOT.")]
	public static IServiceCollection AddOpenSearchProjections(
		this IServiceCollection services,
		IConfiguration section,
		Action<OpenSearchProjectionRegistrar> configure)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(section);
		ArgumentNullException.ThrowIfNull(configure);

		var registrar = new OpenSearchProjectionRegistrar(services, section);
		configure(registrar);

		return services;
	}

	/// <summary>
	/// Wires eager (fail-fast at start-up) validation for a named projection-store options instance.
	/// </summary>
	/// <remarks>
	/// The cross-property validator was registered without this, so it ran on the store's first use.
	/// That is the wrong moment for a configuration mistake: for an async projection the first use is
	/// inside a background host, with the events already durable and no request to fail.
	/// </remarks>
	private static void WireOptionsValidation(IServiceCollection services, string optionsName)
	{
		_ = services.AddOptions<OpenSearchProjectionStoreOptions>(optionsName).ValidateOnStart();
		services.TryAddEnumerable(ServiceDescriptor.Singleton<
			IValidateOptions<OpenSearchProjectionStoreOptions>, OpenSearchProjectionStoreOptionsValidator>());
	}
}
