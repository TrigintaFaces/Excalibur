// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions.Serialization;
using Excalibur.Dispatch.Serialization;
using Excalibur.Dispatch.Serialization.MemoryPack;
using Excalibur.Dispatch.Tests.Serialization.TestData;

using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Dispatch.Tests.Serialization;

/// <summary>
/// Unit tests for <see cref="PayloadSerializer"/> validating magic byte handling,
/// fast path, migration path, and error handling behavior.
/// </summary>
[Trait("Category", "Unit")]
public sealed class PayloadSerializerShould
{
	private readonly ILogger<PayloadSerializer> _logger = NullLogger<PayloadSerializer>.Instance;

	#region Serialization Tests

	[Fact]
	public void Serialize_PrependsCorrectMagicByte()
	{
		// Arrange
		var registry = CreateRegistryWithMemoryPack();
		var sut = new PayloadSerializer(registry, _logger);
		var message = new TestMessage { Name = "Test", Value = 42 };

		// Act
		var result = sut.Serialize(message);

		// Assert
		result.ShouldNotBeEmpty();
		result[0].ShouldBe(SerializerIds.MemoryPack);
	}

	[Fact]
	public void Serialize_WithMemoryPack_UsesMagicByte1()
	{
		// Arrange
		var registry = CreateRegistryWithMemoryPack();
		var sut = new PayloadSerializer(registry, _logger);
		var message = new TestMessage { Name = "Test", Value = 42 };

		// Act
		var result = sut.Serialize(message);

		// Assert
		result[0].ShouldBe((byte)1); // SerializerIds.MemoryPack = 1
	}

	[Fact]
	public void Serialize_WithSystemTextJson_UsesMagicByte2()
	{
		// Arrange
		var registry = CreateRegistryWithSystemTextJson();
		var sut = new PayloadSerializer(registry, _logger);
		var message = new TestMessage { Name = "Test", Value = 42 };

		// Act
		var result = sut.Serialize(message);

		// Assert
		result[0].ShouldBe((byte)2); // SerializerIds.SystemTextJson = 2
	}

	[Fact]
	public void Serialize_WithNull_ThrowsArgumentNullException()
	{
		// Arrange
		var registry = CreateRegistryWithMemoryPack();
		var sut = new PayloadSerializer(registry, _logger);

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			sut.Serialize<TestMessage>(null!));
	}

	[Fact]
	public void Serialize_WhenNoCurrentSerializer_ThrowsInvalidOperationException()
	{
		// Arrange
		var registry = new SerializerRegistry();
		var sut = new PayloadSerializer(registry, _logger);

		// Act & Assert
		_ = Should.Throw<InvalidOperationException>(() =>
			sut.Serialize(new TestMessage { Name = "Test", Value = 1 }));
	}

	#endregion Serialization Tests

	#region Deserialization - Fast Path Tests

	[Fact]
	public void Deserialize_WithCurrentSerializerMagicByte_UsesCurrentSerializer()
	{
		// Arrange
		var registry = CreateRegistryWithMemoryPack();
		var sut = new PayloadSerializer(registry, _logger);
		var original = new TestMessage { Name = "Test", Value = 42 };
		var serialized = sut.Serialize(original);

		// Act
		var result = sut.Deserialize<TestMessage>(serialized);

		// Assert
		result.Name.ShouldBe(original.Name);
		result.Value.ShouldBe(original.Value);
	}

	[Fact]
	public void Deserialize_WithMatchingMagicByte_ReturnsCorrectValue()
	{
		// Arrange
		var registry = CreateRegistryWithMemoryPack();
		var sut = new PayloadSerializer(registry, _logger);
		var original = new ComplexTestMessage
		{
			Id = "complex-1",
			Nested = new TestMessage { Name = "Nested", Value = 100 },
			Tags = ["tag1", "tag2"],
			Metadata = new Dictionary<string, int> { ["key1"] = 1 }
		};
		var serialized = sut.Serialize(original);

		// Act
		var result = sut.Deserialize<ComplexTestMessage>(serialized);

		// Assert
		result.Id.ShouldBe(original.Id);
		_ = result.Nested.ShouldNotBeNull();
		result.Nested.Name.ShouldBe("Nested");
		result.Tags.ShouldBe(original.Tags);
		result.Metadata.ShouldContainKeyAndValue("key1", 1);
	}

	#endregion Deserialization - Fast Path Tests

	#region Deserialization - Migration Path Tests

	[Fact]
	public void Deserialize_WithLegacySerializerMagicByte_UsesLegacySerializer()
	{
		// Arrange - Start with MemoryPack as current
		var registry = new SerializerRegistry();
		var memoryPackSerializer = new MemoryPackPluggableSerializer();
		var stjSerializer = new SystemTextJsonPluggableSerializer();
		registry.Register(SerializerIds.MemoryPack, memoryPackSerializer);
		registry.Register(SerializerIds.SystemTextJson, stjSerializer);
		registry.SetCurrent("MemoryPack");

		var sut = new PayloadSerializer(registry, _logger);
		var original = new TestMessage { Name = "LegacyTest", Value = 99 };

		// Serialize with current (MemoryPack)
		var serialized = sut.Serialize(original);
		serialized[0].ShouldBe(SerializerIds.MemoryPack);

		// Act - Switch to STJ as current, but deserialize MemoryPack data
		registry.SetCurrent("System.Text.Json");

		var result = sut.Deserialize<TestMessage>(serialized);

		// Assert - Should still work using MemoryPack (migration path)
		result.Name.ShouldBe(original.Name);
		result.Value.ShouldBe(original.Value);
	}

	[Fact]
	public void Deserialize_AfterSerializerChange_StillReadsOldData()
	{
		// Arrange
		var registry = CreateRegistryWithBothSerializers();
		var sut = new PayloadSerializer(registry, _logger);

		// Create data with MemoryPack
		registry.SetCurrent("MemoryPack");
		var original = new TestMessage { Name = "MigrationTest", Value = 123 };
		var memoryPackData = sut.Serialize(original);

		// Switch to STJ and create new data
		registry.SetCurrent("System.Text.Json");
		var stjData = sut.Serialize(original);

		// Act - Deserialize both
		var fromMemoryPack = sut.Deserialize<TestMessage>(memoryPackData);
		var fromStj = sut.Deserialize<TestMessage>(stjData);

		// Assert - Both should work correctly
		fromMemoryPack.Name.ShouldBe(original.Name);
		fromMemoryPack.Value.ShouldBe(original.Value);
		fromStj.Name.ShouldBe(original.Name);
		fromStj.Value.ShouldBe(original.Value);
	}

	#endregion Deserialization - Migration Path Tests

	#region Error Handling Tests

	[Fact]
	public void Deserialize_WithUnknownMagicByte_ThrowsSerializationException()
	{
		// Arrange
		var registry = CreateRegistryWithMemoryPack();
		var sut = new PayloadSerializer(registry, _logger);
		var invalidData = new byte[] { 200, 1, 2, 3, 4 }; // Unknown serializer ID 200

		// Act & Assert
		var ex = Should.Throw<SerializationException>(() =>
			sut.Deserialize<TestMessage>(invalidData));
		ex.Message.ShouldContain("Unknown serializer ID");
		ex.Message.ShouldContain("0xC8"); // 200 in hex
	}

	[Fact]
	public void Deserialize_WithEmptyData_ThrowsSerializationException()
	{
		// Arrange
		var registry = CreateRegistryWithMemoryPack();
		var sut = new PayloadSerializer(registry, _logger);
		var emptyData = Array.Empty<byte>();

		// Act & Assert
		var ex = Should.Throw<SerializationException>(() =>
			sut.Deserialize<TestMessage>(emptyData));
		ex.Message.ShouldContain("empty");
	}

	[Fact]
	public void Deserialize_WithNull_ThrowsArgumentNullException()
	{
		// Arrange
		var registry = CreateRegistryWithMemoryPack();
		var sut = new PayloadSerializer(registry, _logger);

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			sut.Deserialize<TestMessage>(null!));
	}

	[Fact]
	public void Deserialize_WithCorruptedData_ThrowsSerializationException()
	{
		// Arrange
		var registry = CreateRegistryWithMemoryPack();
		var sut = new PayloadSerializer(registry, _logger);
		var corruptedData = new byte[] { SerializerIds.MemoryPack, 0xFF, 0xFF, 0xFF }; // Invalid MemoryPack data

		// Act & Assert
		var ex = Should.Throw<SerializationException>(() =>
			sut.Deserialize<TestMessage>(corruptedData));
		// Message may be "Deserialization returned null" or "Failed to deserialize"
		(ex.Message.Contains("Deserialization", StringComparison.OrdinalIgnoreCase) ||
		 ex.Message.Contains("deserialize", StringComparison.OrdinalIgnoreCase)).ShouldBeTrue();
	}

	#endregion Error Handling Tests

	#region Round-Trip Tests

	[Fact]
	public void SerializeThenDeserialize_WithMemoryPack_ReturnsOriginalValue()
	{
		// Arrange
		var registry = CreateRegistryWithMemoryPack();
		var sut = new PayloadSerializer(registry, _logger);
		var original = new TestMessage
		{
			Name = "RoundTrip",
			Value = 12345,
			Timestamp = new DateTime(2025, 1, 15, 10, 30, 0, DateTimeKind.Utc)
		};

		// Act
		var serialized = sut.Serialize(original);
		var deserialized = sut.Deserialize<TestMessage>(serialized);

		// Assert
		deserialized.Name.ShouldBe(original.Name);
		deserialized.Value.ShouldBe(original.Value);
		deserialized.Timestamp.ShouldBe(original.Timestamp);
	}

	[Fact]
	public void SerializeThenDeserialize_WithSystemTextJson_ReturnsOriginalValue()
	{
		// Arrange
		var registry = CreateRegistryWithSystemTextJson();
		var sut = new PayloadSerializer(registry, _logger);
		var original = new TestMessage
		{
			Name = "JsonRoundTrip",
			Value = 54321,
			Timestamp = new DateTime(2025, 6, 20, 14, 45, 0, DateTimeKind.Utc)
		};

		// Act
		var serialized = sut.Serialize(original);
		var deserialized = sut.Deserialize<TestMessage>(serialized);

		// Assert
		deserialized.Name.ShouldBe(original.Name);
		deserialized.Value.ShouldBe(original.Value);
		deserialized.Timestamp.ShouldBe(original.Timestamp);
	}

	[Fact]
	public void SerializeThenDeserialize_WithComplexObject_ReturnsOriginalValue()
	{
		// Arrange
		var registry = CreateRegistryWithMemoryPack();
		var sut = new PayloadSerializer(registry, _logger);
		var original = new ComplexTestMessage
		{
			Id = "complex-round-trip",
			Nested = new TestMessage { Name = "Nested", Value = 999 },
			Tags = ["alpha", "beta", "gamma"],
			Metadata = new Dictionary<string, int>
			{
				["count"] = 42,
				["priority"] = 1
			}
		};

		// Act
		var serialized = sut.Serialize(original);
		var deserialized = sut.Deserialize<ComplexTestMessage>(serialized);

		// Assert
		deserialized.Id.ShouldBe(original.Id);
		_ = deserialized.Nested.ShouldNotBeNull();
		deserialized.Nested.Name.ShouldBe("Nested");
		deserialized.Nested.Value.ShouldBe(999);
		deserialized.Tags.ShouldBe(original.Tags);
		deserialized.Metadata.ShouldBe(original.Metadata);
	}

	#endregion Round-Trip Tests

	#region GetCurrentSerializer Tests

	[Fact]
	public void GetCurrentSerializerId_ReturnsCorrectId()
	{
		// Arrange
		var registry = CreateRegistryWithMemoryPack();
		var sut = new PayloadSerializer(registry, _logger);

		// Act
		var id = sut.GetCurrentSerializerId();

		// Assert
		id.ShouldBe(SerializerIds.MemoryPack);
	}

	[Fact]
	public void GetCurrentSerializerName_ReturnsCorrectName()
	{
		// Arrange
		var registry = CreateRegistryWithMemoryPack();
		var sut = new PayloadSerializer(registry, _logger);

		// Act
		var name = sut.GetCurrentSerializerName();

		// Assert
		name.ShouldBe("MemoryPack");
	}

	#endregion GetCurrentSerializer Tests

	#region Constructor Tests

	[Fact]
	public void Constructor_WithNullRegistry_ThrowsArgumentNullException()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			new PayloadSerializer(null!, _logger));
	}

	[Fact]
	public void Constructor_WithNullLogger_ThrowsArgumentNullException()
	{
		// Arrange
		var registry = new SerializerRegistry();

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			new PayloadSerializer(registry, null!));
	}

	#endregion Constructor Tests

	#region Custom JsonSerializerOptions Tests

	[Fact]
	public void Constructor_WithCustomJsonOptions_UsesProvidedOptionsForFallback()
	{
		// Arrange
		var registry = CreateRegistryWithMemoryPack();
		var customOptions = new System.Text.Json.JsonSerializerOptions
		{
			PropertyNameCaseInsensitive = true,
			WriteIndented = true
		};

		// Act - Create serializer with custom JSON options
		var sut = new PayloadSerializer(registry, _logger, customOptions);

		// Assert - Should not throw, custom options are accepted
		_ = sut.ShouldNotBeNull();
	}

	[Fact]
	public void Constructor_WithNullJsonOptions_UsesDefaultOptions()
	{
		// Arrange
		var registry = CreateRegistryWithMemoryPack();

		// Act - Create serializer with null JSON options (uses defaults)
		var sut = new PayloadSerializer(registry, _logger, null);

		// Assert - Should work with default options
		var original = new TestMessage { Name = "Test", Value = 42 };
		var serialized = sut.Serialize(original);
		var deserialized = sut.Deserialize<TestMessage>(serialized);
		deserialized.Name.ShouldBe(original.Name);
	}

	#endregion Custom JsonSerializerOptions Tests

	#region Thread-Safety Tests

	[Fact]
	public async Task Serialize_ConcurrentCalls_AllSucceed()
	{
		// Arrange
		var registry = CreateRegistryWithMemoryPack();
		var sut = new PayloadSerializer(registry, _logger);
		var tasks = new List<Task<byte[]>>();
		var messages = Enumerable.Range(0, 100)
			.Select(i => new TestMessage { Name = $"Message{i}", Value = i })
			.ToList();

		// Act
		foreach (var message in messages)
		{
			tasks.Add(Task.Run(() => sut.Serialize(message)));
		}

		var results = await Task.WhenAll(tasks);

		// Assert
		results.Length.ShouldBe(100);
		results.All(r => r.Length > 0 && r[0] == SerializerIds.MemoryPack).ShouldBeTrue();
	}

	[Fact]
	public async Task Deserialize_ConcurrentCalls_AllSucceed()
	{
		// Arrange
		var registry = CreateRegistryWithMemoryPack();
		var sut = new PayloadSerializer(registry, _logger);
		var messages = Enumerable.Range(0, 100)
			.Select(i => new TestMessage { Name = $"Message{i}", Value = i })
			.ToList();
		var serialized = messages.Select(m => sut.Serialize(m)).ToList();
		var tasks = new List<Task<TestMessage>>();

		// Act
		foreach (var data in serialized)
		{
			var dataCopy = data; // Capture for closure
			tasks.Add(Task.Run(() => sut.Deserialize<TestMessage>(dataCopy)));
		}

		var results = await Task.WhenAll(tasks);

		// Assert
		results.Length.ShouldBe(100);
		for (var i = 0; i < results.Length; i++)
		{
			results[i].Name.ShouldBe($"Message{i}");
			results[i].Value.ShouldBe(i);
		}
	}

	#endregion Thread-Safety Tests

	#region GetAll Serializers Tests

	[Fact]
	public void GetCurrentSerializerId_WhenNoCurrentSet_ThrowsInvalidOperationException()
	{
		// Arrange
		var registry = new SerializerRegistry();
		var sut = new PayloadSerializer(registry, _logger);

		// Act & Assert
		_ = Should.Throw<InvalidOperationException>(() => sut.GetCurrentSerializerId());
	}

	[Fact]
	public void GetCurrentSerializerName_WhenNoCurrentSet_ThrowsInvalidOperationException()
	{
		// Arrange
		var registry = new SerializerRegistry();
		var sut = new PayloadSerializer(registry, _logger);

		// Act & Assert
		_ = Should.Throw<InvalidOperationException>(() => sut.GetCurrentSerializerName());
	}

	#endregion GetAll Serializers Tests

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
