// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.Core.Search;

using Excalibur.Data.ElasticSearch;
using Excalibur.EventSourcing;

namespace Excalibur.Data.ElasticSearch.Tests.ElasticSearch;

[UnitTest]
public sealed class ElasticSearchCursorHelperShould
{
	// ─── DecodeCursor ───────────────────────────────────────────────────

	[Fact]
	public void ReturnNull_WhenDecodingNullCursor()
	{
		ElasticSearchCursorHelper.DecodeCursor(null).ShouldBeNull();
	}

	[Fact]
	public void ReturnNull_WhenDecodingEmptyCursor()
	{
		ElasticSearchCursorHelper.DecodeCursor("").ShouldBeNull();
	}

	[Fact]
	public void ReturnNull_WhenDecodingInvalidCursor()
	{
		ElasticSearchCursorHelper.DecodeCursor("garbage").ShouldBeNull();
	}

	[Fact]
	public void DecodeStringFieldValues()
	{
		var encoded = CursorEncoder.Encode("order-123");
		var result = ElasticSearchCursorHelper.DecodeCursor(encoded);

		result.ShouldNotBeNull();
		result.Count.ShouldBe(1);
		// FieldValue.String should contain the string value
		result[0].ToString().ShouldBe("order-123");
	}

	[Fact]
	public void DecodeLongFieldValues()
	{
		var encoded = CursorEncoder.Encode(42L);
		var result = ElasticSearchCursorHelper.DecodeCursor(encoded);

		result.ShouldNotBeNull();
		result.Count.ShouldBe(1);
	}

	[Fact]
	public void DecodeBoolFieldValues()
	{
		var encoded = CursorEncoder.Encode(true);
		var result = ElasticSearchCursorHelper.DecodeCursor(encoded);

		result.ShouldNotBeNull();
		result.Count.ShouldBe(1);
	}

	[Fact]
	public void DecodeNullFieldValues()
	{
		// Encode an array containing a null element (not a null array)
		var encoded = CursorEncoder.Encode(new object?[] { null });
		var result = ElasticSearchCursorHelper.DecodeCursor(encoded);

		result.ShouldNotBeNull();
		result.Count.ShouldBe(1);
	}

	[Fact]
	public void DecodeMultipleSortValues()
	{
		var encoded = CursorEncoder.Encode("status-active", 1000L, 42.5);
		var result = ElasticSearchCursorHelper.DecodeCursor(encoded);

		result.ShouldNotBeNull();
		result.Count.ShouldBe(3);
	}

	// ─── EncodeCursor ───────────────────────────────────────────────────

	[Fact]
	public void EncodeStringFieldValues()
	{
		var fieldValues = new List<FieldValue> { FieldValue.String("test-id") };
		var cursor = ElasticSearchCursorHelper.EncodeCursor(fieldValues);

		cursor.ShouldNotBeNullOrWhiteSpace();

		// Should round-trip
		var decoded = ElasticSearchCursorHelper.DecodeCursor(cursor);
		decoded.ShouldNotBeNull();
		decoded.Count.ShouldBe(1);
	}

	[Fact]
	public void EncodeLongFieldValues()
	{
		var fieldValues = new List<FieldValue> { FieldValue.Long(42) };
		var cursor = ElasticSearchCursorHelper.EncodeCursor(fieldValues);

		cursor.ShouldNotBeNullOrWhiteSpace();
	}

	[Fact]
	public void EncodeDoubleFieldValues()
	{
		var fieldValues = new List<FieldValue> { FieldValue.Double(99.5) };
		var cursor = ElasticSearchCursorHelper.EncodeCursor(fieldValues);

		cursor.ShouldNotBeNullOrWhiteSpace();
	}

	[Fact]
	public void EncodeBoolFieldValues()
	{
		var fieldValues = new List<FieldValue> { FieldValue.True, FieldValue.False };
		var cursor = ElasticSearchCursorHelper.EncodeCursor(fieldValues);

		cursor.ShouldNotBeNullOrWhiteSpace();
	}

	[Fact]
	public void EncodeNullFieldValues()
	{
		var fieldValues = new List<FieldValue> { FieldValue.Null };
		var cursor = ElasticSearchCursorHelper.EncodeCursor(fieldValues);

		cursor.ShouldNotBeNullOrWhiteSpace();
	}

	[Fact]
	public void ThrowArgumentNullException_ForNullSortValues()
	{
		Should.Throw<ArgumentNullException>(() =>
			ElasticSearchCursorHelper.EncodeCursor(null!));
	}

	[Fact]
	public void ThrowArgumentException_ForEmptySortValues()
	{
		Should.Throw<ArgumentException>(() =>
			ElasticSearchCursorHelper.EncodeCursor(new List<FieldValue>()));
	}

	// ─── RoundTrip: Encode → Decode ────────────────────────────────────

	[Fact]
	public void RoundTripFieldValues_String()
	{
		var original = new List<FieldValue> { FieldValue.String("order-abc") };
		var cursor = ElasticSearchCursorHelper.EncodeCursor(original);
		var decoded = ElasticSearchCursorHelper.DecodeCursor(cursor);

		decoded.ShouldNotBeNull();
		decoded.Count.ShouldBe(1);
		decoded[0].ToString().ShouldBe("order-abc");
	}

	[Fact]
	public void RoundTripFieldValues_MixedTypes()
	{
		var original = new List<FieldValue>
		{
			FieldValue.String("status"),
			FieldValue.Long(1234567890),
			FieldValue.Double(99.95),
			FieldValue.True,
			FieldValue.Null
		};

		var cursor = ElasticSearchCursorHelper.EncodeCursor(original);
		var decoded = ElasticSearchCursorHelper.DecodeCursor(cursor);

		decoded.ShouldNotBeNull();
		decoded.Count.ShouldBe(5);
	}

	// ─── EncodeCursor produces URL-safe output ─────────────────────────

	[Fact]
	public void ProduceUrlSafeOutput()
	{
		var fieldValues = new List<FieldValue>
		{
			FieldValue.String("value+with/special=chars"),
			FieldValue.Long(999)
		};

		var cursor = ElasticSearchCursorHelper.EncodeCursor(fieldValues);

		cursor.ShouldNotContain("+");
		cursor.ShouldNotContain("/");
		cursor.ShouldNotContain("=");
	}

	// ─── ResolveCursorBoundaries (pure core of ToCursorResult) ───────────
	//
	// Worked example: documents D9..D1 sorted by a descending key, page size 3:
	//   page 0 = [9,8,7], page 1 = [6,5,4], page 2 = [3,2,1].
	// Forward queries (First/Next) return hits in descending order; reverse
	// queries (Previous/Last) return ascending order and are flipped for display.
	// Each query over-fetches one peek row (Size = pageSize + 1).

	private const int PageSize = 3;

	private static IReadOnlyCollection<FieldValue> Sort(long value) => new[] { FieldValue.Long(value) };

	private static string Cursor(long value) =>
		ElasticSearchCursorHelper.EncodeCursor(new[] { FieldValue.Long(value) });

	[Fact]
	public void ResolveCursorBoundaries_FirstPage_HasForwardCursorOnly()
	{
		// Forward (First) over-fetch: [9,8,7] + peek 6
		var hits = new IReadOnlyCollection<FieldValue>?[] { Sort(9), Sort(8), Sort(7), Sort(6) };

		var (keptCount, reverseItems, nextCursor, previousCursor) =
			ElasticSearchCursorHelper.ResolveCursorBoundaries(hits, PageSize, PageNavigation.First);

		keptCount.ShouldBe(3);
		reverseItems.ShouldBeFalse();
		nextCursor.ShouldBe(Cursor(7));   // last displayed item
		previousCursor.ShouldBeNull();    // first page → no previous
	}

	[Fact]
	public void ResolveCursorBoundaries_MiddlePage_ViaNext_HasBothCursors()
	{
		// Forward (Next) over-fetch: [6,5,4] + peek 3
		var hits = new IReadOnlyCollection<FieldValue>?[] { Sort(6), Sort(5), Sort(4), Sort(3) };

		var (keptCount, reverseItems, nextCursor, previousCursor) =
			ElasticSearchCursorHelper.ResolveCursorBoundaries(hits, PageSize, PageNavigation.Next);

		keptCount.ShouldBe(3);
		reverseItems.ShouldBeFalse();
		nextCursor.ShouldBe(Cursor(4));       // last displayed
		previousCursor.ShouldBe(Cursor(6));   // first displayed
	}

	[Fact]
	public void ResolveCursorBoundaries_LastPage_ViaNext_HasNoForwardCursor()
	{
		// Forward (Next), no peek: [3,2,1]
		var hits = new IReadOnlyCollection<FieldValue>?[] { Sort(3), Sort(2), Sort(1) };

		var (keptCount, reverseItems, nextCursor, previousCursor) =
			ElasticSearchCursorHelper.ResolveCursorBoundaries(hits, PageSize, PageNavigation.Next);

		keptCount.ShouldBe(3);
		reverseItems.ShouldBeFalse();
		nextCursor.ShouldBeNull();            // no peek → last page
		previousCursor.ShouldBe(Cursor(3));   // first displayed
	}

	[Fact]
	public void ResolveCursorBoundaries_Previous_FlipsItems_AndMatchesForwardArrival()
	{
		// Reverse (Previous) ascending over-fetch: [4,5,6] + peek 7
		var hits = new IReadOnlyCollection<FieldValue>?[] { Sort(4), Sort(5), Sort(6), Sort(7) };

		var (keptCount, reverseItems, nextCursor, previousCursor) =
			ElasticSearchCursorHelper.ResolveCursorBoundaries(hits, PageSize, PageNavigation.Previous);

		keptCount.ShouldBe(3);
		reverseItems.ShouldBeTrue();
		// Display order after reverse is [6,5,4] — identical cursors to arriving via Next.
		nextCursor.ShouldBe(Cursor(4));       // last displayed
		previousCursor.ShouldBe(Cursor(6));   // first displayed
	}

	[Fact]
	public void ResolveCursorBoundaries_PreviousToFirstPage_HasNoBackwardCursor()
	{
		// Reverse (Previous) ascending, no peek: [7,8,9] → display [9,8,7]
		var hits = new IReadOnlyCollection<FieldValue>?[] { Sort(7), Sort(8), Sort(9) };

		var (keptCount, reverseItems, nextCursor, previousCursor) =
			ElasticSearchCursorHelper.ResolveCursorBoundaries(hits, PageSize, PageNavigation.Previous);

		keptCount.ShouldBe(3);
		reverseItems.ShouldBeTrue();
		nextCursor.ShouldBe(Cursor(7));       // last displayed
		previousCursor.ShouldBeNull();        // no peek → first page
	}

	[Fact]
	public void ResolveCursorBoundaries_LastNavigation_HasBackwardCursorOnly()
	{
		// Reverse (Last) ascending over-fetch: [1,2,3] + peek 4 → display [3,2,1]
		var hits = new IReadOnlyCollection<FieldValue>?[] { Sort(1), Sort(2), Sort(3), Sort(4) };

		var (keptCount, reverseItems, nextCursor, previousCursor) =
			ElasticSearchCursorHelper.ResolveCursorBoundaries(hits, PageSize, PageNavigation.Last);

		keptCount.ShouldBe(3);
		reverseItems.ShouldBeTrue();
		nextCursor.ShouldBeNull();            // Last → no next by definition
		previousCursor.ShouldBe(Cursor(3));   // first displayed
	}

	[Fact]
	public void ResolveCursorBoundaries_EmptyResult_HasNoCursors()
	{
		var hits = System.Array.Empty<IReadOnlyCollection<FieldValue>?>();

		var (keptCount, reverseItems, nextCursor, previousCursor) =
			ElasticSearchCursorHelper.ResolveCursorBoundaries(hits, PageSize, PageNavigation.First);

		keptCount.ShouldBe(0);
		reverseItems.ShouldBeFalse();
		nextCursor.ShouldBeNull();
		previousCursor.ShouldBeNull();
	}

	[Fact]
	public void ResolveCursorBoundaries_ExactlyFullPage_NoPeek_ReportsNoNext()
	{
		// Regression guard for the original off-by-one: a final page of exactly
		// pageSize items (no peek row) must NOT advertise a next page.
		var hits = new IReadOnlyCollection<FieldValue>?[] { Sort(3), Sort(2), Sort(1) };

		var (_, _, nextCursor, _) =
			ElasticSearchCursorHelper.ResolveCursorBoundaries(hits, PageSize, PageNavigation.First);

		nextCursor.ShouldBeNull();
	}
}
