// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Text;

using Excalibur.Dispatch.Simd;

namespace Excalibur.Dispatch.Tests.Messaging.Simd;

/// <summary>
///     Functional tests for <see cref="SimdJsonFieldExtractor" />.
/// </summary>
[Trait("Category", "Unit")]
public sealed class SimdJsonFieldExtractorShould
{
	[Fact]
	public void TryExtractStringFieldReturnValueWhenPresent()
	{
		var found = SimdJsonFieldExtractor.TryExtractStringField(
			Utf8("{\"name\":\"alice\"}"),
			Utf8("name"),
			out var value);

		found.ShouldBeTrue();
		value.ShouldBe("alice");
	}

	[Fact]
	public void TryExtractStringFieldHandleEscapedQuotes()
	{
		var found = SimdJsonFieldExtractor.TryExtractStringField(
			Utf8("{\"message\":\"say \\\"hi\\\" now\"}"),
			Utf8("message"),
			out var value);

		found.ShouldBeTrue();
		value.ShouldBe("say \\\"hi\\\" now");
	}

	[Fact]
	public void TryExtractStringFieldReturnFalseForMissingField()
	{
		var found = SimdJsonFieldExtractor.TryExtractStringField(
			Utf8("{\"name\":\"alice\"}"),
			Utf8("missing"),
			out var value);

		found.ShouldBeFalse();
		value.ShouldBeNull();
	}

	[Fact]
	public void TryExtractStringFieldReturnFalseWhenColonMissing()
	{
		var found = SimdJsonFieldExtractor.TryExtractStringField(
			Utf8("{\"name\" \"alice\"}"),
			Utf8("name"),
			out _);

		found.ShouldBeFalse();
	}

	[Fact]
	public void TryExtractStringFieldReturnFalseWhenValueIsNotQuoted()
	{
		var found = SimdJsonFieldExtractor.TryExtractStringField(
			Utf8("{\"name\":123}"),
			Utf8("name"),
			out _);

		found.ShouldBeFalse();
	}

	[Fact]
	public void TryExtractStringFieldReturnFalseWhenStringNotClosed()
	{
		var found = SimdJsonFieldExtractor.TryExtractStringField(
			Utf8("{\"name\":\"alice}"),
			Utf8("name"),
			out _);

		found.ShouldBeFalse();
	}

	[Fact]
	public void TryExtractNumericFieldParseInteger()
	{
		var found = SimdJsonFieldExtractor.TryExtractNumericField(
			Utf8("{\"count\":42}"),
			Utf8("count"),
			out var value);

		found.ShouldBeTrue();
		value.ShouldBe(42d);
	}

	[Fact]
	public void TryExtractNumericFieldParseScientificNotation()
	{
		var found = SimdJsonFieldExtractor.TryExtractNumericField(
			Utf8("{\"count\":-12e+2}"),
			Utf8("count"),
			out var value);

		found.ShouldBeTrue();
		value.ShouldBe(-1200d);
	}

	[Fact]
	public void TryExtractNumericFieldBackUpWhenTrailingDotExists()
	{
		var found = SimdJsonFieldExtractor.TryExtractNumericField(
			Utf8("{\"count\":12.}"),
			Utf8("count"),
			out var value);

		found.ShouldBeTrue();
		value.ShouldBe(12d);
	}

	[Fact]
	public void TryExtractNumericFieldReturnFalseForInvalidNumber()
	{
		var found = SimdJsonFieldExtractor.TryExtractNumericField(
			Utf8("{\"count\":abc}"),
			Utf8("count"),
			out _);

		found.ShouldBeFalse();
	}

	[Fact]
	public void TryExtractNumericFieldReturnFalseForMissingField()
	{
		var found = SimdJsonFieldExtractor.TryExtractNumericField(
			Utf8("{\"count\":1}"),
			Utf8("missing"),
			out _);

		found.ShouldBeFalse();
	}

	[Fact]
	public void TryExtractBooleanFieldParseTrueAndFalse()
	{
		var trueFound = SimdJsonFieldExtractor.TryExtractBooleanField(
			Utf8("{\"enabled\":true}"),
			Utf8("enabled"),
			out var trueValue);
		var falseFound = SimdJsonFieldExtractor.TryExtractBooleanField(
			Utf8("{\"enabled\":false}"),
			Utf8("enabled"),
			out var falseValue);

		trueFound.ShouldBeTrue();
		trueValue.ShouldBeTrue();
		falseFound.ShouldBeTrue();
		falseValue.ShouldBeFalse();
	}

	[Fact]
	public void TryExtractBooleanFieldReturnFalseForInvalidToken()
	{
		var found = SimdJsonFieldExtractor.TryExtractBooleanField(
			Utf8("{\"enabled\":truthy}"),
			Utf8("enabled"),
			out _);

		found.ShouldBeFalse();
	}

	[Fact]
	public void ExtractMultipleStringFieldsClearDestinationAndExtractRequestedValues()
	{
		var values = new Dictionary<string, string>(StringComparer.Ordinal)
		{
			["stale"] = "value",
		};

		var extracted = SimdJsonFieldExtractor.ExtractMultipleStringFields(
			Utf8("{\"name\":\"alice\",\"city\":\"seattle\",\"age\":42}"),
			new[] { "name", "city", "age", "missing" },
			values);

		extracted.ShouldBe(2);
		values.Count.ShouldBe(2);
		values["name"].ShouldBe("alice");
		values["city"].ShouldBe("seattle");
	}

	[Fact]
	public void ExtractMultipleStringFieldsReturnZeroWhenNoRequestedFieldsExist()
	{
		var values = new Dictionary<string, string>(StringComparer.Ordinal);

		var extracted = SimdJsonFieldExtractor.ExtractMultipleStringFields(
			Utf8("{\"name\":\"alice\"}"),
			new[] { "missing", "absent" },
			values);

		extracted.ShouldBe(0);
		values.ShouldBeEmpty();
	}

	[Fact]
	public void TryExtractStringFieldAvx2ReturnValueWhenPresent()
	{
		var found = SimdJsonFieldExtractor.TryExtractStringFieldAvx2(
			Utf8($"{{{new string(' ', 64)}\"value\":{new string(' ', 16)}\"abc\"}}"),
			Utf8("value"),
			out var value);

		found.ShouldBeTrue();
		value.ShouldBe("abc");
	}

	[Fact]
	public void TryExtractStringFieldAvx2ReturnFalseWhenMissing()
	{
		var found = SimdJsonFieldExtractor.TryExtractStringFieldAvx2(
			Utf8("{\"name\":\"alice\"}"),
			Utf8("missing"),
			out var value);

		found.ShouldBeFalse();
		value.ShouldBeNull();
	}

	private static byte[] Utf8(string value) => Encoding.UTF8.GetBytes(value);
}
