// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0
using System.Diagnostics.CodeAnalysis;
using Excalibur.EventSourcing;
using System.Text.Json.Serialization.Metadata;
using Elastic.Clients.Elasticsearch;
using Elastic.Transport.Extensions;

namespace Excalibur.Data.ElasticSearch.Projections;

public sealed partial class ElasticSearchProjectionStore<TProjection>
{
    private ElasticSearchProjectionStore(ElasticSearchProjectionStore<TProjection> source, string destination)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(destination);
        const string prefix = "elasticsearch/";
        if (!destination.StartsWith(prefix, StringComparison.Ordinal) || destination.Length == prefix.Length
            || destination.AsSpan(prefix.Length).ContainsAny("/,*?"))
        {
            throw new ArgumentException("An exact ElasticSearch index destination is required.", nameof(destination));
        }
        var index = destination[prefix.Length..];
        if (index is "." or ".." || index[0] is '_' or '-' or '+'
            || index.Any(character => char.IsWhiteSpace(character) || char.IsUpper(character))
            || index.IndexOfAny(['\\', '\"', '<', '>', '|', '#', ':']) >= 0)
        {
            throw new ArgumentException("The destination must name one concrete index, not an index expression.", nameof(destination));
        }
        _indexName = destination[prefix.Length..];
        ObjectDisposedException.ThrowIf(source._disposed, source);
        _verifiedGenerationIndices = source._verifiedGenerationIndices;
        _options = source._options;
        _logger = source._logger;
        _projectionType = source._projectionType;
        _declaredMapping = source._declaredMapping;
        _fieldDefinitions = source._fieldDefinitions;
        _configuredClientSettings = source._configuredClientSettings;
        _sourceContext = source._sourceContext;
        _destinationScope = source._destinationScope;
        _client = source._client ?? new ElasticsearchClient(_configuredClientSettings!);
        _initialized = true;
    }

    [RequiresUnreferencedCode("The conventional projection serializer requires reflection.")]
    [RequiresDynamicCode("The conventional projection serializer requires dynamic code.")]
    IProjectionGenerationReader<TProjection> IProjectionGenerationReaderFactory<TProjection>.OpenReader(string destinationIdentity)
    {
        var store = new ElasticSearchProjectionStore<TProjection>(this, destinationIdentity);
        return new GenerationReader(store.VerifyConcreteScopeAsync, (store as IServiceProvider).GetService(typeof(IProjectionDestinationScope)) is IProjectionDestinationScope scope ? scope.Scope : null, store.GetByIdAsync, store.QueryAsync, store.CountAsync, store.DisposeAsync);
    }

    IProjectionGenerationReader<TProjection> IAotProjectionGenerationReaderFactory<TProjection>.OpenReader(string destinationIdentity, JsonTypeInfo<TProjection> typeInfo)
    {
        ArgumentNullException.ThrowIfNull(typeInfo);
        // Typed search responses use the client's source serializer. Never silently substitute
        // reflection or metadata different from the explicitly configured generated contract.
        if (!ReferenceEquals(_sourceContext?.GetTypeInfo(typeof(TProjection)), typeInfo)
            || !(_client?.SourceSerializer ?? ((IElasticsearchClientSettings)_configuredClientSettings!).SourceSerializer).TryGetJsonSerializerOptions(out var sourceOptions)
            || sourceOptions.GetTypeInfo(typeof(TProjection)) is not JsonTypeInfo<TProjection> effectiveTypeInfo)
        {
            throw new NotSupportedException("Configure the Elasticsearch client with the same generated JSON context used by the generation reader.");
        }
        var store = new ElasticSearchProjectionStore<TProjection>(this, destinationIdentity);
        return new GenerationReader(store.VerifyConcreteScopeAsync, (store as IServiceProvider).GetService(typeof(IProjectionDestinationScope)) is IProjectionDestinationScope scope ? scope.Scope : null, (id, token) => store.GetByIdAsyncCore(id, token, effectiveTypeInfo),
            store.QueryCoreAsync, store.CountAsync, store.DisposeAsync);
    }

    private sealed class GenerationReader(
        Func<CancellationToken, ValueTask<string>> verifyScope,
        string? destinationScope,
        Func<string, CancellationToken, Task<TProjection?>> get,
        Func<IDictionary<string, object>?, QueryOptions?, CancellationToken, Task<IReadOnlyList<TProjection>>> query,
        Func<IDictionary<string, object>?, CancellationToken, Task<long>> count,
        Func<ValueTask> dispose) : IProjectionGenerationReader<TProjection>
    {
                // The scope the handle has actually resolved. The contract's null means "not resolved yet",
        // never "cannot be established": an unresolvable scope throws from the resolve instead.
        private string? resolved;
        public string? DestinationScope => resolved ?? destinationScope;
        public async ValueTask<string> GetDestinationScopeAsync(CancellationToken cancellationToken) => resolved = await verifyScope(cancellationToken).ConfigureAwait(false);
        public async Task<TProjection?> GetByIdAsync(string id, CancellationToken cancellationToken) { resolved = await verifyScope(cancellationToken).ConfigureAwait(false); return await get(id, cancellationToken).ConfigureAwait(false); }
        public async Task<IReadOnlyList<TProjection>> QueryAsync(IDictionary<string, object>? filters, QueryOptions? options, CancellationToken cancellationToken) { resolved = await verifyScope(cancellationToken).ConfigureAwait(false); return await query(filters, options, cancellationToken).ConfigureAwait(false); }
        public async Task<long> CountAsync(IDictionary<string, object>? filters, CancellationToken cancellationToken) { resolved = await verifyScope(cancellationToken).ConfigureAwait(false); return await count(filters, cancellationToken).ConfigureAwait(false); }
        public ValueTask DisposeAsync() => dispose();
    }
}
