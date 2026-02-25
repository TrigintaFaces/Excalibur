// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Text.Json;
using Excalibur.Dispatch.Transport.Azure;

namespace Excalibur.Dispatch.Transport.Tests.AzureServiceBus.Serialization;

[Trait("Category", "Unit")]
[Trait("Component", "Platform")]
public sealed class BinaryDataConverterShould
{
	private static readonly JsonSerializerOptions Options = new()
	{
		Converters = { new BinaryDataConverter() },
	};

	[Fact]
	public void ReadNullAsNull()
	{
		// Arrange
		var json = "null"u8.ToArray();

		// Act
		var result = JsonSerializer.Deserialize<BinaryData>(json, Options);

		// Assert
		result.ShouldBeNull();
	}

	[Fact]
	public void ReadEmptyStringAsEmpty()
	{
		// Arrange
		var json = "\"\""u8.ToArray();

		// Act
		var result = JsonSerializer.Deserialize<BinaryData>(json, Options);

		// Assert
		result.ShouldNotBeNull();
		result!.ToArray().ShouldBeEmpty();
	}

	[Fact]
	public void ReadBase64EncodedString()
	{
		// Arrange
		var bytes = new byte[] { 1, 2, 3, 4 };
		var base64 = Convert.ToBase64String(bytes);
		var jsonString = $"\"{base64}\"";

		// Act
		var result = JsonSerializer.Deserialize<BinaryData>(jsonString, Options);

		// Assert
		result.ShouldNotBeNull();
		result!.ToArray().ShouldBe(bytes);
	}

	[Fact]
	public void ReadJsonObjectAsBinaryData()
	{
		// Arrange
		var json = "{\"key\":\"value\"}"u8.ToArray();

		// Act
		var result = JsonSerializer.Deserialize<BinaryData>(json, Options);

		// Assert
		result.ShouldNotBeNull();
		result!.ToString().ShouldContain("key");
		result.ToString().ShouldContain("value");
	}

	[Fact]
	public void ReadJsonArrayAsBinaryData()
	{
		// Arrange
		var json = "[1,2,3]"u8.ToArray();

		// Act
		var result = JsonSerializer.Deserialize<BinaryData>(json, Options);

		// Assert
		result.ShouldNotBeNull();
		result!.ToString().ShouldContain("1");
	}

	[Fact]
	public void WriteJsonContentAsJson()
	{
		// Arrange — BinaryData with valid JSON
		var bd = BinaryData.FromString("{\"hello\":\"world\"}");

		// Act
		var json = JsonSerializer.Serialize(bd, Options);

		// Assert — should serialize the JSON content directly
		json.ShouldContain("hello");
		json.ShouldContain("world");
	}

	[Fact]
	public void WriteBinaryContentAsBase64()
	{
		// Arrange — BinaryData with non-JSON bytes
		var bytes = new byte[] { 0xFF, 0xFE, 0x00, 0x01 };
		var bd = BinaryData.FromBytes(bytes);

		// Act
		var json = JsonSerializer.Serialize(bd, Options);

		// Assert — should fall back to base64
		var expectedBase64 = Convert.ToBase64String(bytes);
		json.ShouldContain(expectedBase64);
	}

	[Fact]
	public void WriteNullBinaryDataAsNull()
	{
		// Arrange
		BinaryData? bd = null;

		// Act
		var json = JsonSerializer.Serialize(bd, Options);

		// Assert
		json.ShouldBe("null");
	}
}
