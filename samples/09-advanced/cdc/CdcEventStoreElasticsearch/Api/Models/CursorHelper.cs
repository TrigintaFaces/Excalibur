// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;

using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.Core.Search;

using Excalibur.EventSourcing.Abstractions;

namespace CdcEventStoreElasticsearch.Api.Models;

/// <summary>
/// Utility for encoding and decoding opaque cursors for Elasticsearch
/// <c>search_after</c> pagination.
/// </summary>
/// <remarks>
/// <para>
/// Cursors are Base64url-encoded JSON arrays of the Elasticsearch sort values
/// from the last hit in a search response. This makes them safe to pass as
/// query parameters without additional URL encoding.
/// </para>
/// <para>
/// The cursor is opaque to consumers — they should not parse or construct it.
/// The encoding format is an implementation detail that may change.
/// </para>
/// </remarks>
internal static class CursorHelper
{
    /// <summary>
    /// Decodes a Base64url cursor string into Elasticsearch <see cref="FieldValue"/> sort values.
    /// </summary>
    /// <param name="cursor">The opaque cursor string, or <c>null</c> for the first page.</param>
    /// <returns>
    /// A list of <see cref="FieldValue"/> sort values to pass to <c>SearchAfter</c>,
    /// or <c>null</c> if the cursor is empty or invalid.
    /// </returns>
    public static IList<FieldValue>? DecodeCursor(string? cursor)
    {
        if (string.IsNullOrWhiteSpace(cursor))
        {
            return null;
        }

        try
        {
            var json = Convert.FromBase64String(ToBase64(cursor));
            var elements = JsonSerializer.Deserialize<JsonElement[]>(json);

            if (elements is null || elements.Length == 0)
            {
                return null;
            }

            var sortValues = new List<FieldValue>(elements.Length);

            foreach (var element in elements)
            {
                sortValues.Add(element.ValueKind switch
                {
                    JsonValueKind.String => FieldValue.String(element.GetString()!),
                    JsonValueKind.Number when element.TryGetInt64(out var l) => FieldValue.Long(l),
                    JsonValueKind.Number => FieldValue.Double(element.GetDouble()),
                    JsonValueKind.True => FieldValue.True,
                    JsonValueKind.False => FieldValue.False,
                    JsonValueKind.Null => FieldValue.Null,
                    _ => FieldValue.String(element.GetRawText())
                });
            }

            return sortValues;
        }
        catch (Exception)
        {
            // Invalid cursor — treat as first page
            return null;
        }
    }

    /// <summary>
    /// Builds a <see cref="CursorPagedResult{T}"/> from an Elasticsearch search response.
    /// </summary>
    /// <typeparam name="T">The document type.</typeparam>
    /// <param name="response">The Elasticsearch search response.</param>
    /// <param name="pageSize">The requested page size.</param>
    /// <param name="reverseItems">
    /// When <c>true</c>, reverses the document order before returning.
    /// Used for <see cref="PageNavigation.Previous"/> and <see cref="PageNavigation.Last"/>
    /// navigation, where the repository reversed the sort order to fetch the correct page
    /// and the handler must restore the expected display order.
    /// </param>
    /// <returns>A cursor-paged result with an encoded cursor for the next page.</returns>
    /// <remarks>
    /// <para>
    /// For bidirectional cursor pagination, the cursor is always derived from the
    /// <b>last hit in display order</b> (after any reversal). This means:
    /// </para>
    /// <list type="bullet">
    /// <item><b>Next/First</b>: cursor from the last hit (natural order).</item>
    /// <item><b>Previous/Last</b>: items are reversed, then cursor from the last
    /// item in display order (which was the first hit from the reversed ES query).</item>
    /// </list>
    /// </remarks>
    public static CursorPagedResult<T> ToCursorResult<T>(
        SearchResponse<T> response,
        int pageSize,
        bool reverseItems = false)
    {
        var hits = response.Hits;
        var documents = response.Documents.ToList();

        if (reverseItems)
        {
            documents.Reverse();
        }

        string? nextCursor = null;

        // If we got a full page of results, there may be more — encode the boundary hit's sort values.
        // For reversed results, the "next" cursor uses the first hit (last in display order after reverse).
        // For normal results, it uses the last hit.
        if (hits.Count >= pageSize)
        {
            var boundaryHit = reverseItems ? hits.First() : hits.Last();
            if (boundaryHit.Sort is { Count: > 0 })
            {
                nextCursor = EncodeSortValues(boundaryHit.Sort);
            }
        }

        return new CursorPagedResult<T>(
            documents,
            pageSize,
            response.Total,
            nextCursor);
    }

    /// <summary>
    /// Encodes Elasticsearch sort values into a Base64url cursor string.
    /// </summary>
    private static string EncodeSortValues(IReadOnlyCollection<FieldValue> sortValues)
    {
        // Convert FieldValue objects to JSON-serializable primitives
        var primitives = new object?[sortValues.Count];
        var i = 0;

        foreach (var value in sortValues)
        {
            primitives[i++] = value.ToString() switch
            {
                "True" => true,
                "False" => false,
                "Null" => null,
                var s when long.TryParse(s, out var l) => l,
                var s when double.TryParse(s, out var d) => d,
                var s => s
            };
        }

        var json = JsonSerializer.SerializeToUtf8Bytes(primitives);
        return ToBase64Url(Convert.ToBase64String(json));
    }

    /// <summary>
    /// Converts standard Base64 to Base64url (URL-safe, no padding).
    /// </summary>
    private static string ToBase64Url(string base64) =>
        base64.TrimEnd('=').Replace('+', '-').Replace('/', '_');

    /// <summary>
    /// Converts Base64url back to standard Base64 for decoding.
    /// </summary>
    private static string ToBase64(string base64Url)
    {
        var s = base64Url.Replace('-', '+').Replace('_', '/');

        // Restore padding
        return (s.Length % 4) switch
        {
            2 => s + "==",
            3 => s + "=",
            _ => s
        };
    }
}
