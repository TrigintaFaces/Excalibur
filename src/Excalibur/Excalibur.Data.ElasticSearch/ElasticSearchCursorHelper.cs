// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.Core.Search;

using Excalibur.EventSourcing.Abstractions;

namespace Excalibur.Data.ElasticSearch;

/// <summary>
/// Elasticsearch-specific cursor helpers that bridge the generic <see cref="CursorEncoder"/>
/// with Elasticsearch <see cref="FieldValue"/> sort values and <see cref="SearchResponse{TDocument}"/>.
/// </summary>
/// <remarks>
/// <para>
/// This helper converts between the opaque Base64url cursor format (produced by
/// <see cref="CursorEncoder"/>) and Elasticsearch's native <see cref="FieldValue"/>
/// type used in <c>search_after</c> queries.
/// </para>
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
public static class ElasticSearchCursorHelper
{
	/// <summary>
	/// Decodes an opaque Base64url cursor string into Elasticsearch <see cref="FieldValue"/>
	/// sort values suitable for <c>search_after</c>.
	/// </summary>
	/// <param name="cursor">The opaque cursor string, or <c>null</c> for the first page.</param>
	/// <returns>
	/// A list of <see cref="FieldValue"/> sort values to pass to <c>SearchAfter</c>,
	/// or <c>null</c> if the cursor is empty or invalid.
	/// </returns>
	public static IList<FieldValue>? DecodeCursor(string? cursor)
	{
		var values = CursorEncoder.Decode(cursor);

		if (values is null)
		{
			return null;
		}

		var sortValues = new List<FieldValue>(values.Length);

		for (var i = 0; i < values.Length; i++)
		{
			sortValues.Add(ToFieldValue(values[i]));
		}

		return sortValues;
	}

	/// <summary>
	/// Encodes Elasticsearch <see cref="FieldValue"/> sort values into an opaque
	/// Base64url cursor string.
	/// </summary>
	/// <param name="sortValues">
	/// The sort values from an Elasticsearch hit (e.g., <c>hit.Sort</c>).
	/// </param>
	/// <returns>A Base64url-encoded cursor string.</returns>
	/// <exception cref="ArgumentNullException">
	/// Thrown when <paramref name="sortValues"/> is <c>null</c>.
	/// </exception>
	/// <exception cref="ArgumentException">
	/// Thrown when <paramref name="sortValues"/> is empty.
	/// </exception>
	public static string EncodeCursor(IReadOnlyCollection<FieldValue> sortValues)
	{
		ArgumentNullException.ThrowIfNull(sortValues);

		if (sortValues.Count == 0)
		{
			throw new ArgumentException("Sort values must not be empty.", nameof(sortValues));
		}

		var primitives = new object?[sortValues.Count];
		var i = 0;

		foreach (var value in sortValues)
		{
			primitives[i++] = FromFieldValue(value);
		}

		return CursorEncoder.Encode(primitives);
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
				nextCursor = EncodeCursor(boundaryHit.Sort);
			}
		}

		return new CursorPagedResult<T>(
			documents,
			pageSize,
			response.Total,
			nextCursor);
	}

	/// <summary>
	/// Converts a primitive value (from <see cref="CursorEncoder.Decode"/>) to an
	/// Elasticsearch <see cref="FieldValue"/>.
	/// </summary>
	private static FieldValue ToFieldValue(object? value) => value switch
	{
		string s => FieldValue.String(s),
		long l => FieldValue.Long(l),
		int i => FieldValue.Long(i),
		double d => FieldValue.Double(d),
		float f => FieldValue.Double(f),
		bool b => b ? FieldValue.True : FieldValue.False,
		null => FieldValue.Null,
		_ => FieldValue.String(value.ToString()!)
	};

	/// <summary>
	/// Converts an Elasticsearch <see cref="FieldValue"/> to a primitive value
	/// suitable for <see cref="CursorEncoder.Encode"/>.
	/// </summary>
	private static object? FromFieldValue(FieldValue value)
	{
		var s = value.ToString();

		return s switch
		{
			"True" => true,
			"False" => false,
			"Null" => null,
			_ when long.TryParse(s, out var l) => l,
			_ when double.TryParse(s, out var d) => d,
			_ => s
		};
	}
}
