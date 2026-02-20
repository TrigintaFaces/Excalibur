// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.CodeAnalysis;

using Excalibur.Dispatch.Abstractions.Serialization;
using Excalibur.Dispatch.Serialization.MessagePack;

using MessagePack;

namespace Excalibur.Dispatch.Serialization.Tests.MessagePack;

/// <summary>
/// Unit tests for <see cref="MessagePackPluggableSerializer" />.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Serialization")]
public sealed class MessagePackPluggableSerializerShould : UnitTestBase
{
	[Fact]
	public void Constructor_WithDefaultOptions_CreatesSerializer()
	{
		// Arrange & Act
		var serializer = new MessagePackPluggableSerializer();

		// Assert
		_ = serializer.ShouldNotBeNull();
		serializer.Name.ShouldBe("MessagePack");
	}

	[Fact]
	public void Constructor_WithNullOptions_UsesStandard()
	{
		// Arrange & Act
		var serializer = new MessagePackPluggableSerializer(null);

		// Assert
		_ = serializer.ShouldNotBeNull();
	}

	[Fact]
	public void Constructor_WithCustomOptions_CreatesSerializer()
	{
		// Arrange
		var options = MessagePackSerializerOptions.Standard.WithCompression(MessagePackCompression.Lz4Block);

		// Act
		var serializer = new MessagePackPluggableSerializer(options);

		// Assert
		_ = serializer.ShouldNotBeNull();
	}

	[Fact]
	public void Name_ReturnsMessagePack()
	{
		// Arrange
		var serializer = new MessagePackPluggableSerializer();

		// Act & Assert
		serializer.Name.ShouldBe("MessagePack");
	}

	[Fact]
	public void Version_ReturnsNonEmptyString()
	{
		// Arrange
		var serializer = new MessagePackPluggableSerializer();

		// Act & Assert
		serializer.Version.ShouldNotBeNullOrWhiteSpace();
	}

	[Fact]
	public void Version_ReturnsValidVersionFormat()
	{
		// Arrange
		var serializer = new MessagePackPluggableSerializer();

		// Act
		var version = serializer.Version;

		// Assert - Version should be a valid version string or "Unknown"
		var isValid = version == "Unknown" || System.Version.TryParse(version, out _);
		isValid.ShouldBeTrue($"Version '{version}' should be a valid version string or 'Unknown'");
	}

	[Fact]
	public void Serialize_AndDeserialize_RoundTrips()
	{
		// Arrange
		var serializer = new MessagePackPluggableSerializer();
		var original = new TestPluggableMessage { Value = 42, Text = "Test" };

		// Act
		var serialized = serializer.Serialize(original);
		var deserialized = serializer.Deserialize<TestPluggableMessage>(serialized);

		// Assert
		_ = deserialized.ShouldNotBeNull();
		deserialized.Value.ShouldBe(original.Value);
		deserialized.Text.ShouldBe(original.Text);
	}

	[Fact]
	public void Serialize_ThrowsOnNullValue()
	{
		// Arrange
		var serializer = new MessagePackPluggableSerializer();

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			serializer.Serialize<TestPluggableMessage>(null!));
	}

	[Fact]
	[RequiresUnreferencedCode("Test")]
	[RequiresDynamicCode("Test")]
	public void Serialize_WithUnserializableType_ThrowsSerializationException()
	{
		// Arrange - Use a type that MessagePack cannot serialize (no attributes, no contractless resolver)
		var serializer = new MessagePackPluggableSerializer();
		var unserializable = new UnserializableType { Data = "test" };

		// Act & Assert
		_ = Should.Throw<SerializationException>(() =>
			serializer.Serialize(unserializable));
	}

	[Fact]
	[RequiresUnreferencedCode("Test")]
	[RequiresDynamicCode("Test")]
	public void Serialize_WithUnserializableType_WrapsInnerException()
	{
		// Arrange
		var serializer = new MessagePackPluggableSerializer();
		var unserializable = new UnserializableType { Data = "test" };

		// Act & Assert
		var ex = Should.Throw<SerializationException>(() =>
			serializer.Serialize(unserializable));
		ex.InnerException.ShouldNotBeNull();
		ex.Message.ShouldContain("serialize");
	}

	[Fact]
	[RequiresUnreferencedCode("Test")]
	[RequiresDynamicCode("Test")]
	public void Deserialize_WithCorruptData_ThrowsSerializationException()
	{
		// Arrange
		var serializer = new MessagePackPluggableSerializer();
		var corruptData = new byte[] { 0xFF, 0xFE, 0xFD, 0xFC, 0xFB };

		// Act & Assert
		_ = Should.Throw<SerializationException>(() =>
			serializer.Deserialize<TestPluggableMessage>(corruptData));
	}

	[Fact]
	[RequiresUnreferencedCode("Test")]
	[RequiresDynamicCode("Test")]
	public void Deserialize_WithEmptyData_ThrowsSerializationException()
	{
		// Arrange
		var serializer = new MessagePackPluggableSerializer();
		var emptyData = Array.Empty<byte>();

		// Act & Assert
		_ = Should.Throw<SerializationException>(() =>
			serializer.Deserialize<TestPluggableMessage>(emptyData));
	}

	[Fact]
	[RequiresUnreferencedCode("Test")]
	[RequiresDynamicCode("Test")]
	public void Deserialize_WithCorruptData_WrapsInnerException()
	{
		// Arrange
		var serializer = new MessagePackPluggableSerializer();
		var corruptData = new byte[] { 0xFF, 0xFE, 0xFD, 0xFC, 0xFB };

		// Act & Assert - Verify the catch wraps the exception properly
		var ex = Should.Throw<SerializationException>(() =>
			serializer.Deserialize<TestPluggableMessage>(corruptData));
		ex.InnerException.ShouldNotBeNull();
		ex.Message.ShouldContain("deserialize");
	}

	[Fact]
	[RequiresUnreferencedCode("Test")]
	[RequiresDynamicCode("Test")]
	public void Deserialize_WhenNullableTypeReturnsNull_ThrowsSerializationException()
	{
		// Arrange - Serialize a MessagePack nil value, then try to deserialize as non-nullable reference type.
		// MessagePack nil is a single byte: 0xC0
		var serializer = new MessagePackPluggableSerializer();
		var nilData = new byte[] { 0xC0 };

		// Act & Assert - The ?? throw path should be hit when MessagePack deserializes nil to null
		var ex = Should.Throw<SerializationException>(() =>
			serializer.Deserialize<TestPluggableMessage>(nilData));
		ex.Message.ShouldContain("null");
	}

	[Fact]
	public void SerializeObject_AndDeserializeObject_RoundTrips()
	{
		// Arrange
		var serializer = new MessagePackPluggableSerializer();
		var original = new TestPluggableMessage { Value = 100, Text = "Object Test" };

		// Act
		var serialized = serializer.SerializeObject(original, typeof(TestPluggableMessage));
		var deserialized = (TestPluggableMessage)serializer.DeserializeObject(serialized, typeof(TestPluggableMessage));

		// Assert
		_ = deserialized.ShouldNotBeNull();
		deserialized.Value.ShouldBe(100);
		deserialized.Text.ShouldBe("Object Test");
	}

	[Fact]
	public void SerializeObject_ThrowsOnNullValue()
	{
		// Arrange
		var serializer = new MessagePackPluggableSerializer();

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			serializer.SerializeObject(null!, typeof(TestPluggableMessage)));
	}

	[Fact]
	public void SerializeObject_ThrowsOnNullType()
	{
		// Arrange
		var serializer = new MessagePackPluggableSerializer();
		var value = new TestPluggableMessage();

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			serializer.SerializeObject(value, null!));
	}

	[Fact]
	[RequiresUnreferencedCode("Test")]
	[RequiresDynamicCode("Test")]
	public void SerializeObject_WithUnserializableType_ThrowsSerializationException()
	{
		// Arrange
		var serializer = new MessagePackPluggableSerializer();
		var unserializable = new UnserializableType { Data = "test" };

		// Act & Assert
		_ = Should.Throw<SerializationException>(() =>
			serializer.SerializeObject(unserializable, typeof(UnserializableType)));
	}

	[Fact]
	[RequiresUnreferencedCode("Test")]
	[RequiresDynamicCode("Test")]
	public void SerializeObject_WithUnserializableType_WrapsInnerException()
	{
		// Arrange
		var serializer = new MessagePackPluggableSerializer();
		var unserializable = new UnserializableType { Data = "test" };

		// Act & Assert
		var ex = Should.Throw<SerializationException>(() =>
			serializer.SerializeObject(unserializable, typeof(UnserializableType)));
		ex.InnerException.ShouldNotBeNull();
		ex.Message.ShouldContain("serialize");
	}

	[Fact]
	public void DeserializeObject_ThrowsOnNullType()
	{
		// Arrange
		var serializer = new MessagePackPluggableSerializer();
		var data = new byte[] { 0x92, 0x01, 0xA4, 0x54, 0x65, 0x73, 0x74 };

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			serializer.DeserializeObject(data, null!));
	}

	[Fact]
	[RequiresUnreferencedCode("Test")]
	[RequiresDynamicCode("Test")]
	public void DeserializeObject_WithCorruptData_ThrowsSerializationException()
	{
		// Arrange
		var serializer = new MessagePackPluggableSerializer();
		var corruptData = new byte[] { 0xFF, 0xFE, 0xFD, 0xFC, 0xFB };

		// Act & Assert
		_ = Should.Throw<SerializationException>(() =>
			serializer.DeserializeObject(corruptData, typeof(TestPluggableMessage)));
	}

	[Fact]
	[RequiresUnreferencedCode("Test")]
	[RequiresDynamicCode("Test")]
	public void DeserializeObject_WithEmptyData_ThrowsSerializationException()
	{
		// Arrange
		var serializer = new MessagePackPluggableSerializer();
		var emptyData = Array.Empty<byte>();

		// Act & Assert
		_ = Should.Throw<SerializationException>(() =>
			serializer.DeserializeObject(emptyData, typeof(TestPluggableMessage)));
	}

	[Fact]
	[RequiresUnreferencedCode("Test")]
	[RequiresDynamicCode("Test")]
	public void DeserializeObject_WithCorruptData_WrapsInnerException()
	{
		// Arrange
		var serializer = new MessagePackPluggableSerializer();
		var corruptData = new byte[] { 0xFF, 0xFE, 0xFD, 0xFC, 0xFB };

		// Act & Assert
		var ex = Should.Throw<SerializationException>(() =>
			serializer.DeserializeObject(corruptData, typeof(TestPluggableMessage)));
		ex.InnerException.ShouldNotBeNull();
		ex.Message.ShouldContain("deserialize");
	}

	[Fact]
	[RequiresUnreferencedCode("Test")]
	[RequiresDynamicCode("Test")]
	public void DeserializeObject_WhenNullableTypeReturnsNull_ThrowsSerializationException()
	{
		// Arrange - Serialize a MessagePack nil value to trigger the NullResultForType path
		var serializer = new MessagePackPluggableSerializer();
		var nilData = new byte[] { 0xC0 };

		// Act & Assert - The ?? throw path should be hit when MessagePack deserializes nil to null
		var ex = Should.Throw<SerializationException>(() =>
			serializer.DeserializeObject(nilData, typeof(TestPluggableMessage)));
		ex.Message.ShouldContain("null");
	}

	[Fact]
	public void Serialize_WithLz4Compression_RoundTrips()
	{
		// Arrange - Use LZ4 compression options to exercise different serialization path
		var options = MessagePackSerializerOptions.Standard.WithCompression(MessagePackCompression.Lz4BlockArray);
		var serializer = new MessagePackPluggableSerializer(options);
		var original = new TestPluggableMessage { Value = 999, Text = "Compressed Test" };

		// Act
		var serialized = serializer.Serialize(original);
		var deserialized = serializer.Deserialize<TestPluggableMessage>(serialized);

		// Assert
		_ = deserialized.ShouldNotBeNull();
		deserialized.Value.ShouldBe(999);
		deserialized.Text.ShouldBe("Compressed Test");
	}

	[Fact]
	public void SerializeObject_WithLz4Compression_RoundTrips()
	{
		// Arrange - Use LZ4 compression options for the object-based API
		var options = MessagePackSerializerOptions.Standard.WithCompression(MessagePackCompression.Lz4BlockArray);
		var serializer = new MessagePackPluggableSerializer(options);
		var original = new TestPluggableMessage { Value = 777, Text = "Compressed Object" };

		// Act
		var serialized = serializer.SerializeObject(original, typeof(TestPluggableMessage));
		var deserialized = (TestPluggableMessage)serializer.DeserializeObject(serialized, typeof(TestPluggableMessage));

		// Assert
		_ = deserialized.ShouldNotBeNull();
		deserialized.Value.ShouldBe(777);
		deserialized.Text.ShouldBe("Compressed Object");
	}

	/// <summary>
	/// A type that cannot be serialized by MessagePack with standard options (no MessagePackObject attribute).
	/// </summary>
	private sealed class UnserializableType
	{
		public string Data { get; set; } = string.Empty;
	}
}
