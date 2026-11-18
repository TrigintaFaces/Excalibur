// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Text.Json;

using Excalibur.Dispatch.Outbox;

namespace Excalibur.Dispatch.Tests.Outbox;

[Trait("Category", "Unit")]
[Trait("Component", "Outbox")]
[Trait("Pattern", "Serialization")]
public sealed class OutboxHeaderJsonShould
{
	[Theory]
	[InlineData("null")]
	[InlineData("{}")]
	[InlineData("{\"Header\":\"upper\",\"header\":\"lower\",\"same\":1,\"same\":2}")]
	[InlineData("{\"text\":\"value\",\"integer\":42,\"decimal\":1.25,\"true\":true,\"false\":false,\"null\":null,\"array\":[1,\"two\",null],\"object\":{\"Name\":\"nested\"}}")]
	public void PreserveTheExistingClosedDictionaryValuesAndNullResult(string json)
	{
		var expected = JsonSerializer.Deserialize<Dictionary<string, object>>(json, EventSerializationDefaults.Canonical);
		var actual = OutboxHeaderJson.Deserialize(json);
		if (expected is null) { actual.ShouldBeNull(); return; }
		actual.ShouldNotBeNull();
		actual.Keys.Order(StringComparer.Ordinal).ShouldBe(expected.Keys.Order(StringComparer.Ordinal));
		foreach (var entry in expected)
		{
			if (entry.Value is null) { actual[entry.Key].ShouldBeNull(); continue; }
			var value = actual[entry.Key].ShouldBeOfType<JsonElement>();
			var prior = entry.Value.ShouldBeOfType<JsonElement>();
			value.ValueKind.ShouldBe(prior.ValueKind);
			value.GetRawText().ShouldBe(prior.GetRawText());
		}
	}

	[Theory]
	[InlineData("")]
	[InlineData(" ")]
	[InlineData("{")]
	[InlineData("[]")]
	[InlineData("{\"value\":}")]
	public void PreserveMalformedAndNonObjectErrorsRatherThanReplacingThemWithEmptyHeaders(string json)
	{
		Should.Throw<JsonException>(() => JsonSerializer.Deserialize<Dictionary<string, object>>(json, EventSerializationDefaults.Canonical));
		Should.Throw<JsonException>(() => OutboxHeaderJson.Deserialize(json));
	}

	[Fact]
	public void PreserveTheNullArgumentErrorRatherThanInventingAHeaderFallback()
	{
		Should.Throw<ArgumentNullException>(() => JsonSerializer.Deserialize<Dictionary<string, object>>((string)null!, EventSerializationDefaults.Canonical));
		Should.Throw<ArgumentNullException>(() => OutboxHeaderJson.Deserialize(null!));
	}

	[Fact]
	public void ReturnIndependentCaseSensitiveDictionaries()
	{
		var first = OutboxHeaderJson.Deserialize("{\"Header\":1,\"header\":2}").ShouldNotBeNull();
		var second = OutboxHeaderJson.Deserialize("{\"Header\":1,\"header\":2}").ShouldNotBeNull();
		first.ShouldNotBeSameAs(second);
		first.Remove("Header").ShouldBeTrue();
		first.ContainsKey("header").ShouldBeTrue();
		second.ContainsKey("Header").ShouldBeTrue();
	}
}
