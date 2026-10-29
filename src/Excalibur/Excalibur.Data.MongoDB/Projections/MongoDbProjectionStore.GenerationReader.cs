// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0
using System.Diagnostics.CodeAnalysis;
using Excalibur.EventSourcing;
using MongoDB.Driver;
using MongoDB.Bson;

namespace Excalibur.Data.MongoDB.Projections;

public sealed partial class MongoDbProjectionStore<TProjection>
{
    private MongoDbProjectionStore(MongoDbProjectionStore<TProjection> source, string destination)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(destination);
        var parts = destination.Split('/');
        if (parts.Length != 4 || parts[0] != "mongodb")
        {
            throw new ArgumentException("Expected an exact mongodb destination identity.", nameof(destination));
        }

        _options = source._options;
        _logger = source._logger;
        var databaseName = DecodeDestinationSegment(parts[1]);
        _collectionName = DecodeDestinationSegment(parts[2]);
        _projectionType = DecodeDestinationSegment(parts[3]);
        _client = source._client;
        if (_client is null)
        {
            var settings = MongoClientSettings.FromConnectionString(_options.ConnectionString);
            settings.ServerSelectionTimeout = TimeSpan.FromSeconds(_options.ServerSelectionTimeoutSeconds);
            settings.ConnectTimeout = TimeSpan.FromSeconds(_options.ConnectTimeoutSeconds);
            settings.MaxConnectionPoolSize = _options.MaxPoolSize;
            settings.UseTls = _options.UseSsl;
            _client = new MongoClient(settings);
            _ownsClient = true;
        }
        _database = _client.GetDatabase(databaseName);
        _collection = _database.GetCollection<BsonDocument>(_collectionName);
        _initialized = true;

    }

    private static string DecodeDestinationSegment(string encoded)
    {
        var decoded = Uri.UnescapeDataString(encoded);
        if (string.IsNullOrWhiteSpace(decoded) || !string.Equals(Uri.EscapeDataString(decoded), encoded, StringComparison.Ordinal))
        {
            throw new ArgumentException("A projection destination must contain canonical escaped database, collection and type segments.", nameof(encoded));
        }
        return decoded;
    }

    [SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification = "Ownership transfers to the returned reader; its DisposeAsync invokes this clone's DisposeAsync. Borrowed host clients are not disposed.")]
    [RequiresUnreferencedCode("The conventional projection serializer requires reflection.")]
    [RequiresDynamicCode("The conventional projection serializer requires dynamic code.")]
    IProjectionGenerationReader<TProjection> IProjectionGenerationReaderFactory<TProjection>.OpenReader(string destinationIdentity)
    {
        var store = new MongoDbProjectionStore<TProjection>(this, destinationIdentity);
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
        var settings = _client?.Settings ?? MongoClientSettings.FromConnectionString(_options.ConnectionString);
        if (settings.Servers is null) { return null; }
        var servers = settings.Servers.Select(server => server.ToString()).OrderBy(server => server, StringComparer.Ordinal);
        return "mongodb/" + string.Join(",", servers.Select(Uri.EscapeDataString)) + "/" + Uri.EscapeDataString(settings.ReplicaSetName ?? string.Empty);
    }
    private sealed class ConfiguredDestinationScope(string scope) : IProjectionDestinationScope
    {
        public string Scope => scope;
    }

}
