// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions.Serialization;
using Excalibur.Dispatch.Serialization.MessagePack;

using MessagePack;

namespace Excalibur.Dispatch.Serialization.Tests.MessagePack;

/// <summary>
/// Unit tests for <see cref="AotMessagePackSerializer" />.
/// </summary>
/// <remarks>
/// Note: The AotMessagePackSerializer uses StaticCompositeResolver which requires types
/// to be registered at compile time. Tests that require serialization use the Standard
/// resolver via custom options to work with test types.
/// </remarks>
[Trait("Component", "Serialization")]
[Trait("Category", "Unit")]
public sealed class AotMessagePackSerializerShould : UnitTestBase
{
	/// <summary>
	/// Gets standard options that work with test types (not AOT-restricted).
	/// </summary>
	private static MessagePackSerializerOptions TestOptions =>
		MessagePackSerializerOptions.Standard.WithCompression(MessagePackCompression.Lz4BlockArray);

	#region Constructor Tests

	[Fact]
	public void Constructor_WithDefaultOptions_CreatesSerializer()
	{
		// Arrange & Act
		var serializer = new AotMessagePackSerializer();

		// Assert
		_ = serializer.ShouldNotBeNull();
		serializer.SerializerName.ShouldBe("MessagePack-AOT");
		serializer.SerializerVersion.ShouldBe("1.0.0");
	}

	[Fact]
	public void Constructor_WithNullOptions_UsesDefaults()
	{
		// Arrange & Act
		var serializer = new AotMessagePackSerializer(null);

		// Assert
		_ = serializer.ShouldNotBeNull();
		serializer.SerializerName.ShouldBe("MessagePack-AOT");
	}

	[Fact]
	public void Constructor_WithCustomOptions_CreatesSerializer()
	{
		// Arrange
		var options = MessagePackSerializerOptions.Standard.WithCompression(MessagePackCompression.Lz4Block);

		// Act
		var serializer = new AotMessagePackSerializer(options);

		// Assert
		_ = serializer.ShouldNotBeNull();
	}

	#endregion Constructor Tests

	#region Property Tests

	[Fact]
	public void SerializerName_ReturnsMessagePackAOT()
	{
		// Arrange
		var serializer = new AotMessagePackSerializer();

		// Act & Assert
		serializer.SerializerName.ShouldBe("MessagePack-AOT");
	}

	[Fact]
	public void SerializerVersion_ReturnsExpectedVersion()
	{
		// Arrange
		var serializer = new AotMessagePackSerializer();

		// Act & Assert
		serializer.SerializerVersion.ShouldBe("1.0.0");
	}

	#endregion Property Tests

	#region Serialize Tests (with Standard Resolver for test types)

	[Fact]
	public void Serialize_WithValidMessage_ReturnsBytes()
	{
		// Arrange - Use standard options for test types
		var serializer = new AotMessagePackSerializer(TestOptions);
		var message = new TestMessage { Id = 42, Name = "Test" };

		// Act
		var result = serializer.Serialize(message);

		// Assert
		_ = result.ShouldNotBeNull();
		result.Length.ShouldBeGreaterThan(0);
	}

	[Fact]
	public void Serialize_WithNullMessage_ThrowsArgumentNullException()
	{
		// Arrange
		var serializer = new AotMessagePackSerializer(TestOptions);

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			serializer.Serialize<TestMessage>(null!));
	}

	[Fact]
	public void Serialize_WithEmptyName_Succeeds()
	{
		// Arrange
		var serializer = new AotMessagePackSerializer(TestOptions);
		var message = new TestMessage { Id = 1, Name = string.Empty };

		// Act
		var result = serializer.Serialize(message);

		// Assert
		_ = result.ShouldNotBeNull();
		result.Length.ShouldBeGreaterThan(0);
	}

	[Fact]
	public void Serialize_WithLargeString_Succeeds()
	{
		// Arrange
		var serializer = new AotMessagePackSerializer(TestOptions);
		var largeString = new string('X', 10000);
		var message = new TestMessage { Id = 99, Name = largeString };

		// Act
		var result = serializer.Serialize(message);

		// Assert
		_ = result.ShouldNotBeNull();
		// With compression, result may be smaller than string
		result.Length.ShouldBeGreaterThan(0);
	}

	#endregion Serialize Tests

	#region Deserialize Tests

	[Fact]
	public void Deserialize_WithValidData_ReturnsObject()
	{
		// Arrange
		var serializer = new AotMessagePackSerializer(TestOptions);
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
	public void Deserialize_WithNullData_ThrowsArgumentNullException()
	{
		// Arrange
		var serializer = new AotMessagePackSerializer(TestOptions);

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			serializer.Deserialize<TestMessage>(null!));
	}

	#endregion Deserialize Tests

	#region RoundTrip Tests

	[Fact]
	public void Serialize_AndDeserialize_RoundTrips()
	{
		// Arrange
		var serializer = new AotMessagePackSerializer(TestOptions);
		var original = new TestMessage { Id = 456, Name = "RoundTrip Test" };

		// Act
		var serialized = serializer.Serialize(original);
		var deserialized = serializer.Deserialize<TestMessage>(serialized);

		// Assert
		_ = deserialized.ShouldNotBeNull();
		deserialized.Id.ShouldBe(original.Id);
		deserialized.Name.ShouldBe(original.Name);
	}

	[Fact]
	public void Serialize_AndDeserialize_PreservesZeroId()
	{
		// Arrange
		var serializer = new AotMessagePackSerializer(TestOptions);
		var original = new TestMessage { Id = 0, Name = "Zero" };

		// Act
		var serialized = serializer.Serialize(original);
		var deserialized = serializer.Deserialize<TestMessage>(serialized);

		// Assert
		deserialized.Id.ShouldBe(0);
	}

	[Fact]
	public void Serialize_AndDeserialize_PreservesNegativeId()
	{
		// Arrange
		var serializer = new AotMessagePackSerializer(TestOptions);
		var original = new TestMessage { Id = -999, Name = "Negative" };

		// Act
		var serialized = serializer.Serialize(original);
		var deserialized = serializer.Deserialize<TestMessage>(serialized);

		// Assert
		deserialized.Id.ShouldBe(-999);
	}

	[Fact]
	public void Serialize_AndDeserialize_PreservesMaxInt()
	{
		// Arrange
		var serializer = new AotMessagePackSerializer(TestOptions);
		var original = new TestMessage { Id = int.MaxValue, Name = "Max" };

		// Act
		var serialized = serializer.Serialize(original);
		var deserialized = serializer.Deserialize<TestMessage>(serialized);

		// Assert
		deserialized.Id.ShouldBe(int.MaxValue);
	}

	[Fact]
	public void Serialize_AndDeserialize_PreservesMinInt()
	{
		// Arrange
		var serializer = new AotMessagePackSerializer(TestOptions);
		var original = new TestMessage { Id = int.MinValue, Name = "Min" };

		// Act
		var serialized = serializer.Serialize(original);
		var deserialized = serializer.Deserialize<TestMessage>(serialized);

		// Assert
		deserialized.Id.ShouldBe(int.MinValue);
	}

	[Fact]
	public void Serialize_AndDeserialize_PreservesUnicodeCharacters()
	{
		// Arrange
		var serializer = new AotMessagePackSerializer(TestOptions);
		var original = new TestMessage { Id = 1, Name = "Unicode: \u00e9\u00e0\u00fc\u4e2d\u6587" };

		// Act
		var serialized = serializer.Serialize(original);
		var deserialized = serializer.Deserialize<TestMessage>(serialized);

		// Assert
		deserialized.Name.ShouldBe(original.Name);
	}

	[Fact]
	public void Serialize_AndDeserialize_PreservesSpecialCharacters()
	{
		// Arrange
		var serializer = new AotMessagePackSerializer(TestOptions);
		var original = new TestMessage { Id = 2, Name = "Special: \t\n\r\"'\\/" };

		// Act
		var serialized = serializer.Serialize(original);
		var deserialized = serializer.Deserialize<TestMessage>(serialized);

		// Assert
		deserialized.Name.ShouldBe(original.Name);
	}

	#endregion RoundTrip Tests

	#region Interface Implementation Tests

	[Fact]
	public void ImplementsIMessageSerializer()
	{
		// Arrange
		var serializer = new AotMessagePackSerializer();

		// Assert
		_ = serializer.ShouldBeAssignableTo<IMessageSerializer>();
	}

	#endregion Interface Implementation Tests
}
