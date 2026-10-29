// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0
using System.Diagnostics.CodeAnalysis;
using Excalibur.EventSourcing;


namespace Excalibur.Data.CosmosDb.Projections;

public sealed partial class CosmosDbProjectionStore<TProjection>
{
    private CosmosDbProjectionStore(CosmosDbProjectionStore<TProjection> source, string destination)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(destination);
        var parts = destination.Split('/');
        if (parts.Length != 4 || parts[0] != "cosmosdb" || string.IsNullOrWhiteSpace(parts[1]) || string.IsNullOrWhiteSpace(parts[2]))
        {
            throw new ArgumentException("Expected an exact cosmosdb destination identity.", nameof(destination));
        }

        _options = source._options;
        _logger = source._logger;
        _projectionType = parts[3];
        _containerName = parts[2];
        _jsonOptions = source._jsonOptions;
        _client = source._client;
        if (_client is null)
        {
            _client = CreateClient(CreateClientOptions());
            _ownsClient = true;
        }
        _container = _client.GetDatabase(parts[1]).GetContainer(parts[2]);
        _initialized = true;

    }

    [SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification = "Ownership transfers to the returned reader; its DisposeAsync invokes this clone's DisposeAsync. Borrowed host clients are not disposed.")]
    [RequiresUnreferencedCode("The conventional projection serializer requires reflection.")]
    [RequiresDynamicCode("The conventional projection serializer requires dynamic code.")]
    IProjectionGenerationReader<TProjection> IProjectionGenerationReaderFactory<TProjection>.OpenReader(string destinationIdentity)
    {
        var store = new CosmosDbProjectionStore<TProjection>(this, destinationIdentity);
        return new GenerationReader((store as IServiceProvider).GetService(typeof(IProjectionDestinationScope)) is IProjectionDestinationScope scope ? scope.Scope : null, store.GetByIdAsync, store.QueryAsync, store.CountAsync, store.DisposeAsync);
    }

    private sealed class GenerationReader(
        string? destinationScope,
        Func<string, CancellationToken, Task<TProjection?>> get,
        Func<IDictionary<string, object>?, QueryOptions?, CancellationToken, Task<IReadOnlyList<TProjection>>> query,
        Func<IDictionary<string, object>?, CancellationToken, Task<long>> count, Func<ValueTask> dispose) : IProjectionGenerationReader<TProjection>
    {
        public string? DestinationScope => destinationScope;
        public Task<TProjection?> GetByIdAsync(string id, CancellationToken cancellationToken) => get(id, cancellationToken);
        public Task<IReadOnlyList<TProjection>> QueryAsync(IDictionary<string, object>? filters, QueryOptions? options, CancellationToken cancellationToken) => query(filters, options, cancellationToken);
        public Task<long> CountAsync(IDictionary<string, object>? filters, CancellationToken cancellationToken) => count(filters, cancellationToken);
        public ValueTask DisposeAsync() => dispose();
    }    private string? ReadDestinationScope()
    {
        var endpoint = _client?.Endpoint?.AbsoluteUri ?? _options.Client.AccountEndpoint;
        if (_client is null && !string.IsNullOrWhiteSpace(_options.Client.ConnectionString))
        {
            var settings = new System.Data.Common.DbConnectionStringBuilder { ConnectionString = _options.Client.ConnectionString };
            endpoint = settings.TryGetValue("AccountEndpoint", out var value) ? value as string : null;
        }
        return Uri.TryCreate(endpoint, UriKind.Absolute, out var uri)
            ? "cosmosdb/" + uri.GetComponents(UriComponents.SchemeAndServer | UriComponents.Path, UriFormat.UriEscaped) : null;
    }
    private sealed class ConfiguredDestinationScope(string scope) : IProjectionDestinationScope
    {
        public string Scope => scope;
    }

}
