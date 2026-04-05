// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions.Serialization;

using Excalibur.Dispatch.Serialization.MessagePack;
using MessagePack;

namespace Excalibur.Dispatch.Serialization.Tests.MessagePack;

/// <summary>
/// Unit tests for <see cref="MessagePackSerializer" />.
/// </summary>
[Trait(TraitNames.Component, TestComponents.Serialization)]
[Trait(TraitNames.Category, TestCategories.Unit)]
public sealed class MessagePackMessageSerializerShould : UnitTestBase
{
	private readonly Serialization.MessagePack.MessagePackSerializer _sut;

	public MessagePackMessageSerializerShould()
	{
		_sut = new Serialization.MessagePack.MessagePackSerializer();
	}

	#region Property Tests

	[Fact]
	public void Name_ReturnsMessagePack()
	{
		// Act & Assert
		_sut.Name.ShouldBe("MessagePack");
	}

	[Fact]
	public void Version_ReturnsNonEmptyString()
	{
		// Act & Assert
		_sut.Version.ShouldNotBeNullOrEmpty();
	}

	[Fact]
	public void Version_ReturnsValidVersionFormat()
	{
		// Act
		var version = _sut.Version;

		// Assert - Either valid version or "Unknown"
		if (version != "Unknown")
		{
			System.Version.TryParse(version, out var parsedVersion).ShouldBeTrue();
			_ = parsedVersion.ShouldNotBeNull();
		}
	}

	#endregion Property Tests

	#region Serialize Tests

	[Fact]
	public void Serialize_WithValidMessage_ReturnsBytes()
	{
		// Arrange
		var message = new TestMessage { Id = 42, Name = "Test" };

		// Act
		var result = _sut.SerializeToBytes(message);

		// Assert
		_ = result.ShouldNotBeNull();
		result.Length.ShouldBeGreaterThan(0);
	}

	[Fact]
	public void Serialize_WithNullMessage_ProducesValidOutput()
	{
		// Act - New consolidated serializer follows STJ pattern: null is serializable
		var result = _sut.SerializeToBytes<TestMessage>(null!);

		// Assert - MessagePack serializes null as nil (0xC0)
		_ = result.ShouldNotBeNull();
	}

	[Fact]
	public void Serialize_WithEmptyName_Succeeds()
	{
		// Arrange
		var message = new TestMessage { Id = 1, Name = string.Empty };

		// Act
		var result = _sut.SerializeToBytes(message);

		// Assert
		_ = result.ShouldNotBeNull();
		result.Length.ShouldBeGreaterThan(0);
	}

	[Fact]
	public void Serialize_WithLargeString_Succeeds()
	{
		// Arrange
		var largeString = new string('X', 10000);
		var message = new TestMessage { Id = 99, Name = largeString };

		// Act
		var result = _sut.SerializeToBytes(message);

		// Assert
		_ = result.ShouldNotBeNull();
		result.Length.ShouldBeGreaterThan(0);
	}

	[Fact]
	public void Serialize_WithZeroId_Succeeds()
	{
		// Arrange
		var message = new TestMessage { Id = 0, Name = "Zero" };

		// Act
		var result = _sut.SerializeToBytes(message);

		// Assert
		_ = result.ShouldNotBeNull();
	}

	[Fact]
	public void Serialize_WithNegativeId_Succeeds()
	{
		// Arrange
		var message = new TestMessage { Id = -999, Name = "Negative" };

		// Act
		var result = _sut.SerializeToBytes(message);

		// Assert
		_ = result.ShouldNotBeNull();
	}

	#endregion Serialize Tests

	#region Deserialize Tests

	[Fact]
	public void Deserialize_WithValidData_ReturnsObject()
	{
		// Arrange
		var original = new TestMessage { Id = 123, Name = "Deserialize Test" };
		var bytes = _sut.SerializeToBytes(original);

		// Act
		var result = _sut.Deserialize<TestMessage>(bytes);

		// Assert
		_ = result.ShouldNotBeNull();
		result.Id.ShouldBe(123);
		result.Name.ShouldBe("Deserialize Test");
	}

	[Fact]
	public void Deserialize_WithNullData_ThrowsArgumentNullException()
	{
		// Act & Assert - Must call extension method explicitly; instance method resolves
		// via implicit byte[]->ReadOnlySpan conversion, bypassing the null guard.
		_ = Should.Throw<ArgumentNullException>(() =>
			SerializerExtensions.Deserialize<TestMessage>(_sut, (byte[])null!));
	}

	[Fact]
	public void Deserialize_WithUnicodeString_PreservesCharacters()
	{
		// Arrange
		var original = new TestMessage { Id = 1, Name = "Unicode: \u00e9\u00e0\u00fc\u4e2d\u6587" };
		var bytes = _sut.SerializeToBytes(original);

		// Act
		var result = _sut.Deserialize<TestMessage>(bytes);

		// Assert
		result.Name.ShouldBe(original.Name);
	}

	[Fact]
	public void Deserialize_WithSpecialCharacters_PreservesCharacters()
	{
		// Arrange
		var original = new TestMessage { Id = 2, Name = "Special: \t\n\r\"'\\/" };
		var bytes = _sut.SerializeToBytes(original);

		// Act
		var result = _sut.Deserialize<TestMessage>(bytes);

		// Assert
		result.Name.ShouldBe(original.Name);
	}

	[Fact]
	public void Deserialize_WhenResultIsNull_ThrowsSerializationException()
	{
		// Arrange - MessagePack nil value is a single byte: 0xC0.
		// When deserialized as a reference type (TestMessage), MessagePack returns null,
		// which triggers the NullResult path wrapped in SerializationException.
		var nilData = new byte[] { 0xC0 };

		// Act & Assert
		_ = Should.Throw<SerializationException>(() =>
			_sut.Deserialize<TestMessage>(nilData));
	}

	#endregion Deserialize Tests

	#region RoundTrip Tests

	[Fact]
	public void Serialize_AndDeserialize_RoundTrips()
	{
		// Arrange
		var original = new TestMessage { Id = 456, Name = "RoundTrip Test" };

		// Act
		var serialized = _sut.SerializeToBytes(original);
		var deserialized = _sut.Deserialize<TestMessage>(serialized);

		// Assert
		_ = deserialized.ShouldNotBeNull();
		deserialized.Id.ShouldBe(original.Id);
		deserialized.Name.ShouldBe(original.Name);
	}

	[Fact]
	public void Serialize_AndDeserialize_PreservesZeroId()
	{
		// Arrange
		var original = new TestMessage { Id = 0, Name = "Zero" };

		// Act
		var serialized = _sut.SerializeToBytes(original);
		var deserialized = _sut.Deserialize<TestMessage>(serialized);

		// Assert
		deserialized.Id.ShouldBe(0);
	}

	[Fact]
	public void Serialize_AndDeserialize_PreservesNegativeId()
	{
		// Arrange
		var original = new TestMessage { Id = -999, Name = "Negative" };

		// Act
		var serialized = _sut.SerializeToBytes(original);
		var deserialized = _sut.Deserialize<TestMessage>(serialized);

		// Assert
		deserialized.Id.ShouldBe(-999);
	}

	[Fact]
	public void Serialize_AndDeserialize_PreservesMaxInt()
	{
		// Arrange
		var original = new TestMessage { Id = int.MaxValue, Name = "Max" };

		// Act
		var serialized = _sut.SerializeToBytes(original);
		var deserialized = _sut.Deserialize<TestMessage>(serialized);

		// Assert
		deserialized.Id.ShouldBe(int.MaxValue);
	}

	[Fact]
	public void Serialize_AndDeserialize_PreservesMinInt()
	{
		// Arrange
		var original = new TestMessage { Id = int.MinValue, Name = "Min" };

		// Act
		var serialized = _sut.SerializeToBytes(original);
		var deserialized = _sut.Deserialize<TestMessage>(serialized);

		// Assert
		deserialized.Id.ShouldBe(int.MinValue);
	}

	#endregion RoundTrip Tests

	#region Interface Implementation Tests

	[Fact]
	public void ImplementsISerializer()
	{
		// Assert
		_ = _sut.ShouldBeAssignableTo<ISerializer>();
	}

	#endregion Interface Implementation Tests

	#region Compression Options Tests

	[Fact]
	public void Serialize_WithCompression_Succeeds()
	{
		// Arrange
		var options = MessagePackSerializerOptions.Standard
			.WithCompression(MessagePackCompression.Lz4BlockArray);
		var serializer = new Serialization.MessagePack.MessagePackSerializer(options);
		var message = new TestMessage { Id = 42, Name = "Compression Test" };

		// Act
		var result = serializer.SerializeToBytes(message);

		// Assert
		_ = result.ShouldNotBeNull();
	}

	[Fact]
	public void Serialize_AndDeserialize_WithCompression_RoundTrips()
	{
		// Arrange
		var options = MessagePackSerializerOptions.Standard
			.WithCompression(MessagePackCompression.Lz4BlockArray);
		var serializer = new Serialization.MessagePack.MessagePackSerializer(options);
		var original = new TestMessage { Id = 789, Name = "Compression RoundTrip" };

		// Act
		var serialized = serializer.SerializeToBytes(original);
		var deserialized = serializer.Deserialize<TestMessage>(serialized);

		// Assert
		deserialized.Id.ShouldBe(original.Id);
		deserialized.Name.ShouldBe(original.Name);
	}

	[Fact]
	public void Deserialize_WithCompression_WhenResultIsNull_ThrowsSerializationException()
	{
		// Arrange - Test the null-result branch with compression enabled.
		// MessagePack nil (0xC0) is not compressed, but a serializer configured with
		// LZ4 compression can still handle uncompressed nil.
		var options = MessagePackSerializerOptions.Standard
			.WithCompression(MessagePackCompression.Lz4BlockArray);
		var serializer = new Serialization.MessagePack.MessagePackSerializer(options);
		var nilData = new byte[] { 0xC0 };

		// Act & Assert
		_ = Should.Throw<SerializationException>(() =>
			serializer.Deserialize<TestMessage>(nilData));
	}

	#endregion Compression Options Tests
}
