// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: Apache-2.0

using Elastic.Clients.Elasticsearch;

using Excalibur.Data.ElasticSearch;
using Excalibur.EventSourcing.Abstractions;

namespace CdcEventStoreElasticsearch.Api.Models;

/// <summary>
/// Thin wrapper around the framework's <see cref="ElasticSearchCursorHelper"/> for
/// Elasticsearch <c>search_after</c> cursor-based pagination.
/// </summary>
/// <remarks>
/// All encoding, decoding, and result-building logic lives in the framework packages:
/// <list type="bullet">
/// <item><see cref="CursorEncoder"/> — generic Base64url cursor encoding/decoding
/// (in <c>Excalibur.EventSourcing.Abstractions</c>).</item>
/// <item><see cref="ElasticSearchCursorHelper"/> — Elasticsearch-specific
/// <see cref="FieldValue"/> conversion and <c>SearchResponse</c> mapping
/// (in <c>Excalibur.Data.ElasticSearch</c>).</item>
/// </list>
/// This sample helper exists only to keep handler code concise. For new projects,
/// use <see cref="ElasticSearchCursorHelper"/> directly.
/// </remarks>
internal static class CursorHelper
{
    /// <inheritdoc cref="ElasticSearchCursorHelper.DecodeCursor"/>
    public static IList<FieldValue>? DecodeCursor(string? cursor) =>
        ElasticSearchCursorHelper.DecodeCursor(cursor);

    /// <inheritdoc cref="ElasticSearchCursorHelper.ToCursorResult{T}"/>
    public static CursorPagedResult<T> ToCursorResult<T>(
        SearchResponse<T> response,
        int pageSize,
        bool reverseItems = false) =>
        ElasticSearchCursorHelper.ToCursorResult(response, pageSize, reverseItems);
}
