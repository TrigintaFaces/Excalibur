// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Serialization.MessagePack;
using MessagePack;

namespace Excalibur.Dispatch.Serialization.Tests.MessagePack;

/// <summary>
/// Unit tests for <see cref="DispatchMessagePackSerializer" />.
/// </summary>
[Trait("Category", "Unit")]
public sealed class DispatchMessagePackSerializerShould : UnitTestBase
{
	[Fact]
	public void Constructor_WithDefaultOptions_CreatesSerializer()
	{
		// Arrange & Act
		var serializer = new DispatchMessagePackSerializer();

		// Assert
		_ = serializer.ShouldNotBeNull();
		serializer.SerializerName.ShouldBe("MessagePack");
		serializer.SerializerVersion.ShouldBe("1.0.0");
	}

	[Fact]
	public void Constructor_WithCustomOptions_CreatesSerializer()
	{
		// Arrange
		var options = MessagePackSerializerOptions.Standard.WithCompression(MessagePackCompression.Lz4Block);

		// Act
		var serializer = new DispatchMessagePackSerializer(options);

		// Assert
		_ = serializer.ShouldNotBeNull();
	}

	[Fact]
	public void SerializerName_ReturnsMessagePack()
	{
		// Arrange
		var serializer = new DispatchMessagePackSerializer();

		// Act & Assert
		serializer.SerializerName.ShouldBe("MessagePack");
	}

	[Fact]
	public void SerializerVersion_ReturnsExpectedVersion()
	{
		// Arrange
		var serializer = new DispatchMessagePackSerializer();

		// Act & Assert
		serializer.SerializerVersion.ShouldBe("1.0.0");
	}

	[Fact]
	public void Serialize_AndDeserialize_RoundTrips()
	{
		// Arrange
		var serializer = new DispatchMessagePackSerializer();
		var original = new TestMessage { Id = 42, Name = "Test" };

		// Act
		var serialized = serializer.Serialize(original);
		var deserialized = serializer.Deserialize<TestMessage>(serialized);

		// Assert
		_ = deserialized.ShouldNotBeNull();
		deserialized.Id.ShouldBe(original.Id);
		deserialized.Name.ShouldBe(original.Name);
	}

	[Fact]
	public void Serialize_ProducesNonEmptyBytes()
	{
		// Arrange
		var serializer = new DispatchMessagePackSerializer();
		var message = new TestMessage { Id = 1, Name = "Test" };

		// Act
		var serialized = serializer.Serialize(message);

		// Assert
		_ = serialized.ShouldNotBeNull();
		serialized.Length.ShouldBeGreaterThan(0);
	}

	[Fact]
	public void Deserialize_WithValidData_ReturnsObject()
	{
		// Arrange
		var serializer = new DispatchMessagePackSerializer();
		var original = new TestMessage { Id = 123, Name = "Hello" };
		var serialized = serializer.Serialize(original);

		// Act
		var result = serializer.Deserialize<TestMessage>(serialized);

		// Assert
		_ = result.ShouldNotBeNull();
		result.Id.ShouldBe(123);
		result.Name.ShouldBe("Hello");
	}

	[Fact]
	public void Serialize_WithLz4Compression_ProducesValidData()
	{
		// Arrange
		var options = MessagePackSerializerOptions.Standard.WithCompression(MessagePackCompression.Lz4BlockArray);
		var serializer = new DispatchMessagePackSerializer(options);
		var message = new TestMessage { Id = 999, Name = "Compressed" };

		// Act
		var serialized = serializer.Serialize(message);
		var deserialized = serializer.Deserialize<TestMessage>(serialized);

		// Assert
		deserialized.Id.ShouldBe(999);
		deserialized.Name.ShouldBe("Compressed");
	}
}
