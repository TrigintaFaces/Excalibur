// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Text.Json;

using Excalibur.Dispatch;
using Excalibur.Outbox.SqlServer;

using Shouldly;

namespace Excalibur.Outbox.Tests.SqlServer;

[Trait("Category", "Unit")]
[Trait("Component", "Outbox")]
[Trait("Pattern", "Serialization")]
public sealed class OutboxHeaderJsonReaderShould
{
	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("null")]
	[InlineData("{}")]
	[InlineData("{\"Header\":\"upper\",\"header\":\"lower\",\"same\":1,\"same\":2}")]
	[InlineData("{\"text\":\"value\",\"integer\":42,\"decimal\":1.25,\"true\":true,\"false\":false,\"null\":null,\"array\":[1,\"two\",null],\"object\":{\"Name\":\"nested\"}}")]
	public void PreserveTheExistingCanonicalDictionaryContract(string? json)
	{
		var expected = string.IsNullOrEmpty(json)
			? new Dictionary<string, object>(StringComparer.Ordinal)
			: JsonSerializer.Deserialize<Dictionary<string, object>>(json, EventSerializationDefaults.Canonical)
			  ?? new Dictionary<string, object>(StringComparer.Ordinal);
		var actual = OutboxHeaderJsonReader.Deserialize(json);

		actual.Count.ShouldBe(expected.Count);
		actual.Keys.Order(StringComparer.Ordinal).ShouldBe(expected.Keys.Order(StringComparer.Ordinal));
		foreach (var entry in expected)
		{
			if (entry.Value is null)
			{
				actual[entry.Key].ShouldBeNull();
				continue;
			}

			var expectedElement = entry.Value.ShouldBeOfType<JsonElement>();
			var actualElement = actual[entry.Key].ShouldBeOfType<JsonElement>();
			actualElement.ValueKind.ShouldBe(expectedElement.ValueKind);
			actualElement.GetRawText().ShouldBe(expectedElement.GetRawText());
		}
	}

	[Theory]
	[InlineData(" ")]
	[InlineData("{")]
	[InlineData("[]")]
	[InlineData("{\"header\":}")]
	public void RejectMalformedOrNonDictionaryJson(string json)
	{
		Should.Throw<JsonException>(() => JsonSerializer.Deserialize<Dictionary<string, object>>(json, EventSerializationDefaults.Canonical));
		Should.Throw<JsonException>(() => OutboxHeaderJsonReader.Deserialize(json));
	}
}
