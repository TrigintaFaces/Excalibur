// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Text;
using System.Text.Json;

using Excalibur.Dispatch.Abstractions.Serialization;
using Excalibur.Dispatch.Serialization;
using Excalibur.Dispatch.Serialization.MemoryPack;
using Excalibur.Dispatch.Tests.Serialization.TestData;

using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Dispatch.Tests.Serialization;

/// <summary>
/// Unit tests for <see cref="PayloadSerializer.DeserializeTransportMessage{T}"/>
/// validating hybrid format detection for transport layer messages.
/// </summary>
/// <remarks>
/// <para>
/// These tests verify the four detection paths in DeserializeTransportMessage:
/// </para>
/// <list type="number">
///   <item>Our magic byte format (1-254) - delegated to IPayloadSerializer</item>
///   <item>Confluent Schema Registry format (0x00 + 4-byte header) - JSON fallback</item>
///   <item>Raw JSON format (0x7B '{' or 0x5B '[') - direct JSON deserialization</item>
///   <item>Unknown format - throws SerializationException</item>
/// </list>
/// <para>
/// See ADR-062 and ADR-063 for architecture and magic byte format details.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
public sealed class PayloadSerializerTransportDeserializationShould
{
	private readonly ILogger<PayloadSerializer> _logger = NullLogger<PayloadSerializer>.Instance;

	#region Our Magic Byte Format Tests (1-254)

	[Fact]
	public void DeserializeTransportMessage_WithOurMagicByte_DelegatesToDeserialize()
	{
		// Arrange
		var registry = CreateRegistryWithMemoryPack();
		var sut = new PayloadSerializer(registry, _logger);
		var original = new TestMessage { Name = "Transport", Value = 42 };
		var serialized = sut.Serialize(original); // Creates [1, ...payload]

		// Act
		var result = sut.DeserializeTransportMessage<TestMessage>(serialized);

		// Assert
		result.Name.ShouldBe(original.Name);
		result.Value.ShouldBe(original.Value);
	}

	[Fact]
	public void DeserializeTransportMessage_WithMemoryPackMagicByte_UsesMemoryPackSerializer()
	{
		// Arrange
		var registry = CreateRegistryWithMemoryPack();
		var sut = new PayloadSerializer(registry, _logger);
		var original = new TestMessage { Name = "MemoryPackTransport", Value = 100 };
		var serialized = sut.Serialize(original);

		// Verify magic byte is MemoryPack (1)
		serialized[0].ShouldBe(SerializerIds.MemoryPack);

		// Act
		var result = sut.DeserializeTransportMessage<TestMessage>(serialized);

		// Assert
		result.Name.ShouldBe(original.Name);
		result.Value.ShouldBe(original.Value);
	}

	[Fact]
	public void DeserializeTransportMessage_WithSystemTextJsonMagicByte_UsesSystemTextJsonSerializer()
	{
		// Arrange
		var registry = CreateRegistryWithSystemTextJson();
		var sut = new PayloadSerializer(registry, _logger);
		var original = new TestMessage { Name = "STJTransport", Value = 200 };
		var serialized = sut.Serialize(original);

		// Verify magic byte is SystemTextJson (2)
		serialized[0].ShouldBe(SerializerIds.SystemTextJson);

		// Act
		var result = sut.DeserializeTransportMessage<TestMessage>(serialized);

		// Assert
		result.Name.ShouldBe(original.Name);
		result.Value.ShouldBe(original.Value);
	}

	[Theory]
	[InlineData(SerializerIds.MemoryPack)]       // 1
	[InlineData(SerializerIds.SystemTextJson)]  // 2
	[InlineData(SerializerIds.MessagePack)]     // 3
	[InlineData(SerializerIds.Protobuf)]        // 4
	[InlineData(5)]                              // Framework reserved start
	[InlineData(199)]                            // Framework reserved end
	[InlineData(SerializerIds.CustomRangeStart)] // 200
	[InlineData(SerializerIds.CustomRangeEnd)]   // 254
	public void DeserializeTransportMessage_WithValidMagicBytes_RecognizesAsOurFormat(byte magicByte)
	{
		// This test validates IsValidSerializerId returns true for all valid IDs
		SerializerIds.IsValidSerializerId(magicByte).ShouldBeTrue(
			$"Magic byte {magicByte} (0x{magicByte:X2}) should be recognized as valid");
	}

	[Fact]
	public void DeserializeTransportMessage_WithLegacySerializer_UsesLegacyForMigration()
	{
		// Arrange - Setup dual registration with MemoryPack and STJ
		var registry = CreateRegistryWithBothSerializers();
		var sut = new PayloadSerializer(registry, _logger);

		// Serialize with MemoryPack
		registry.SetCurrent("MemoryPack");
		var original = new TestMessage { Name = "LegacyMigration", Value = 300 };
		var memoryPackData = sut.Serialize(original);

		// Switch current to STJ
		registry.SetCurrent("System.Text.Json");

		// Act - Should still deserialize MemoryPack data via legacy path
		var result = sut.DeserializeTransportMessage<TestMessage>(memoryPackData);

		// Assert
		result.Name.ShouldBe(original.Name);
		result.Value.ShouldBe(original.Value);
	}

	#endregion Our Magic Byte Format Tests (1-254)

	#region Confluent Schema Registry Format Tests (0x00 + 4-byte header)

	[Fact]
	public void DeserializeTransportMessage_WithConfluentFormatAndJsonObject_DeserializesJson()
	{
		// Arrange
		var registry = CreateRegistryWithMemoryPack();
		var sut = new PayloadSerializer(registry, _logger);

		// Confluent format: [0x00][4-byte schema ID][JSON payload]
		var jsonPayload = JsonSerializer.SerializeToUtf8Bytes(new TestMessage { Name = "Confluent", Value = 500 });
		var confluentData = new byte[5 + jsonPayload.Length];
		confluentData[0] = 0x00; // Confluent magic byte
								 // Schema ID bytes [1-4] set to zero (not used for JSON)
		Buffer.BlockCopy(jsonPayload, 0, confluentData, 5, jsonPayload.Length);

		// Act
		var result = sut.DeserializeTransportMessage<TestMessage>(confluentData);

		// Assert
		result.Name.ShouldBe("Confluent");
		result.Value.ShouldBe(500);
	}

	[Fact]
	public void DeserializeTransportMessage_WithConfluentFormatAndJsonArray_DeserializesJson()
	{
		// Arrange
		var registry = CreateRegistryWithMemoryPack();
		var sut = new PayloadSerializer(registry, _logger);

		// JSON array for testing
		var items = new[] { "item1", "item2", "item3" };
		var jsonPayload = JsonSerializer.SerializeToUtf8Bytes(items);
		var confluentData = new byte[5 + jsonPayload.Length];
		confluentData[0] = 0x00; // Confluent magic byte
		Buffer.BlockCopy(jsonPayload, 0, confluentData, 5, jsonPayload.Length);

		// Act
		var result = sut.DeserializeTransportMessage<string[]>(confluentData);

		// Assert
		result.ShouldBe(items);
	}

	[Fact]
	public void DeserializeTransportMessage_WithConfluentFormatAndAvro_ThrowsFormatNotSupported()
	{
		// Arrange
		var registry = CreateRegistryWithMemoryPack();
		var sut = new PayloadSerializer(registry, _logger);

		// Confluent format with non-JSON payload (simulating Avro)
		// Avro binary typically starts with bytes like 0x02 (schema version)
		var confluentData = new byte[]
		{
			0x00,                   // Confluent magic byte
			0x00, 0x00, 0x00, 0x01, // Schema ID = 1
			0x02, 0x14, 0x61, 0x76  // Some Avro-like binary data (not JSON)
		};

		// Act & Assert
		var ex = Should.Throw<SerializationException>(() =>
			sut.DeserializeTransportMessage<TestMessage>(confluentData));
		ex.Message.ShouldContain("Confluent Schema Registry Avro/Protobuf");
		ex.Message.ShouldContain("not supported");
	}

	[Fact]
	public void DeserializeTransportMessage_WithConfluentFormatAndProtobuf_ThrowsFormatNotSupported()
	{
		// Arrange
		var registry = CreateRegistryWithMemoryPack();
		var sut = new PayloadSerializer(registry, _logger);

		// Confluent format with Protobuf payload (starts with field tags, not JSON)
		var confluentData = new byte[]
		{
			0x00,                   // Confluent magic byte
			0x00, 0x00, 0x00, 0x02, // Schema ID = 2
			0x0A, 0x08, 0x54, 0x65  // Protobuf-like bytes (wire type 2, field 1)
		};

		// Act & Assert
		var ex = Should.Throw<SerializationException>(() =>
			sut.DeserializeTransportMessage<TestMessage>(confluentData));
		ex.Message.ShouldContain("Confluent Schema Registry Avro/Protobuf");
	}

	[Fact]
	public void DeserializeTransportMessage_WithConfluentFormatShortHeader_FallsToUnknownFormat()
	{
		// Arrange
		var registry = CreateRegistryWithMemoryPack();
		var sut = new PayloadSerializer(registry, _logger);

		// Confluent magic byte (0x00) but less than 5 bytes total
		// This should NOT match Confluent format (needs 5+ bytes)
		var shortData = new byte[] { 0x00, 0x01, 0x02 }; // Only 3 bytes

		// Act & Assert
		// Since 0x00 is not a valid serializer ID (SerializerIds.Invalid)
		// and there's not enough bytes for Confluent format,
		// and it's not raw JSON, this should throw UnknownFormat
		var ex = Should.Throw<SerializationException>(() =>
			sut.DeserializeTransportMessage<TestMessage>(shortData));
		ex.Message.ShouldContain("Unknown", Case.Insensitive);
	}

	[Fact]
	public void DeserializeTransportMessage_WithConfluentFormatEmptyPayload_ThrowsError()
	{
		// Arrange
		var registry = CreateRegistryWithMemoryPack();
		var sut = new PayloadSerializer(registry, _logger);

		// Confluent format with exactly 5 bytes (header only, no payload)
		var confluentHeaderOnly = new byte[] { 0x00, 0x00, 0x00, 0x00, 0x01 };

		// Act & Assert
		// The payload after header is empty, so it's neither JSON nor Avro
		var ex = Should.Throw<SerializationException>(() =>
			sut.DeserializeTransportMessage<TestMessage>(confluentHeaderOnly));
		// Should throw FormatNotSupported since first byte after header isn't JSON
		ex.Message.ShouldContain("not supported");
	}

	#endregion Confluent Schema Registry Format Tests (0x00 + 4-byte header)

	#region Raw JSON Format Tests (0x7B '{' or 0x5B '[')

	// NOTE: Raw JSON detection now works correctly. The implementation checks if a serializer
	// is actually registered for the ID (via IsRegistered), not just if the ID is valid (1-254).
	// This allows bytes like 0x7B ('{') and 0x5B ('[') to fall through to JSON detection
	// when no serializer is registered at those IDs.
	// Fix implemented in bd-w09j: PayloadSerializer.DeserializeTransportMessage now uses
	// IsValidSerializerId(firstByte) && _registry.IsRegistered(firstByte)

	[Fact]
	public void DeserializeTransportMessage_WithRawJsonObject_DeserializesCorrectly()
	{
		// Arrange
		var registry = CreateRegistryWithMemoryPack();
		var sut = new PayloadSerializer(registry, _logger);

		// Raw JSON object (external system format)
		var original = new TestMessage { Name = "RawJson", Value = 600 };
		var rawJsonData = JsonSerializer.SerializeToUtf8Bytes(original);

		// Verify first byte is '{' (0x7B = 123)
		rawJsonData[0].ShouldBe((byte)'{');

		// Act - Raw JSON now works because we check IsRegistered(123) which returns false
		var result = sut.DeserializeTransportMessage<TestMessage>(rawJsonData);

		// Assert
		_ = result.ShouldNotBeNull();
		result.Name.ShouldBe("RawJson");
		result.Value.ShouldBe(600);
	}

	[Fact]
	public void DeserializeTransportMessage_WithRawJsonArray_DeserializesCorrectly()
	{
		// Arrange
		var registry = CreateRegistryWithMemoryPack();
		var sut = new PayloadSerializer(registry, _logger);

		// JSON array for testing
		var items = new[] { "item1", "item2", "item3" };
		var rawJsonArray = JsonSerializer.SerializeToUtf8Bytes(items);

		// Verify first byte is '[' (0x5B = 91)
		rawJsonArray[0].ShouldBe((byte)'[');

		// Act - Raw JSON array now works because we check IsRegistered(91) which returns false
		var result = sut.DeserializeTransportMessage<string[]>(rawJsonArray);

		// Assert
		_ = result.ShouldNotBeNull();
		result.Length.ShouldBe(3);
		result[0].ShouldBe("item1");
		result[1].ShouldBe("item2");
		result[2].ShouldBe("item3");
	}

	[Fact]
	public void DeserializeTransportMessage_WithRawJsonAndWhitespace_MayNotParse()
	{
		// Arrange
		var registry = CreateRegistryWithMemoryPack();
		var sut = new PayloadSerializer(registry, _logger);

		// JSON with leading whitespace - first byte is space (0x20), not '{'
		var jsonWithWhitespace = Encoding.UTF8.GetBytes("  {\"Name\":\"Test\",\"Value\":1}");

		// First byte is space (0x20), which is not in our valid ranges
		jsonWithWhitespace[0].ShouldBe((byte)' ');

		// Act & Assert
		// This should throw UnknownFormat since 0x20 is not:
		// - A valid serializer ID (1-254)
		// - Confluent format (0x00)
		// - Raw JSON (0x7B or 0x5B)
		var ex = Should.Throw<SerializationException>(() =>
			sut.DeserializeTransportMessage<TestMessage>(jsonWithWhitespace));
		ex.Message.ShouldContain("Unknown", Case.Insensitive);
	}

	[Fact]
	public void DeserializeTransportMessage_WithRawJsonComplexObject_DeserializesCorrectly()
	{
		// Arrange
		var registry = CreateRegistryWithMemoryPack();
		var sut = new PayloadSerializer(registry, _logger);

		var complex = new ComplexTestMessage
		{
			Id = "external-system-1",
			Nested = new TestMessage { Name = "NestedFromExternal", Value = 999 },
			Tags = ["external", "json"],
			Metadata = new Dictionary<string, int> { ["priority"] = 1 }
		};
		var rawJson = JsonSerializer.SerializeToUtf8Bytes(complex);

		// Act - Raw JSON now works because we check IsRegistered('{') which returns false
		var result = sut.DeserializeTransportMessage<ComplexTestMessage>(rawJson);

		// Assert
		_ = result.ShouldNotBeNull();
		result.Id.ShouldBe("external-system-1");
		_ = result.Nested.ShouldNotBeNull();
		result.Nested.Name.ShouldBe("NestedFromExternal");
		result.Nested.Value.ShouldBe(999);
		result.Tags.ShouldContain("external");
		result.Tags.ShouldContain("json");
		result.Metadata.ShouldContainKeyAndValue("priority", 1);
	}

	#endregion Raw JSON Format Tests (0x7B '{' or 0x5B '[')

	#region Unknown Format Tests

	[Fact]
	public void DeserializeTransportMessage_WithUnknownFirstByte_ThrowsSerializationException()
	{
		// Arrange
		var registry = CreateRegistryWithMemoryPack();
		var sut = new PayloadSerializer(registry, _logger);

		// 0xFF is SerializerIds.Unknown - not a valid format
		var unknownData = new byte[] { 0xFF, 0x01, 0x02, 0x03 };

		// Act & Assert
		var ex = Should.Throw<SerializationException>(() =>
			sut.DeserializeTransportMessage<TestMessage>(unknownData));
		ex.Message.ShouldContain("Unknown");
		ex.Message.ShouldContain("0xFF");
	}

	[Fact]
	public void DeserializeTransportMessage_WithBinaryGarbage_ThrowsSerializationException()
	{
		// Arrange
		var registry = CreateRegistryWithMemoryPack();
		var sut = new PayloadSerializer(registry, _logger);

		// Random bytes starting with 0x80 (not valid serializer ID, not JSON, not Confluent)
		var garbageData = new byte[] { 0x80, 0x12, 0x34, 0x56, 0x78 };

		// Act & Assert
		var ex = Should.Throw<SerializationException>(() =>
			sut.DeserializeTransportMessage<TestMessage>(garbageData));
		ex.Message.ShouldContain("Unknown");
		ex.Message.ShouldContain("0x80");
	}

	[Theory]
	[InlineData(0x00)] // Invalid/Confluent magic (but needs 5+ bytes)
	[InlineData(0xFF)] // Unknown
	public void DeserializeTransportMessage_WithReservedBytes_RejectsShortPayloads(byte reservedByte)
	{
		// Arrange
		var registry = CreateRegistryWithMemoryPack();
		var sut = new PayloadSerializer(registry, _logger);

		// Short payload starting with reserved byte
		var shortData = new byte[] { reservedByte, 0x01 };

		// Act & Assert
		_ = Should.Throw<SerializationException>(() =>
			sut.DeserializeTransportMessage<TestMessage>(shortData));
	}

	#endregion Unknown Format Tests

	#region Edge Cases and Error Handling

	[Fact]
	public void DeserializeTransportMessage_WithNull_ThrowsArgumentNullException()
	{
		// Arrange
		var registry = CreateRegistryWithMemoryPack();
		var sut = new PayloadSerializer(registry, _logger);

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			sut.DeserializeTransportMessage<TestMessage>(null!));
	}

	[Fact]
	public void DeserializeTransportMessage_WithEmptyArray_ThrowsSerializationException()
	{
		// Arrange
		var registry = CreateRegistryWithMemoryPack();
		var sut = new PayloadSerializer(registry, _logger);

		// Act & Assert
		var ex = Should.Throw<SerializationException>(() =>
			sut.DeserializeTransportMessage<TestMessage>(Array.Empty<byte>()));
		ex.Message.ShouldContain("empty", Case.Insensitive);
	}

	[Fact]
	public void DeserializeTransportMessage_WithSingleByte_HandlesCorrectly()
	{
		// Arrange
		var registry = CreateRegistryWithMemoryPack();
		var sut = new PayloadSerializer(registry, _logger);

		// Single byte that's a valid serializer ID but no payload
		var singleByte = new byte[] { SerializerIds.MemoryPack };

		// Act & Assert
		// This will try to deserialize empty payload with MemoryPack
		// MemoryPack should fail on empty/invalid data
		_ = Should.Throw<SerializationException>(() =>
			sut.DeserializeTransportMessage<TestMessage>(singleByte));
	}

	[Fact]
	public void DeserializeTransportMessage_WithInvalidJson_ThrowsJsonParsingError()
	{
		// Arrange
		var registry = CreateRegistryWithMemoryPack();
		var sut = new PayloadSerializer(registry, _logger);

		// Invalid JSON starting with '{' but malformed
		var invalidJson = Encoding.UTF8.GetBytes("{invalid json here}");
		invalidJson[0].ShouldBe((byte)'{');

		// Act & Assert
		// Now that raw JSON detection works, invalid JSON correctly throws JSON parsing error
		var ex = Should.Throw<SerializationException>(() =>
			sut.DeserializeTransportMessage<TestMessage>(invalidJson));
		ex.Message.ShouldContain("deserialize JSON", Case.Insensitive);
	}

	[Fact]
	public void DeserializeTransportMessage_WithValidJsonWrongType_ThrowsSerializationException()
	{
		// Arrange
		var registry = CreateRegistryWithMemoryPack();
		var sut = new PayloadSerializer(registry, _logger);

		// Valid JSON but wrong schema - trying to deserialize string as TestMessage
		var stringJson = Encoding.UTF8.GetBytes("\"just a string\"");

		// Note: This starts with '"' (0x22), not '{' or '['
		// So it will be treated as unknown format
		var ex = Should.Throw<SerializationException>(() =>
			sut.DeserializeTransportMessage<TestMessage>(stringJson));
		ex.Message.ShouldContain("Unknown");
	}

	[Fact]
	public void DeserializeTransportMessage_WithConfluentFormatValidJson_PreservesAllProperties()
	{
		// Arrange
		var registry = CreateRegistryWithMemoryPack();
		var sut = new PayloadSerializer(registry, _logger);

		var original = new TestMessage
		{
			Name = "ConfluentPreserve",
			Value = 12345,
			Timestamp = new DateTime(2025, 6, 15, 10, 30, 0, DateTimeKind.Utc)
		};

		var jsonPayload = JsonSerializer.SerializeToUtf8Bytes(original);
		var confluentData = new byte[5 + jsonPayload.Length];
		confluentData[0] = 0x00;
		// Schema ID = 42 (arbitrary)
		confluentData[1] = 0x00;
		confluentData[2] = 0x00;
		confluentData[3] = 0x00;
		confluentData[4] = 0x2A;
		Buffer.BlockCopy(jsonPayload, 0, confluentData, 5, jsonPayload.Length);

		// Act
		var result = sut.DeserializeTransportMessage<TestMessage>(confluentData);

		// Assert
		result.Name.ShouldBe(original.Name);
		result.Value.ShouldBe(original.Value);
		result.Timestamp.ShouldBe(original.Timestamp);
	}

	#endregion Edge Cases and Error Handling

	#region Helper Methods

	private static SerializerRegistry CreateRegistryWithMemoryPack()
	{
		var registry = new SerializerRegistry();
		var serializer = new MemoryPackPluggableSerializer();
		registry.Register(SerializerIds.MemoryPack, serializer);
		registry.SetCurrent("MemoryPack");
		return registry;
	}

	private static SerializerRegistry CreateRegistryWithSystemTextJson()
	{
		var registry = new SerializerRegistry();
		var serializer = new SystemTextJsonPluggableSerializer();
		registry.Register(SerializerIds.SystemTextJson, serializer);
		registry.SetCurrent("System.Text.Json");
		return registry;
	}

	private static SerializerRegistry CreateRegistryWithBothSerializers()
	{
		var registry = new SerializerRegistry();
		registry.Register(SerializerIds.MemoryPack, new MemoryPackPluggableSerializer());
		registry.Register(SerializerIds.SystemTextJson, new SystemTextJsonPluggableSerializer());
		registry.SetCurrent("MemoryPack");
		return registry;
	}

	#endregion Helper Methods
}
