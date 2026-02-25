// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Text;

using Excalibur.Dispatch.Simd;

namespace Excalibur.Dispatch.Tests.Messaging.Simd;

/// <summary>
///     Functional tests for <see cref="SimdMessageParser" />.
/// </summary>
[Trait("Category", "Unit")]
public sealed class SimdMessageParserShould
{
	[Fact]
	public void FindFirstByteReturnFirstIndexWhenPresent()
	{
		var buffer = Utf8($"{new string('x', 64)}ab");

		var index = SimdMessageParser.FindFirstByte(buffer, (byte)'a');

		index.ShouldBe(64);
	}

	[Fact]
	public void FindFirstByteReturnMinusOneWhenMissing()
	{
		var index = SimdMessageParser.FindFirstByte(Utf8("abc"), (byte)'z');

		index.ShouldBe(-1);
	}

	[Fact]
	public void FindFirstNewlineReturnEarliestNewlineCharacter()
	{
		var buffer = Utf8($"header{new string('x', 40)}\r\nnext");

		var index = SimdMessageParser.FindFirstNewline(buffer);

		index.ShouldBe(46);
	}

	[Fact]
	public void FindFirstNewlineReturnMinusOneWhenMissing()
	{
		var index = SimdMessageParser.FindFirstNewline(Utf8("abcdef"));

		index.ShouldBe(-1);
	}

	[Fact]
	public void FindAllDelimitersReturnAllPositionsInOrder()
	{
		var indices = new int[8];
		var count = SimdMessageParser.FindAllDelimiters(Utf8("a,b,c,d"), (byte)',', indices);

		count.ShouldBe(3);
		indices[0].ShouldBe(1);
		indices[1].ShouldBe(3);
		indices[2].ShouldBe(5);
	}

	[Fact]
	public void FindAllDelimitersRespectOutputCapacity()
	{
		var indices = new int[2];

		var count = SimdMessageParser.FindAllDelimiters(Utf8("1,2,3,4"), (byte)',', indices);

		count.ShouldBe(2);
		indices[0].ShouldBe(1);
		indices[1].ShouldBe(3);
	}

	[Fact]
	public void ThrowForNullHeadersDictionaryInParseHeaders()
	{
		Should.Throw<ArgumentNullException>(() => SimdMessageParser.ParseHeaders(Utf8("X: Y"), null!));
	}

	[Fact]
	public void ParseHeadersReadHeadersUntilDoubleCrlf()
	{
		const string raw = "Content-Type: application/json\r\nX-Request-Id:\tabc-123\r\n\r\n{\"ok\":true}";
		var buffer = Utf8(raw);
		var headers = new Dictionary<string, string>(StringComparer.Ordinal);

		var headerEnd = SimdMessageParser.ParseHeaders(buffer, headers);

		headerEnd.ShouldBe(raw.IndexOf("\r\n\r\n", StringComparison.Ordinal) + 4);
		headers.Count.ShouldBe(2);
		headers["Content-Type"].ShouldBe("application/json");
		headers["X-Request-Id"].ShouldBe("abc-123");
	}

	[Fact]
	public void ParseHeadersUseWholeBufferWhenHeaderSeparatorMissing()
	{
		var buffer = Utf8("A:1\nB: 2");
		var headers = new Dictionary<string, string>(StringComparer.Ordinal);

		var headerEnd = SimdMessageParser.ParseHeaders(buffer, headers);

		headerEnd.ShouldBe(buffer.Length);
		headers["A"].ShouldBe("1");
		headers["B"].ShouldBe("2");
	}

	[Fact]
	public void ParseHeadersIgnoreMalformedLinesAndEmptyValues()
	{
		var headers = new Dictionary<string, string>(StringComparer.Ordinal);
		var buffer = Utf8(": bad\nKey:   \nValid: good\n\n");

		_ = SimdMessageParser.ParseHeaders(buffer, headers);

		headers.Count.ShouldBe(1);
		headers["Valid"].ShouldBe("good");
	}

	[Fact]
	public void FindJsonStartReturnObjectOrArrayStart()
	{
		SimdMessageParser.FindJsonStart(Utf8("   [1,2,3]")).ShouldBe(3);
		SimdMessageParser.FindJsonStart(Utf8("xx{\"a\":1}")).ShouldBe(2);
	}

	[Fact]
	public void FindJsonStartReturnMinusOneWhenNoJsonStartExists()
	{
		var index = SimdMessageParser.FindJsonStart(Utf8("plain text"));

		index.ShouldBe(-1);
	}

	[Fact]
	public void CountByteReturnTotalOccurrences()
	{
		var count = SimdMessageParser.CountByte(Utf8("banana"), (byte)'a');

		count.ShouldBe(3);
	}

	[Fact]
	public void FindFirstByteAvx2ReturnIndexWhenValueExists()
	{
		var buffer = Utf8($"{new string('q', 96)}z");

		var index = SimdMessageParser.FindFirstByteAvx2(buffer, (byte)'z');

		index.ShouldBe(96);
	}

	[Fact]
	public void FindFirstByteAvx2ReturnMinusOneWhenValueMissing()
	{
		var index = SimdMessageParser.FindFirstByteAvx2(Utf8(new string('q', 80)), (byte)'z');

		index.ShouldBe(-1);
	}

	[Fact]
	public void FindHeadersEndReturnPositionAfterDoubleCrlf()
	{
		const string raw = "A: 1\r\nB: 2\r\n\r\nbody";
		var index = SimdMessageParser.FindHeadersEnd(Utf8(raw));

		index.ShouldBe(raw.IndexOf("\r\n\r\n", StringComparison.Ordinal) + 4);
	}

	[Fact]
	public void FindHeadersEndReturnMinusOneWhenSeparatorMissing()
	{
		var index = SimdMessageParser.FindHeadersEnd(Utf8("A: 1\r\nB: 2\r\nbody"));

		index.ShouldBe(-1);
	}

	[Fact]
	public void TryFindHeaderReturnValueSpanForMatchingHeader()
	{
		var buffer = Utf8("A: 1\r\nX-Target: found\r\nB: 2\r\n");
		var found = SimdMessageParser.TryFindHeader(buffer, Utf8("X-Target"), out var valueStart, out var valueLength);

		found.ShouldBeTrue();
		valueLength.ShouldBe(5);
		Encoding.UTF8.GetString(buffer.AsSpan(valueStart, valueLength)).ShouldBe("found");
	}

	[Fact]
	public void TryFindHeaderReturnFalseForMissingHeader()
	{
		var found = SimdMessageParser.TryFindHeader(Utf8("A: 1\r\nB: 2\r\n"), Utf8("X"), out var valueStart, out var valueLength);

		found.ShouldBeFalse();
		valueStart.ShouldBe(-1);
		valueLength.ShouldBe(0);
	}

	[Fact]
	public void TryFindHeaderHandleFinalLineWithoutTrailingNewline()
	{
		var buffer = Utf8("A: 1\r\nB: final");
		var found = SimdMessageParser.TryFindHeader(buffer, Utf8("B"), out var valueStart, out var valueLength);

		found.ShouldBeTrue();
		Encoding.UTF8.GetString(buffer.AsSpan(valueStart, valueLength)).ShouldBe("final");
	}

	[Fact]
	public void CountNewlinesReturnExpectedCount()
	{
		var count = SimdMessageParser.CountNewlines(Utf8("a\nb\nc\nd"));

		count.ShouldBe(3);
	}

	[Fact]
	public void CountNewlinesReturnZeroWhenNoNewlines()
	{
		var count = SimdMessageParser.CountNewlines(Utf8(new string('x', 128)));

		count.ShouldBe(0);
	}

	private static byte[] Utf8(string value) => Encoding.UTF8.GetBytes(value);
}
