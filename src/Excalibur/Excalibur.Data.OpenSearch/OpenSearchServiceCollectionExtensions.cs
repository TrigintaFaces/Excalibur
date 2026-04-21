// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.CodeAnalysis;

using Excalibur.Data.OpenSearch;

using OpenSearch.Client;

using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Provides extension methods for configuring OpenSearch-related services
/// in the application's dependency injection container.
/// </summary>
public static class OpenSearchServiceCollectionExtensions
{
	/// <summary>
	/// Adds OpenSearch data provider to the service collection using the fluent builder.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="configure">Configuration action for the OpenSearch data builder.</param>
	/// <returns>The service collection for chaining.</returns>
	/// <exception cref="ArgumentNullException">
	/// Thrown when <paramref name="services"/> or <paramref name="configure"/> is null.
	/// </exception>
	/// <example>
	/// <code>
	/// services.AddExcaliburOpenSearch(os =&gt;
	/// {
	///     os.NodeUri(new Uri("http://localhost:9200"))
	///       .IndexPrefix("myapp");
	/// });
	/// </code>
	/// </example>
	[UnconditionalSuppressMessage("AOT", "IL2026:RequiresUnreferencedCode",
		Justification = "Options validation/binding uses reflection by design.")]
	[UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCode",
		Justification = "Configuration binding uses reflection by design.")]
	public static IServiceCollection AddExcaliburOpenSearch(
		this IServiceCollection services,
		Action<IOpenSearchDataBuilder> configure)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configure);

		var osBuilder = new OpenSearchDataBuilder();
		configure(osBuilder);

		RegisterClientFromBuilder(services, osBuilder);

		return services;
	}

	/// <summary>
	/// Registers OpenSearch services using a preconfigured <see cref="OpenSearchClient"/>.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="client">The preconfigured <see cref="OpenSearchClient"/>.</param>
	/// <param name="registry">A delegate to register additional services related to OpenSearch.</param>
	/// <returns>The updated <see cref="IServiceCollection"/>.</returns>
	/// <exception cref="ArgumentNullException">Thrown if <paramref name="client"/> is null.</exception>
	public static IServiceCollection AddOpenSearchServices(
		this IServiceCollection services,
		OpenSearchClient client,
		Action<IServiceCollection>? registry = null)
	{
		ArgumentNullException.ThrowIfNull(client);

		services.TryAddSingleton(client);

		registry?.Invoke(services);

		return services;
	}

	/// <summary>
	/// Registers the OpenSearch client and related services with the dependency injection container,
	/// creating the client from connection settings.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="nodeUri">The URI of the OpenSearch node.</param>
	/// <param name="configureSettings">
	/// An optional delegate to further configure the <see cref="ConnectionSettings"/> before creating the client.
	/// </param>
	/// <param name="registry">A delegate to register additional services related to OpenSearch.</param>
	/// <returns>The updated <see cref="IServiceCollection"/>.</returns>
	/// <exception cref="ArgumentException">Thrown if <paramref name="nodeUri"/> is null or whitespace.</exception>
	public static IServiceCollection AddOpenSearchServices(
		this IServiceCollection services,
		string nodeUri,
		Action<ConnectionSettings>? configureSettings = null,
		Action<IServiceCollection>? registry = null)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(nodeUri);

		services.TryAddSingleton(_ =>
		{
#pragma warning disable CA2000 // ConnectionSettings lifetime managed by OpenSearchClient
			var settings = new ConnectionSettings(new Uri(nodeUri));
#pragma warning restore CA2000

			configureSettings?.Invoke(settings);

			return new OpenSearchClient(settings);
		});

		registry?.Invoke(services);

		return services;
	}

	/// <summary>
	/// Registers the OpenSearch client and related services with the dependency injection container,
	/// creating the client from multiple node URIs with optional configuration.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="nodeUris">The URIs of the OpenSearch cluster nodes.</param>
	/// <param name="configureSettings">
	/// An optional delegate to further configure the <see cref="ConnectionSettings"/> before creating the client.
	/// </param>
	/// <param name="registry">A delegate to register additional services related to OpenSearch.</param>
	/// <returns>The updated <see cref="IServiceCollection"/>.</returns>
	/// <exception cref="ArgumentNullException">Thrown if <paramref name="nodeUris"/> is null.</exception>
	public static IServiceCollection AddOpenSearchServices(
		this IServiceCollection services,
		IEnumerable<Uri> nodeUris,
		Action<ConnectionSettings>? configureSettings = null,
		Action<IServiceCollection>? registry = null)
	{
		ArgumentNullException.ThrowIfNull(nodeUris);

		services.TryAddSingleton(_ =>
		{
#pragma warning disable CA2000 // ConnectionSettings lifetime managed by OpenSearchClient
			var pool = new OpenSearch.Net.StaticConnectionPool(nodeUris);
			var settings = new ConnectionSettings(pool);
#pragma warning restore CA2000

			configureSettings?.Invoke(settings);

			return new OpenSearchClient(settings);
		});

		registry?.Invoke(services);

		return services;
	}

	[UnconditionalSuppressMessage("AOT", "IL2026:RequiresUnreferencedCode",
		Justification = "Options validation/binding uses reflection by design.")]
	[UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCode",
		Justification = "Configuration binding uses reflection by design.")]
	private static void RegisterClientFromBuilder(
		IServiceCollection services,
		OpenSearchDataBuilder osBuilder)
	{
		if (osBuilder.BindConfigurationPath is not null)
		{
			services.AddOptions<OpenSearchConfigurationOptions>()
				.BindConfiguration(osBuilder.BindConfigurationPath)
				.ValidateOnStart();

			services.TryAddSingleton(sp =>
			{
				var config = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<OpenSearchConfigurationOptions>>().Value;

				if (config.Urls?.Any() == true)
				{
#pragma warning disable CA2000 // ConnectionSettings lifetime managed by OpenSearchClient
					var pool = new OpenSearch.Net.StaticConnectionPool(config.Urls);
					var settings = new ConnectionSettings(pool);
#pragma warning restore CA2000
					return new OpenSearchClient(settings);
				}

				if (config.Url is not null)
				{
#pragma warning disable CA2000 // ConnectionSettings lifetime managed by OpenSearchClient
					var settings = new ConnectionSettings(config.Url);
#pragma warning restore CA2000
					return new OpenSearchClient(settings);
				}

				throw new InvalidOperationException(
					"OpenSearch configuration must specify either Urls (for cluster) or Url (for single node).");
			});
		}
		else if (osBuilder.ClientInstance is not null)
		{
			var client = osBuilder.ClientInstance;
			services.TryAddSingleton(client);
		}
		else if (osBuilder.ClientFactoryFunc is not null)
		{
			var factory = osBuilder.ClientFactoryFunc;
			services.TryAddSingleton(factory);
		}
		else if (osBuilder.NodeUrisValue is not null)
		{
			var uris = osBuilder.NodeUrisValue;
			services.TryAddSingleton(_ =>
			{
#pragma warning disable CA2000 // ConnectionSettings lifetime managed by OpenSearchClient
				var pool = new OpenSearch.Net.StaticConnectionPool(uris);
				var settings = new ConnectionSettings(pool);
#pragma warning restore CA2000
				return new OpenSearchClient(settings);
			});
		}
		else if (osBuilder.NodeUriValue is not null)
		{
			var uri = osBuilder.NodeUriValue;
			services.TryAddSingleton(_ =>
			{
#pragma warning disable CA2000 // ConnectionSettings lifetime managed by OpenSearchClient
				var settings = new ConnectionSettings(uri);
#pragma warning restore CA2000
				return new OpenSearchClient(settings);
			});
		}
	}
}
