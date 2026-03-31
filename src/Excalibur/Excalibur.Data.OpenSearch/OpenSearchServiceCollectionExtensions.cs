// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

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
}
