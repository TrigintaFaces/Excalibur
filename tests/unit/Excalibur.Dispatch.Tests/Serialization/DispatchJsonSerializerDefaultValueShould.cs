// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Text;
using System.Text.Json;

using Excalibur.Dispatch.Serialization;

namespace Excalibur.Dispatch.Tests.Serialization;

[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Core)]
[Trait("Pattern", "Regression")]
public sealed class DispatchJsonSerializerDefaultValueShould
{
	[Fact]
	public void PreserveZeroAcrossGenericPooledStringAndElementApis()
	{
		using var serializer = new DispatchJsonSerializer();
		using var pooled = serializer.SerializeToPooledBuffer(0);
		Encoding.UTF8.GetString(pooled.WrittenSpan).ShouldBe("0");
		serializer.Serialize(0).ShouldBe("0");
		serializer.SerializeToElement(0).GetInt32().ShouldBe(0);
	}

	[Fact]
	public void PreserveFalseAcrossGenericPooledStringAndElementApis()
	{
		using var serializer = new DispatchJsonSerializer();
		using var pooled = serializer.SerializeToPooledBuffer(false);
		Encoding.UTF8.GetString(pooled.WrittenSpan).ShouldBe("false");
		serializer.Serialize(false).ShouldBe("false");
		serializer.SerializeToElement(false).GetBoolean().ShouldBeFalse();
	}

	[Fact]
	public void PreserveThePropertiesOfADefaultStruct()
	{
		using var serializer = new DispatchJsonSerializer();
		var element = serializer.SerializeToElement(default(DefaultPayload));
		element.ValueKind.ShouldBe(JsonValueKind.Object);
		element.GetProperty("number").GetInt32().ShouldBe(0);
		element.GetProperty("enabled").GetBoolean().ShouldBeFalse();
	}

	[Fact]
	public void PreserveAnEmptyGuidAsAJsonString()
	{
		using var serializer = new DispatchJsonSerializer();
		serializer.SerializeToElement(Guid.Empty).GetGuid().ShouldBe(Guid.Empty);
	}

	[Fact]
	public void PreserveActualNullAndNullableZeroAsDistinctValues()
	{
		using var serializer = new DispatchJsonSerializer();
		serializer.Serialize<int?>(null).ShouldBe("null");
		serializer.Serialize<string?>(null).ShouldBe("null");
		serializer.Serialize<int?>(0).ShouldBe("0");
		serializer.Serialize("value").ShouldBe("\"value\"");
	}

	[Fact]
	public void KeepExplicitTypeSerializationConsistentWithGenericSerialization()
	{
		using var serializer = new DispatchJsonSerializer();
		serializer.Serialize((object)0, typeof(int)).ShouldBe("0");
		serializer.Serialize((object)false, typeof(bool)).ShouldBe("false");
	}

	private readonly record struct DefaultPayload(int Number, bool Enabled);
}
