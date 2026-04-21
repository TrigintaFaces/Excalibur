// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.EventSourcing.Abstractions;

namespace Excalibur.Data.ElasticSearch.Tests.ElasticSearch;

[UnitTest]
public sealed class CursorEncoderShould
{
	// ─── Encode/Decode round-trip tests ─────────────────────────────────

	[Fact]
	public void RoundTrip_StringValues()
	{
		var cursor = CursorEncoder.Encode("hello", "world");
		var decoded = CursorEncoder.Decode(cursor);

		decoded.ShouldNotBeNull();
		decoded.Length.ShouldBe(2);
		decoded[0].ShouldBe("hello");
		decoded[1].ShouldBe("world");
	}

	[Fact]
	public void RoundTrip_LongValues()
	{
		var cursor = CursorEncoder.Encode(42L, 9999999999L);
		var decoded = CursorEncoder.Decode(cursor);

		decoded.ShouldNotBeNull();
		decoded.Length.ShouldBe(2);
		decoded[0].ShouldBe(42L);
		decoded[1].ShouldBe(9999999999L);
	}

	[Fact]
	public void RoundTrip_IntValues()
	{
		// int is written as a number; Decode reads it as long (JSON has no int/long distinction)
		var cursor = CursorEncoder.Encode(42);
		var decoded = CursorEncoder.Decode(cursor);

		decoded.ShouldNotBeNull();
		decoded[0].ShouldBe(42L); // int → long on decode
	}

	[Fact]
	public void RoundTrip_DoubleValues()
	{
		var cursor = CursorEncoder.Encode(3.14);
		var decoded = CursorEncoder.Decode(cursor);

		decoded.ShouldNotBeNull();
		decoded[0].ShouldBe(3.14);
	}

	[Fact]
	public void RoundTrip_FloatValues()
	{
		// float 1.5f has exact double representation
		var cursor = CursorEncoder.Encode(1.5f);
		var decoded = CursorEncoder.Decode(cursor);

		decoded.ShouldNotBeNull();
		// Float may be read as long if it's an integer value, or double
		var value = Convert.ToDouble(decoded[0]);
		value.ShouldBe(1.5);
	}

	[Fact]
	public void RoundTrip_DecimalValues()
	{
		var cursor = CursorEncoder.Encode(99.99m);
		var decoded = CursorEncoder.Decode(cursor);

		decoded.ShouldNotBeNull();
		// Decimal written as JSON number, read back as double
		Convert.ToDouble(decoded[0]).ShouldBe(99.99);
	}

	[Fact]
	public void RoundTrip_BooleanValues()
	{
		var cursor = CursorEncoder.Encode(true, false);
		var decoded = CursorEncoder.Decode(cursor);

		decoded.ShouldNotBeNull();
		decoded.Length.ShouldBe(2);
		decoded[0].ShouldBe(true);
		decoded[1].ShouldBe(false);
	}

	[Fact]
	public void RoundTrip_NullValues()
	{
		var cursor = CursorEncoder.Encode(null!, "after-null");
		var decoded = CursorEncoder.Decode(cursor);

		decoded.ShouldNotBeNull();
		decoded.Length.ShouldBe(2);
		decoded[0].ShouldBeNull();
		decoded[1].ShouldBe("after-null");
	}

	[Fact]
	public void RoundTrip_DateTimeOffsetValues()
	{
		var dto = new DateTimeOffset(2026, 4, 21, 12, 30, 0, TimeSpan.Zero);
		var cursor = CursorEncoder.Encode(dto);
		var decoded = CursorEncoder.Decode(cursor);

		decoded.ShouldNotBeNull();
		// DateTimeOffset → epoch millis (long) on decode
		decoded[0].ShouldBeOfType<long>();
		var millis = (long)decoded[0]!;
		millis.ShouldBe(dto.ToUnixTimeMilliseconds());
	}

	[Fact]
	public void RoundTrip_DateTimeValues()
	{
		var dt = new DateTime(2026, 4, 21, 12, 30, 0, DateTimeKind.Utc);
		var cursor = CursorEncoder.Encode(dt);
		var decoded = CursorEncoder.Decode(cursor);

		decoded.ShouldNotBeNull();
		decoded[0].ShouldBeOfType<long>();
		var millis = (long)decoded[0]!;
		millis.ShouldBe(new DateTimeOffset(dt).ToUnixTimeMilliseconds());
	}

	[Fact]
	public void RoundTrip_DateOnlyValues()
	{
		var date = new DateOnly(2026, 4, 21);
		var cursor = CursorEncoder.Encode(date);
		var decoded = CursorEncoder.Decode(cursor);

		decoded.ShouldNotBeNull();
		// DateOnly stored as ISO 8601 string
		decoded[0].ShouldBeOfType<string>();
		var str = (string)decoded[0]!;
		str.ShouldBe(date.ToString("O"));
	}

	[Fact]
	public void RoundTrip_TimeOnlyValues()
	{
		var time = new TimeOnly(14, 30, 45);
		var cursor = CursorEncoder.Encode(time);
		var decoded = CursorEncoder.Decode(cursor);

		decoded.ShouldNotBeNull();
		// TimeOnly stored as ISO 8601 string
		decoded[0].ShouldBeOfType<string>();
		var str = (string)decoded[0]!;
		str.ShouldBe(time.ToString("O"));
	}

	[Fact]
	public void RoundTrip_MixedTypeValues()
	{
		var cursor = CursorEncoder.Encode("order-123", 42L, 99.5, true, null!);
		var decoded = CursorEncoder.Decode(cursor);

		decoded.ShouldNotBeNull();
		decoded.Length.ShouldBe(5);
		decoded[0].ShouldBe("order-123");
		decoded[1].ShouldBe(42L);
		decoded[2].ShouldBe(99.5);
		decoded[3].ShouldBe(true);
		decoded[4].ShouldBeNull();
	}

	// ─── Encode output format tests ────────────────────────────────────

	[Fact]
	public void ProduceUrlSafeCursorString()
	{
		var cursor = CursorEncoder.Encode("test");

		// Base64url: no +, no /, no = padding
		cursor.ShouldNotContain("+");
		cursor.ShouldNotContain("/");
		cursor.ShouldNotContain("=");
	}

	[Fact]
	public void ProduceNonEmptyCursorString()
	{
		var cursor = CursorEncoder.Encode("value");

		cursor.ShouldNotBeNullOrWhiteSpace();
		cursor.Length.ShouldBeGreaterThan(0);
	}

	// ─── Decode edge cases ─────────────────────────────────────────────

	[Fact]
	public void ReturnNull_ForNullCursor()
	{
		CursorEncoder.Decode(null).ShouldBeNull();
	}

	[Fact]
	public void ReturnNull_ForEmptyStringCursor()
	{
		CursorEncoder.Decode("").ShouldBeNull();
	}

	[Fact]
	public void ReturnNull_ForWhitespaceCursor()
	{
		CursorEncoder.Decode("   ").ShouldBeNull();
	}

	[Fact]
	public void ReturnNull_ForMalformedBase64()
	{
		CursorEncoder.Decode("not-valid-base64!!!").ShouldBeNull();
	}

	[Fact]
	public void ReturnNull_ForCorruptJson()
	{
		// Valid Base64 but invalid JSON
		var base64 = Convert.ToBase64String("not json"u8.ToArray());
		CursorEncoder.Decode(base64).ShouldBeNull();
	}

	[Fact]
	public void ReturnNull_ForEmptyJsonArray()
	{
		var base64 = Convert.ToBase64String("[]"u8.ToArray());
		CursorEncoder.Decode(base64).ShouldBeNull();
	}

	[Fact]
	public void ReturnNull_ForJsonObjectInsteadOfArray()
	{
		var base64 = Convert.ToBase64String("{}"u8.ToArray());
		CursorEncoder.Decode(base64).ShouldBeNull();
	}

	// ─── Encode error handling ──────────────────────────────────────────

	[Fact]
	public void ThrowArgumentNullException_ForNullSortValues()
	{
		Should.Throw<ArgumentNullException>(() => CursorEncoder.Encode(null!));
	}

	[Fact]
	public void ThrowArgumentException_ForEmptySortValues()
	{
		Should.Throw<ArgumentException>(() => CursorEncoder.Encode(Array.Empty<object?>()));
	}

	// ─── Fallback for unknown types ────────────────────────────────────

	[Fact]
	public void HandleUnknownType_ViaToString()
	{
		// Unknown type should be encoded via ToString()
		var cursor = CursorEncoder.Encode(new Uri("https://example.com"));
		var decoded = CursorEncoder.Decode(cursor);

		decoded.ShouldNotBeNull();
		decoded[0].ShouldBe("https://example.com/");
	}

	// ─── Single value convenience ──────────────────────────────────────

	[Fact]
	public void RoundTrip_SingleValue()
	{
		var cursor = CursorEncoder.Encode("single");
		var decoded = CursorEncoder.Decode(cursor);

		decoded.ShouldNotBeNull();
		decoded.Length.ShouldBe(1);
		decoded[0].ShouldBe("single");
	}

	// ─── Large values ──────────────────────────────────────────────────

	[Fact]
	public void RoundTrip_LargeNumberOfSortValues()
	{
		var values = new object?[10];
		for (var i = 0; i < 10; i++)
		{
			values[i] = (long)i * 1000;
		}

		var cursor = CursorEncoder.Encode(values);
		var decoded = CursorEncoder.Decode(cursor);

		decoded.ShouldNotBeNull();
		decoded.Length.ShouldBe(10);

		for (var i = 0; i < 10; i++)
		{
			decoded[i].ShouldBe((long)i * 1000);
		}
	}

	[Fact]
	public void RoundTrip_LongStringValue()
	{
		var longString = new string('x', 10000);
		var cursor = CursorEncoder.Encode(longString);
		var decoded = CursorEncoder.Decode(cursor);

		decoded.ShouldNotBeNull();
		decoded[0].ShouldBe(longString);
	}

	// ─── Boundary number values ────────────────────────────────────────

	[Fact]
	public void RoundTrip_LongMaxValue()
	{
		var cursor = CursorEncoder.Encode(long.MaxValue);
		var decoded = CursorEncoder.Decode(cursor);

		decoded.ShouldNotBeNull();
		decoded[0].ShouldBe(long.MaxValue);
	}

	[Fact]
	public void RoundTrip_LongMinValue()
	{
		var cursor = CursorEncoder.Encode(long.MinValue);
		var decoded = CursorEncoder.Decode(cursor);

		decoded.ShouldNotBeNull();
		decoded[0].ShouldBe(long.MinValue);
	}

	[Fact]
	public void RoundTrip_Zero()
	{
		var cursor = CursorEncoder.Encode(0L);
		var decoded = CursorEncoder.Decode(cursor);

		decoded.ShouldNotBeNull();
		decoded[0].ShouldBe(0L);
	}

	[Fact]
	public void RoundTrip_NegativeNumbers()
	{
		var cursor = CursorEncoder.Encode(-42L, -99.5);
		var decoded = CursorEncoder.Decode(cursor);

		decoded.ShouldNotBeNull();
		decoded[0].ShouldBe(-42L);
		decoded[1].ShouldBe(-99.5);
	}

	// ─── Special string values ─────────────────────────────────────────

	[Fact]
	public void RoundTrip_EmptyString()
	{
		var cursor = CursorEncoder.Encode("");
		var decoded = CursorEncoder.Decode(cursor);

		decoded.ShouldNotBeNull();
		decoded[0].ShouldBe("");
	}

	[Fact]
	public void RoundTrip_UnicodeString()
	{
		var cursor = CursorEncoder.Encode("日本語テスト 🎉");
		var decoded = CursorEncoder.Decode(cursor);

		decoded.ShouldNotBeNull();
		decoded[0].ShouldBe("日本語テスト 🎉");
	}

	[Fact]
	public void RoundTrip_StringWithSpecialCharacters()
	{
		var cursor = CursorEncoder.Encode("line1\nline2\ttab\"quote");
		var decoded = CursorEncoder.Decode(cursor);

		decoded.ShouldNotBeNull();
		decoded[0].ShouldBe("line1\nline2\ttab\"quote");
	}
}
