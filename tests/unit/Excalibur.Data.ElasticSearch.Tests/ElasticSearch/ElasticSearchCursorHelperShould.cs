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
}
