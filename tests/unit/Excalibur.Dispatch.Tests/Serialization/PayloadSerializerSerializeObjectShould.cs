// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions.Serialization;
using Excalibur.Dispatch.Serialization;
using Excalibur.Dispatch.Serialization.MemoryPack;
using Excalibur.Dispatch.Tests.Serialization.TestData;

using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Dispatch.Tests.Serialization;

/// <summary>
/// Unit tests for <see cref="PayloadSerializer.SerializeObject"/> validating
/// runtime type serialization, interface references, and round-trip scenarios.
/// </summary>
/// <remarks>
/// <para>
/// The <see cref="IPayloadSerializer.SerializeObject(object, Type)"/> method is designed for scenarios
/// where the compile-time type differs from the runtime type. Binary serializers like MemoryPack
/// require concrete types for proper serialization, so this method allows specifying the exact
/// runtime type to use.
/// </para>
/// <para>
/// Key test scenarios:
/// </para>
/// <list type="bullet">
///   <item>Concrete type serialization - basic functionality verification</item>
///   <item>Interface reference serialization - the primary use case</item>
///   <item>Round-trip with runtime type - end-to-end verification</item>
///   <item>Error handling - null values, null types, serialization failures</item>
/// </list>
/// </remarks>
[Trait("Category", "Unit")]
public sealed partial class PayloadSerializerSerializeObjectShould
{
	private readonly ILogger<PayloadSerializer> _logger = NullLogger<PayloadSerializer>.Instance;

	#region Concrete Type Serialization Tests

	[Fact]
	public void SerializeObject_WithConcreteType_PrependsCorrectMagicByte()
	{
		// Arrange
		var registry = CreateRegistryWithMemoryPack();
		var sut = new PayloadSerializer(registry, _logger);
		var message = new TestMessage { Name = "Test", Value = 42 };

		// Act
		var result = sut.SerializeObject(message, typeof(TestMessage));

		// Assert
		result.ShouldNotBeEmpty();
		result[0].ShouldBe(SerializerIds.MemoryPack);
	}

	[Fact]
	public void SerializeObject_WithConcreteType_ProducesDeserializablePayload()
	{
		// Arrange
		var registry = CreateRegistryWithMemoryPack();
		var sut = new PayloadSerializer(registry, _logger);
		var original = new TestMessage { Name = "ConcreteTest", Value = 123 };

		// Act
		var serialized = sut.SerializeObject(original, typeof(TestMessage));
		var deserialized = sut.Deserialize<TestMessage>(serialized);

		// Assert
		deserialized.Name.ShouldBe(original.Name);
		deserialized.Value.ShouldBe(original.Value);
	}

	[Fact]
	public void SerializeObject_WithComplexType_ProducesDeserializablePayload()
	{
		// Arrange
		var registry = CreateRegistryWithMemoryPack();
		var sut = new PayloadSerializer(registry, _logger);
		var original = new ComplexTestMessage
		{
			Id = "complex-serialize-object",
			Nested = new TestMessage { Name = "Nested", Value = 999 },
			Tags = ["tag1", "tag2", "tag3"],
			Metadata = new Dictionary<string, int>
			{
				["count"] = 42,
				["priority"] = 1
			}
		};

		// Act
		var serialized = sut.SerializeObject(original, typeof(ComplexTestMessage));
		var deserialized = sut.Deserialize<ComplexTestMessage>(serialized);

		// Assert
		deserialized.Id.ShouldBe(original.Id);
		_ = deserialized.Nested.ShouldNotBeNull();
		deserialized.Nested.Name.ShouldBe("Nested");
		deserialized.Nested.Value.ShouldBe(999);
		deserialized.Tags.ShouldBe(original.Tags);
		deserialized.Metadata.ShouldBe(original.Metadata);
	}

	[Fact]
	public void SerializeObject_WithMemoryPack_UsesMagicByte1()
	{
		// Arrange
		var registry = CreateRegistryWithMemoryPack();
		var sut = new PayloadSerializer(registry, _logger);
		var message = new TestMessage { Name = "Test", Value = 42 };

		// Act
		var result = sut.SerializeObject(message, typeof(TestMessage));

		// Assert
		result[0].ShouldBe((byte)1); // SerializerIds.MemoryPack = 1
	}

	[Fact]
	public void SerializeObject_WithSystemTextJson_UsesMagicByte2()
	{
		// Arrange
		var registry = CreateRegistryWithSystemTextJson();
		var sut = new PayloadSerializer(registry, _logger);
		var message = new TestMessage { Name = "Test", Value = 42 };

		// Act
		var result = sut.SerializeObject(message, typeof(TestMessage));

		// Assert
		result[0].ShouldBe((byte)2); // SerializerIds.SystemTextJson = 2
	}

	#endregion Concrete Type Serialization Tests

	#region Interface Reference Serialization Tests

	[Fact]
	public void SerializeObject_WithInterfaceReference_SerializesUsingRuntimeType()
	{
		// Arrange
		var registry = CreateRegistryWithMemoryPack();
		var sut = new PayloadSerializer(registry, _logger);
		ITestMessageInterface message = new ConcreteTestMessage
		{
			Name = "InterfaceTest",
			Value = 456,
			ExtraData = "Additional info"
		};

		// Act - Serialize using the concrete runtime type
		var serialized = sut.SerializeObject(message, message.GetType());

		// Assert - Should produce valid serialized data with magic byte
		serialized.ShouldNotBeEmpty();
		serialized[0].ShouldBe(SerializerIds.MemoryPack);

		// Verify round-trip works
		var deserialized = sut.Deserialize<ConcreteTestMessage>(serialized);
		deserialized.Name.ShouldBe("InterfaceTest");
		deserialized.Value.ShouldBe(456);
		deserialized.ExtraData.ShouldBe("Additional info");
	}

	[Fact]
	public void SerializeObject_WithInterfaceReferenceAndConcreteType_WorksCorrectly()
	{
		// Arrange
		var registry = CreateRegistryWithMemoryPack();
		var sut = new PayloadSerializer(registry, _logger);
		ITestMessageInterface message = new ConcreteTestMessage
		{
			Name = "PolymorphicTest",
			Value = 789
		};

		// Act - Explicitly pass typeof(ConcreteTestMessage)
		var serialized = sut.SerializeObject(message, typeof(ConcreteTestMessage));
		var deserialized = sut.Deserialize<ConcreteTestMessage>(serialized);

		// Assert
		deserialized.Name.ShouldBe("PolymorphicTest");
		deserialized.Value.ShouldBe(789);
	}

	[Fact]
	public void SerializeObject_WithDerivedTypeAsBase_SerializesCorrectly()
	{
		// Arrange
		var registry = CreateRegistryWithMemoryPack();
		var sut = new PayloadSerializer(registry, _logger);
		var derived = new DerivedTestMessage
		{
			Name = "DerivedTest",
			Value = 111,
			DerivedProperty = "Extended"
		};

		// Act - Serialize as base type
		var serialized = sut.SerializeObject(derived, typeof(BaseTestMessage));
		var deserialized = sut.Deserialize<BaseTestMessage>(serialized);

		// Assert - Base properties should be preserved
		deserialized.Name.ShouldBe("DerivedTest");
		deserialized.Value.ShouldBe(111);
	}

	[Fact]
	public void SerializeObject_WithDerivedTypeAsDerived_PreservesDerivedProperties()
	{
		// Arrange
		var registry = CreateRegistryWithMemoryPack();
		var sut = new PayloadSerializer(registry, _logger);
		var derived = new DerivedTestMessage
		{
			Name = "DerivedFull",
			Value = 222,
			DerivedProperty = "ExtendedValue"
		};

		// Act - Serialize as derived type
		var serialized = sut.SerializeObject(derived, typeof(DerivedTestMessage));
		var deserialized = sut.Deserialize<DerivedTestMessage>(serialized);

		// Assert - All properties should be preserved
		deserialized.Name.ShouldBe("DerivedFull");
		deserialized.Value.ShouldBe(222);
		deserialized.DerivedProperty.ShouldBe("ExtendedValue");
	}

	#endregion Interface Reference Serialization Tests

	#region Round-Trip with Runtime Type Tests

	[Fact]
	public void SerializeObject_ThenDeserialize_RoundTripWithMemoryPack()
	{
		// Arrange
		var registry = CreateRegistryWithMemoryPack();
		var sut = new PayloadSerializer(registry, _logger);
		var original = new TestMessage
		{
			Name = "RoundTripObject",
			Value = 12345,
			Timestamp = new DateTime(2025, 1, 15, 10, 30, 0, DateTimeKind.Utc)
		};

		// Act
		var serialized = sut.SerializeObject(original, typeof(TestMessage));
		var deserialized = sut.Deserialize<TestMessage>(serialized);

		// Assert
		deserialized.Name.ShouldBe(original.Name);
		deserialized.Value.ShouldBe(original.Value);
		deserialized.Timestamp.ShouldBe(original.Timestamp);
	}

	[Fact]
	public void SerializeObject_ThenDeserialize_RoundTripWithSystemTextJson()
	{
		// Arrange
		var registry = CreateRegistryWithSystemTextJson();
		var sut = new PayloadSerializer(registry, _logger);
		var original = new TestMessage
		{
			Name = "JsonRoundTripObject",
			Value = 54321,
			Timestamp = new DateTime(2025, 6, 20, 14, 45, 0, DateTimeKind.Utc)
		};

		// Act
		var serialized = sut.SerializeObject(original, typeof(TestMessage));
		var deserialized = sut.Deserialize<TestMessage>(serialized);

		// Assert
		deserialized.Name.ShouldBe(original.Name);
		deserialized.Value.ShouldBe(original.Value);
		deserialized.Timestamp.ShouldBe(original.Timestamp);
	}

	[Fact]
	public void SerializeObject_ThenDeserialize_CrossSerializerMigration()
	{
		// Arrange
		var registry = CreateRegistryWithBothSerializers();
		var sut = new PayloadSerializer(registry, _logger);
		var original = new TestMessage { Name = "MigrationObject", Value = 333 };

		// Act - Serialize with MemoryPack
		registry.SetCurrent("MemoryPack");
		var memoryPackData = sut.SerializeObject(original, typeof(TestMessage));

		// Switch to STJ
		registry.SetCurrent("System.Text.Json");
		var stjData = sut.SerializeObject(original, typeof(TestMessage));

		// Deserialize both (should use migration path for MemoryPack data)
		var fromMemoryPack = sut.Deserialize<TestMessage>(memoryPackData);
		var fromStj = sut.Deserialize<TestMessage>(stjData);

		// Assert
		fromMemoryPack.Name.ShouldBe(original.Name);
		fromMemoryPack.Value.ShouldBe(original.Value);
		fromStj.Name.ShouldBe(original.Name);
		fromStj.Value.ShouldBe(original.Value);
	}

	[Fact]
	public void SerializeObject_ProducesSameResultAsGenericSerialize()
	{
		// Arrange
		var registry = CreateRegistryWithMemoryPack();
		var sut = new PayloadSerializer(registry, _logger);
		var message = new TestMessage { Name = "Comparison", Value = 555 };

		// Act
		var genericResult = sut.Serialize(message);
		var objectResult = sut.SerializeObject(message, typeof(TestMessage));

		// Assert - Both should produce equivalent payloads
		genericResult.Length.ShouldBe(objectResult.Length);
		genericResult[0].ShouldBe(objectResult[0]); // Same magic byte
													// Payload content may differ slightly due to serializer internals,
													// but both should deserialize to same value
		var fromGeneric = sut.Deserialize<TestMessage>(genericResult);
		var fromObject = sut.Deserialize<TestMessage>(objectResult);
		fromGeneric.Name.ShouldBe(fromObject.Name);
		fromGeneric.Value.ShouldBe(fromObject.Value);
	}

	#endregion Round-Trip with Runtime Type Tests

	#region Error Handling Tests

	[Fact]
	public void SerializeObject_WithNullValue_ThrowsArgumentNullException()
	{
		// Arrange
		var registry = CreateRegistryWithMemoryPack();
		var sut = new PayloadSerializer(registry, _logger);

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			sut.SerializeObject(null!, typeof(TestMessage)));
	}

	[Fact]
	public void SerializeObject_WithNullType_ThrowsArgumentNullException()
	{
		// Arrange
		var registry = CreateRegistryWithMemoryPack();
		var sut = new PayloadSerializer(registry, _logger);
		var message = new TestMessage { Name = "Test", Value = 1 };

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			sut.SerializeObject(message, null!));
	}

	[Fact]
	public void SerializeObject_WhenNoCurrentSerializer_ThrowsInvalidOperationException()
	{
		// Arrange
		var registry = new SerializerRegistry();
		var sut = new PayloadSerializer(registry, _logger);
		var message = new TestMessage { Name = "Test", Value = 1 };

		// Act & Assert
		_ = Should.Throw<InvalidOperationException>(() =>
			sut.SerializeObject(message, typeof(TestMessage)));
	}

	[Fact]
	public void SerializeObject_WithTypeMismatch_ThrowsSerializationException()
	{
		// Arrange
		var registry = CreateRegistryWithSystemTextJson();
		var sut = new PayloadSerializer(registry, _logger);
		var message = new TestMessage { Name = "Test", Value = 42 };

		// Act & Assert - Pass mismatched type (ComplexTestMessage instead of TestMessage)
		// System.Text.Json validates that the value is assignable to the specified type
		// and throws ArgumentException which is wrapped in SerializationException
		var ex = Should.Throw<SerializationException>(() =>
			sut.SerializeObject(message, typeof(ComplexTestMessage)));
		ex.Message.ShouldContain("serialize");
		_ = ex.InnerException.ShouldBeOfType<ArgumentException>();
	}

	[Fact]
	public void SerializeObject_WhenSerializerThrows_WrapsInSerializationException()
	{
		// Arrange
		var registry = new SerializerRegistry();
		var faultySerializer = new FaultyPluggableSerializer();
		registry.Register(100, faultySerializer);
		registry.SetCurrent("Faulty");
		var sut = new PayloadSerializer(registry, _logger);
		var message = new TestMessage { Name = "Test", Value = 1 };

		// Act & Assert
		var ex = Should.Throw<SerializationException>(() =>
			sut.SerializeObject(message, typeof(TestMessage)));
		ex.Message.ShouldContain("serialize");
		_ = ex.InnerException.ShouldNotBeNull();
		ex.InnerException.Message.ShouldContain("Intentional");
	}

	#endregion Error Handling Tests

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

	#region Test Helpers

	/// <summary>
	/// Faulty serializer for testing exception handling.
	/// </summary>
	private sealed class FaultyPluggableSerializer : IPluggableSerializer
	{
		public string Name => "Faulty";
		public string Version => "1.0.0";

		public byte[] Serialize<T>(T value)
			=> throw new InvalidOperationException("Intentional serialization failure");

		public T Deserialize<T>(ReadOnlySpan<byte> data)
			=> throw new InvalidOperationException("Intentional deserialization failure");

		public byte[] SerializeObject(object value, Type type)
			=> throw new InvalidOperationException("Intentional serialization failure");

		public object DeserializeObject(ReadOnlySpan<byte> data, Type type)
			=> throw new InvalidOperationException("Intentional deserialization failure");
	}

	#endregion Test Helpers
}
