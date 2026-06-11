// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.Core.Search;

using Excalibur.EventSourcing;

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
	/// Applies cursor-based paging to an Elasticsearch search request: sets the page
	/// size with a single extra "peek" row (<c>pageSize + 1</c>) and applies the
	/// <c>search_after</c> cursor when one is supplied.
	/// </summary>
	/// <typeparam name="T">The document type.</typeparam>
	/// <param name="descriptor">The search request descriptor to configure.</param>
	/// <param name="pageSize">The requested page size. Must be greater than zero.</param>
	/// <param name="searchAfter">
	/// The decoded cursor sort values for <c>search_after</c>, or <c>null</c>/empty for
	/// the first page. For <see cref="PageNavigation.First"/> and
	/// <see cref="PageNavigation.Last"/> (which navigate without a cursor), pass <c>null</c>.
	/// </param>
	/// <returns>The same <paramref name="descriptor"/> instance, to allow chaining.</returns>
	/// <exception cref="ArgumentNullException">Thrown when <paramref name="descriptor"/> is <c>null</c>.</exception>
	/// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="pageSize"/> is less than one.</exception>
	/// <remarks>
	/// The extra peek row is what allows <see cref="ToCursorResult{T}"/> to reliably report
	/// <see cref="CursorPagedResult{T}.HasMore"/> — including the boundary case where the
	/// final page is exactly <paramref name="pageSize"/> items. The peek row is never
	/// returned to callers; <see cref="ToCursorResult{T}"/> trims it. Always pair this
	/// method with <see cref="ToCursorResult{T}"/>; do not set <c>Size(pageSize)</c> manually.
	/// </remarks>
	public static SearchRequestDescriptor<T> ApplyCursorPaging<T>(
		SearchRequestDescriptor<T> descriptor,
		int pageSize,
		IList<FieldValue>? searchAfter = null)
	{
		ArgumentOutOfRangeException.ThrowIfLessThan(pageSize, 1);

		// Over-fetch a single peek row so ToCursorResult can detect a further page.
		descriptor.Size(pageSize + 1);

		if (searchAfter is { Count: > 0 })
		{
			descriptor.SearchAfter(searchAfter);
		}

		return descriptor;
	}

	/// <summary>
	/// Builds a bidirectional <see cref="CursorPagedResult{T}"/> from an Elasticsearch
	/// search response that was issued with a single extra "peek" row
	/// (see <see cref="ApplyCursorPaging{T}"/>).
	/// </summary>
	/// <typeparam name="T">The document type.</typeparam>
	/// <param name="response">The Elasticsearch search response.</param>
	/// <param name="pageSize">The requested page size (excluding the peek row).</param>
	/// <param name="navigation">
	/// The navigation direction the query was issued for. For
	/// <see cref="PageNavigation.Previous"/> and <see cref="PageNavigation.Last"/> the
	/// repository must have reversed the sort order; this method restores display order
	/// and assigns the forward/backward cursors accordingly.
	/// </param>
	/// <returns>
	/// A cursor-paged result populated with both <see cref="CursorPagedResult{T}.NextCursor"/>
	/// (forward) and <see cref="CursorPagedResult{T}.PreviousCursor"/> (backward) so the
	/// consumer can navigate in either direction.
	/// </returns>
	/// <exception cref="ArgumentNullException">Thrown when <paramref name="response"/> is <c>null</c>.</exception>
	/// <remarks>
	/// <para>
	/// This method assumes the query over-fetched one row (<c>Size = pageSize + 1</c>) via
	/// <see cref="ApplyCursorPaging{T}"/>. The presence of that extra "peek" row — not a full
	/// page — determines whether a further page exists in the direction of travel, fixing the
	/// boundary case where the final page contains exactly <paramref name="pageSize"/> items.
	/// The peek row is trimmed and never returned to the caller.
	/// </para>
	/// <para>
	/// Cursor assignment, in display order (the first/last item the caller sees):
	/// <see cref="CursorPagedResult{T}.NextCursor"/> is the sort values of the <b>last</b>
	/// displayed item (used to fetch the following page going forward), and
	/// <see cref="CursorPagedResult{T}.PreviousCursor"/> is the sort values of the <b>first</b>
	/// displayed item (used to fetch the preceding page via a reversed query). A cursor is
	/// <c>null</c> only when that direction has no further page: <c>PreviousCursor</c> is
	/// <c>null</c> on the first page, <c>NextCursor</c> is <c>null</c> on the last page.
	/// </para>
	/// </remarks>
	public static CursorPagedResult<T> ToCursorResult<T>(
		SearchResponse<T> response,
		int pageSize,
		PageNavigation navigation)
	{
		ArgumentNullException.ThrowIfNull(response);

		var hits = response.Hits as IReadOnlyList<Hit<T>> ?? response.Hits.ToList();

		// Project the per-hit sort values (in raw query order) and resolve the
		// forward/backward cursors with the pure, store-agnostic core below.
		var sortValuesPerHit = new IReadOnlyCollection<FieldValue>?[hits.Count];
		for (var i = 0; i < hits.Count; i++)
		{
			sortValuesPerHit[i] = hits[i].Sort;
		}

		var (keptCount, reverseItems, nextCursor, previousCursor) =
			ResolveCursorBoundaries(sortValuesPerHit, pageSize, navigation);

		var documents = response.Documents.Take(keptCount).ToList();

		if (reverseItems)
		{
			documents.Reverse();
		}

		return new CursorPagedResult<T>(
			documents,
			pageSize,
			response.Total,
			nextCursor,
			previousCursor);
	}

	/// <summary>
	/// Pure, store-agnostic core of <see cref="ToCursorResult{T}"/>: given the sort
	/// values of each hit (in raw query order), the requested page size, and the
	/// navigation direction, decides how many hits belong to the page, whether to
	/// reverse them for display, and computes the forward/backward cursors.
	/// </summary>
	/// <param name="sortValuesPerHit">
	/// Sort values per hit in raw Elasticsearch query order (length up to
	/// <paramref name="pageSize"/> + 1 when the query over-fetched a peek row).
	/// </param>
	/// <param name="pageSize">The requested page size (excluding the peek row).</param>
	/// <param name="navigation">The navigation direction the query was issued for.</param>
	/// <returns>
	/// The number of hits to keep, whether display order must be reversed, and the
	/// encoded forward (<c>NextCursor</c>) and backward (<c>PreviousCursor</c>) cursors.
	/// </returns>
	/// <remarks>
	/// Extracted so the cursor logic (the subtle part) is unit-testable without
	/// constructing an Elasticsearch <see cref="SearchResponse{T}"/>. See
	/// <see cref="ToCursorResult{T}"/> for the full semantics and the
	/// over-fetch/peek convention from <see cref="ApplyCursorPaging{T}"/>.
	/// </remarks>
	internal static (int KeptCount, bool ReverseItems, string? NextCursor, string? PreviousCursor) ResolveCursorBoundaries(
		IReadOnlyList<IReadOnlyCollection<FieldValue>?> sortValuesPerHit,
		int pageSize,
		PageNavigation navigation)
	{
		ArgumentNullException.ThrowIfNull(sortValuesPerHit);

		var reverseItems = navigation is PageNavigation.Previous or PageNavigation.Last;

		// The query over-fetches a single "peek" row (Size = pageSize + 1) so we can
		// reliably tell whether another page exists in the direction of travel — even
		// when the boundary page holds exactly `pageSize` items. The peek is dropped.
		var hasPeek = sortValuesPerHit.Count > pageSize;
		var keptCount = hasPeek ? pageSize : sortValuesPerHit.Count;

		string? nextCursor = null;
		string? previousCursor = null;

		if (keptCount > 0)
		{
			// Raw hits are in query order; for a reversed query the display order is flipped.
			//   first displayed item  -> backward (Previous) boundary
			//   last  displayed item  -> forward  (Next) boundary
			var firstDisplay = reverseItems ? sortValuesPerHit[keptCount - 1] : sortValuesPerHit[0];
			var lastDisplay = reverseItems ? sortValuesPerHit[0] : sortValuesPerHit[keptCount - 1];

			// The "peek" reports the far side of the direction of travel:
			//   forward query  -> peek means another page exists going forward
			//   reversed query -> peek means another page exists going backward
			// The near side is implied by the navigation that produced this page:
			// First has no previous; Last has no next.
			var hasNext = reverseItems ? navigation is not PageNavigation.Last : hasPeek;
			var hasPrevious = reverseItems ? hasPeek : navigation is not PageNavigation.First;

			if (hasNext && lastDisplay is { Count: > 0 })
			{
				nextCursor = EncodeCursor(lastDisplay);
			}

			if (hasPrevious && firstDisplay is { Count: > 0 })
			{
				previousCursor = EncodeCursor(firstDisplay);
			}
		}

		return (keptCount, reverseItems, nextCursor, previousCursor);
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
