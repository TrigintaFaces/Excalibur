// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0
using System.Diagnostics.CodeAnalysis;
using Excalibur.EventSourcing;

namespace Excalibur.Data.OpenSearch.Projections;

public sealed partial class OpenSearchProjectionStore<TProjection>
{
    private readonly string? _destinationScope;
    private OpenSearchProjectionStore(OpenSearchProjectionStore<TProjection> source, string destination)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(destination);
        const string prefix = "opensearch/";
        if (!destination.StartsWith(prefix, StringComparison.Ordinal) || destination.Length == prefix.Length
            || destination.AsSpan(prefix.Length).ContainsAny("/,*?"))
        {
            throw new ArgumentException("An exact OpenSearch index destination is required.", nameof(destination));
        }
        var index = destination[prefix.Length..];
        if (index is "." or ".." || index[0] is '_' or '-' or '+'
            || index.Any(character => char.IsWhiteSpace(character) || char.IsUpper(character))
            || index.IndexOfAny(['\\', '\"', '<', '>', '|', '#', ':']) >= 0)
        {
            throw new ArgumentException("The destination must name one concrete index, not an index expression.", nameof(destination));
        }
        _indexName = destination[prefix.Length..];
        _verifiedGenerationIndices = source._verifiedGenerationIndices;
        _options = source._options;
        _logger = source._logger;
        _client = source._client;
        _destinationScope = source._destinationScope;
        _indexVerified = true;
    }

    [RequiresUnreferencedCode("The conventional projection serializer requires reflection.")]
    [RequiresDynamicCode("The conventional projection serializer requires dynamic code.")]
    IProjectionGenerationReader<TProjection> IProjectionGenerationReaderFactory<TProjection>.OpenReader(string destinationIdentity)
    {
        var store = new OpenSearchProjectionStore<TProjection>(this, destinationIdentity);
        return new GenerationReader(store.VerifyConcreteScopeAsync, (store as IServiceProvider).GetService(typeof(IProjectionDestinationScope)) is IProjectionDestinationScope scope ? scope.Scope : null, store.GetByIdAsync, store.QueryAsync, store.CountAsync, () => ValueTask.CompletedTask);
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
    private string? ReadDestinationScope() => _destinationScope;

    private static string? CaptureDestinationScope(global::OpenSearch.Client.IOpenSearchClient client)
    {
        var nodes = client.ConnectionSettings?.ConnectionPool?.Nodes;
        if (nodes is null) { return null; }
        var endpoints = nodes.Select(node => node.Uri.GetComponents(UriComponents.SchemeAndServer | UriComponents.Path, UriFormat.UriEscaped))
            .OrderBy(endpoint => endpoint, StringComparer.Ordinal).ToArray();
        return endpoints.Length == 0 ? null : "opensearch/" + string.Join("|", endpoints);
    }
    private sealed class ConfiguredDestinationScope(string scope) : IProjectionDestinationScope
    {
        public string Scope => scope;
    }

}
