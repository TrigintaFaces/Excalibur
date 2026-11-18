// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Text.Json.Serialization.Metadata;

using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.Serialization;

namespace Excalibur.Outbox.ElasticSearch;

/// <summary>
/// Creates the SDK source serializer with metadata for the framework's persisted outbox document.
/// </summary>
public static class ElasticsearchOutboxSerializer
{
    /// <summary>
    /// Creates a source serializer without an implicit reflection fallback.
    /// </summary>
    /// <param name="settings">The settings supplied by the SDK source-serializer factory.</param>
    /// <param name="additionalTypes">Optional metadata for application header values and other shared-client documents.</param>
    /// <returns>The SDK serializer retaining its built-in converters and JSON defaults.</returns>
    /// <remarks>
    /// Framework document metadata takes precedence. Supply generated metadata for custom header types
    /// in trimmed and NativeAOT applications. An explicitly supplied reflection resolver retains managed
    /// arbitrary-object support and carries that resolver's trimming and dynamic-code requirements.
    /// Missing metadata is an error; values are never dropped or converted to strings.
    /// Configure a borrowed client with this helper at its original construction: the store does not
    /// replace its source serializer or take ownership of its transport.
    /// </remarks>
    public static DefaultSourceSerializer Create(IElasticsearchClientSettings settings, IJsonTypeInfoResolver? additionalTypes = null)
    {
        ArgumentNullException.ThrowIfNull(settings);
        var resolver = additionalTypes is null
            ? ElasticsearchOutboxJsonContext.Default
            : JsonTypeInfoResolver.Combine(ElasticsearchOutboxJsonContext.Default, additionalTypes);
        return new DefaultSourceSerializer(settings, resolver);
    }
}
