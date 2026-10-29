// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0
using Excalibur.EventSourcing;

namespace Excalibur.Data.ElasticSearch.Projections;

public sealed partial class ElasticSearchProjectionStore<TProjection>
{
    private readonly HashSet<string> _verifiedGenerationIndices = new(StringComparer.Ordinal);

    private async ValueTask<string> VerifyConcreteScopeAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);
        var scope = _destinationScope ?? throw new InvalidOperationException("The Elasticsearch client's storage scope is unknown.");
        lock (_verifiedGenerationIndices) { if (_verifiedGenerationIndices.Contains(_indexName)) { return scope; } }
        var response = await _client!.Indices.GetMappingAsync(_indexName, cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        if (!response.IsValidResponse || response.Mappings.Count != 1 || !response.Mappings.ContainsKey(_indexName))
        {
            throw new InvalidOperationException($"Projection generation '{_indexName}' must be an existing concrete index. Aliases and data streams cannot be generation destinations.");
        }
        lock (_verifiedGenerationIndices) { if (_verifiedGenerationIndices.Count < 128) { _verifiedGenerationIndices.Add(_indexName); } }
        return scope;
    }

    private sealed class ConcreteDestinationScope(ElasticSearchProjectionStore<TProjection> store) : IAsyncProjectionDestinationScope
    {
        public ValueTask<string> GetScopeAsync(CancellationToken cancellationToken) => store.VerifyConcreteScopeAsync(cancellationToken);
    }
}
