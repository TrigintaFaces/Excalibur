// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0
using System.Text.Json;
using Excalibur.EventSourcing;
using OpenSearch.Net;

namespace Excalibur.Data.OpenSearch.Projections;

public sealed partial class OpenSearchProjectionStore<TProjection>
{
    private readonly HashSet<string> _verifiedGenerationIndices = new(StringComparer.Ordinal);

    private async ValueTask<string> VerifyConcreteScopeAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await EnsureIndexAsync(cancellationToken).ConfigureAwait(false);
        var scope = ReadDestinationScope() ?? throw new InvalidOperationException("The OpenSearch client's storage scope is unknown.");
        lock (_verifiedGenerationIndices) { if (_verifiedGenerationIndices.Contains(_indexName)) { return scope; } }
        var response = await _client.LowLevel.DoRequestAsync<StringResponse>(global::OpenSearch.Net.HttpMethod.GET,
            Uri.EscapeDataString(_indexName) + "/_mapping", cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        if (!response.Success) { throw new InvalidOperationException($"Cannot verify concrete projection index '{_indexName}'."); }
        using var document = JsonDocument.Parse(response.Body);
        if (document.RootElement.ValueKind != JsonValueKind.Object
            || document.RootElement.EnumerateObject().Count() != 1 || !document.RootElement.TryGetProperty(_indexName, out _))
        {
            throw new InvalidOperationException($"Projection generation '{_indexName}' must be an existing concrete index. Aliases and data streams cannot be generation destinations.");
        }
        lock (_verifiedGenerationIndices) { if (_verifiedGenerationIndices.Count < 128) { _verifiedGenerationIndices.Add(_indexName); } }
        return scope;
    }

    private sealed class ConcreteDestinationScope(OpenSearchProjectionStore<TProjection> store) : IAsyncProjectionDestinationScope
    {
        public ValueTask<string> GetScopeAsync(CancellationToken cancellationToken) => store.VerifyConcreteScopeAsync(cancellationToken);
    }
}
