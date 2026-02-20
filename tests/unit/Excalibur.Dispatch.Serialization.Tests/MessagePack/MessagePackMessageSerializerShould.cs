// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions.Serialization;

using Microsoft.Extensions.Options;

using Excalibur.Dispatch.Serialization.MessagePack;
using MessagePack;

namespace Excalibur.Dispatch.Serialization.Tests.MessagePack;

/// <summary>
/// Unit tests for <see cref="MessagePackMessageSerializer" />.
/// </summary>
[Trait("Component", "Serialization")]
[Trait("Category", "Unit")]
public sealed class MessagePackMessageSerializerShould : UnitTestBase
{
	private readonly MessagePackMessageSerializer _sut;

	public MessagePackMessageSerializerShould()
	{
		var options = Microsoft.Extensions.Options.Options.Create(new MessagePackSerializationOptions());
		_sut = new MessagePackMessageSerializer(options);
	}

	#region Constructor Tests

	[Fact]
	public void Constructor_WithValidOptions_CreatesSerializer()
	{
		// Arrange
		var options = Microsoft.Extensions.Options.Options.Create(new MessagePackSerializationOptions());

		// Act
		var serializer = new MessagePackMessageSerializer(options);

		// Assert
		_ = serializer.ShouldNotBeNull();
	}

	[Fact]
	public void Constructor_WithNullOptions_ThrowsArgumentNullException()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			new MessagePackMessageSerializer(null!));
	}

	#endregion Constructor Tests

	#region Property Tests

	[Fact]
	public void SerializerName_ReturnsMessagePack()
	{
		// Act & Assert
		_sut.SerializerName.ShouldBe("MessagePack");
	}

	[Fact]
	public void SerializerVersion_ReturnsExpectedVersion()
	{
		// Act & Assert
		_sut.SerializerVersion.ShouldBe("1.0.0");
	}

	#endregion Property Tests

	#region Serialize Tests

	[Fact]
	public void Serialize_WithValidMessage_ReturnsBytes()
	{
		// Arrange
		var message = new TestMessage { Id = 42, Name = "Test" };

		// Act
		var result = _sut.Serialize(message);

		// Assert
		_ = result.ShouldNotBeNull();
		result.Length.ShouldBeGreaterThan(0);
	}

	[Fact]
	public void Serialize_WithNullMessage_ThrowsArgumentNullException()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			_sut.Serialize<TestMessage>(null!));
	}

	[Fact]
	public void Serialize_WithEmptyName_Succeeds()
	{
		// Arrange
		var message = new TestMessage { Id = 1, Name = string.Empty };

		// Act
		var result = _sut.Serialize(message);

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
		var result = _sut.Serialize(message);

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
		var result = _sut.Serialize(message);

		// Assert
		_ = result.ShouldNotBeNull();
	}

	[Fact]
	public void Serialize_WithNegativeId_Succeeds()
	{
		// Arrange
		var message = new TestMessage { Id = -999, Name = "Negative" };

		// Act
		var result = _sut.Serialize(message);

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
		var bytes = _sut.Serialize(original);

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
		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			_sut.Deserialize<TestMessage>(null!));
	}

	[Fact]
	public void Deserialize_WithUnicodeString_PreservesCharacters()
	{
		// Arrange
		var original = new TestMessage { Id = 1, Name = "Unicode: \u00e9\u00e0\u00fc\u4e2d\u6587" };
		var bytes = _sut.Serialize(original);

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
		var bytes = _sut.Serialize(original);

		// Act
		var result = _sut.Deserialize<TestMessage>(bytes);

		// Assert
		result.Name.ShouldBe(original.Name);
	}

	[Fact]
	public void Deserialize_WhenResultIsNull_ThrowsInvalidOperationException()
	{
		// Arrange - MessagePack nil value is a single byte: 0xC0.
		// When deserialized as a reference type (TestMessage), MessagePack returns null,
		// which triggers the ?? throw path on line 55-56 of MessagePackMessageSerializer.
		var nilData = new byte[] { 0xC0 };

		// Act & Assert
		_ = Should.Throw<InvalidOperationException>(() =>
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
		var serialized = _sut.Serialize(original);
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
		var serialized = _sut.Serialize(original);
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
		var serialized = _sut.Serialize(original);
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
		var serialized = _sut.Serialize(original);
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
		var serialized = _sut.Serialize(original);
		var deserialized = _sut.Deserialize<TestMessage>(serialized);

		// Assert
		deserialized.Id.ShouldBe(int.MinValue);
	}

	#endregion RoundTrip Tests

	#region Interface Implementation Tests

	[Fact]
	public void ImplementsIMessageSerializer()
	{
		// Assert
		_ = _sut.ShouldBeAssignableTo<IMessageSerializer>();
	}

	#endregion Interface Implementation Tests

	#region Compression Options Tests

	[Fact]
	public void Serialize_WithCompression_Succeeds()
	{
		// Arrange
		var options = Microsoft.Extensions.Options.Options.Create(new MessagePackSerializationOptions
		{
			UseLz4Compression = true,
		});
		var serializer = new MessagePackMessageSerializer(options);
		var message = new TestMessage { Id = 42, Name = "Compression Test" };

		// Act
		var result = serializer.Serialize(message);

		// Assert
		_ = result.ShouldNotBeNull();
	}

	[Fact]
	public void Serialize_AndDeserialize_WithCompression_RoundTrips()
	{
		// Arrange
		var options = Microsoft.Extensions.Options.Options.Create(new MessagePackSerializationOptions
		{
			UseLz4Compression = true,
		});
		var serializer = new MessagePackMessageSerializer(options);
		var original = new TestMessage { Id = 789, Name = "Compression RoundTrip" };

		// Act
		var serialized = serializer.Serialize(original);
		var deserialized = serializer.Deserialize<TestMessage>(serialized);

		// Assert
		deserialized.Id.ShouldBe(original.Id);
		deserialized.Name.ShouldBe(original.Name);
	}

	[Fact]
	public void Deserialize_WithCompression_WhenResultIsNull_ThrowsInvalidOperationException()
	{
		// Arrange - Test the null-result branch with compression enabled.
		// MessagePack nil (0xC0) is not compressed, but a serializer configured with
		// LZ4 compression can still handle uncompressed nil.
		var options = Microsoft.Extensions.Options.Options.Create(new MessagePackSerializationOptions
		{
			UseLz4Compression = true,
		});
		var serializer = new MessagePackMessageSerializer(options);
		var nilData = new byte[] { 0xC0 };

		// Act & Assert
		_ = Should.Throw<InvalidOperationException>(() =>
			serializer.Deserialize<TestMessage>(nilData));
	}

	#endregion Compression Options Tests
}
