// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.CodeAnalysis;

using Excalibur.Dispatch.Abstractions.Serialization;
using Excalibur.Dispatch.Serialization.MessagePack;

using MessagePack;

namespace Excalibur.Dispatch.Serialization.Tests.MessagePack;

/// <summary>
/// Tests for error handling in MessagePack serializers.
/// Verifies proper exception types and messages.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Serialization")]
public sealed class MessagePackErrorHandlingShould : UnitTestBase
{
	#region DispatchMessagePackSerializer Error Handling

	[Fact]
	public void DispatchSerializer_Deserialize_WithCorruptData_Throws()
	{
		// Arrange
		var serializer = new DispatchMessagePackSerializer();
		var corrupt = new byte[] { 0xFF, 0xFE, 0xFD, 0xFC };

		// Act & Assert
		Should.Throw<MessagePackSerializationException>(() =>
			serializer.Deserialize<TestMessage>(corrupt));
	}

	[Fact]
	public void DispatchSerializer_Deserialize_WithEmptyArray_Throws()
	{
		// Arrange
		var serializer = new DispatchMessagePackSerializer();
		var empty = Array.Empty<byte>();

		// Act & Assert
		Should.Throw<MessagePackSerializationException>(() =>
			serializer.Deserialize<TestMessage>(empty));
	}

	[Fact]
	public void DispatchSerializer_Deserialize_WithTruncatedData_Throws()
	{
		// Arrange
		var serializer = new DispatchMessagePackSerializer();
		var original = new TestMessage { Id = 42, Name = "Test" };
		var bytes = serializer.Serialize(original);

		// Truncate the data
		var truncated = bytes.Take(bytes.Length / 2).ToArray();

		// Act & Assert
		Should.Throw<MessagePackSerializationException>(() =>
			serializer.Deserialize<TestMessage>(truncated));
	}

	#endregion

	#region AotMessagePackSerializer Error Handling

	[Fact]
	public void AotSerializer_Serialize_WithNull_ThrowsArgumentNull()
	{
		// Arrange
		var serializer = new AotMessagePackSerializer(MessagePackSerializerOptions.Standard);

		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			serializer.Serialize<TestMessage>(null!));
	}

	[Fact]
	public void AotSerializer_Deserialize_WithNull_ThrowsArgumentNull()
	{
		// Arrange
		var serializer = new AotMessagePackSerializer(MessagePackSerializerOptions.Standard);

		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			serializer.Deserialize<TestMessage>(null!));
	}

	[Fact]
	public void AotSerializer_Deserialize_WithCorruptData_Throws()
	{
		// Arrange
		var serializer = new AotMessagePackSerializer(MessagePackSerializerOptions.Standard);
		var corrupt = new byte[] { 0xFF, 0xFE };

		// Act & Assert
		Should.Throw<MessagePackSerializationException>(() =>
			serializer.Deserialize<TestMessage>(corrupt));
	}

	#endregion

	#region MessagePackMessageSerializer Error Handling

	[Fact]
	public void MessagePackMessageSerializer_Constructor_WithNullOptions_ThrowsArgumentNull()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			new MessagePackMessageSerializer(null!));
	}

	[Fact]
	public void MessagePackMessageSerializer_Serialize_WithNull_ThrowsArgumentNull()
	{
		// Arrange
		var options = Microsoft.Extensions.Options.Options.Create(new MessagePackSerializationOptions());
		var serializer = new MessagePackMessageSerializer(options);

		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			serializer.Serialize<TestMessage>(null!));
	}

	[Fact]
	public void MessagePackMessageSerializer_Deserialize_WithNull_ThrowsArgumentNull()
	{
		// Arrange
		var options = Microsoft.Extensions.Options.Options.Create(new MessagePackSerializationOptions());
		var serializer = new MessagePackMessageSerializer(options);

		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			serializer.Deserialize<TestMessage>(null!));
	}

	[Fact]
	public void MessagePackMessageSerializer_Deserialize_WithNilData_ThrowsInvalidOperation()
	{
		// Arrange
		var options = Microsoft.Extensions.Options.Options.Create(new MessagePackSerializationOptions());
		var serializer = new MessagePackMessageSerializer(options);
		var nilData = new byte[] { 0xC0 }; // MessagePack nil

		// Act & Assert
		var ex = Should.Throw<InvalidOperationException>(() =>
			serializer.Deserialize<TestMessage>(nilData));
		ex.Message.ShouldNotBeNullOrWhiteSpace();
	}

	#endregion

	#region MessagePackPluggableSerializer Error Handling

	[Fact]
	public void PluggableSerializer_Serialize_WithNull_ThrowsArgumentNull()
	{
		// Arrange
		var serializer = new MessagePackPluggableSerializer();

		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			serializer.Serialize<TestPluggableMessage>(null!));
	}

	[Fact]
	public void PluggableSerializer_SerializeObject_WithNullValue_ThrowsArgumentNull()
	{
		// Arrange
		var serializer = new MessagePackPluggableSerializer();

		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			serializer.SerializeObject(null!, typeof(TestPluggableMessage)));
	}

	[Fact]
	public void PluggableSerializer_SerializeObject_WithNullType_ThrowsArgumentNull()
	{
		// Arrange
		var serializer = new MessagePackPluggableSerializer();
		var value = new TestPluggableMessage();

		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			serializer.SerializeObject(value, null!));
	}

	[Fact]
	public void PluggableSerializer_DeserializeObject_WithNullType_ThrowsArgumentNull()
	{
		// Arrange
		var serializer = new MessagePackPluggableSerializer();
		var data = new byte[] { 0x01 };

		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			serializer.DeserializeObject(data, null!));
	}

	[Fact]
	[RequiresUnreferencedCode("Test")]
	[RequiresDynamicCode("Test")]
	public void PluggableSerializer_Deserialize_WithCorruptData_ThrowsSerializationException()
	{
		// Arrange
		var serializer = new MessagePackPluggableSerializer();
		var corrupt = new byte[] { 0xFF, 0xAA, 0xBB };

		// Act & Assert
		var ex = Should.Throw<SerializationException>(() =>
			serializer.Deserialize<TestPluggableMessage>(corrupt));
		ex.InnerException.ShouldNotBeNull();
	}

	[Fact]
	[RequiresUnreferencedCode("Test")]
	[RequiresDynamicCode("Test")]
	public void PluggableSerializer_Deserialize_WithNilData_ThrowsSerializationException()
	{
		// Arrange
		var serializer = new MessagePackPluggableSerializer();
		var nilData = new byte[] { 0xC0 };

		// Act & Assert
		var ex = Should.Throw<SerializationException>(() =>
			serializer.Deserialize<TestPluggableMessage>(nilData));
		ex.Message.ShouldContain("null");
	}

	[Fact]
	[RequiresUnreferencedCode("Test")]
	[RequiresDynamicCode("Test")]
	public void PluggableSerializer_DeserializeObject_WithNilData_ThrowsSerializationException()
	{
		// Arrange
		var serializer = new MessagePackPluggableSerializer();
		var nilData = new byte[] { 0xC0 };

		// Act & Assert
		var ex = Should.Throw<SerializationException>(() =>
			serializer.DeserializeObject(nilData, typeof(TestPluggableMessage)));
		ex.Message.ShouldContain("null");
	}

	[Fact]
	[RequiresUnreferencedCode("Test")]
	[RequiresDynamicCode("Test")]
	public void PluggableSerializer_DeserializeObject_WithCorruptData_ThrowsSerializationException()
	{
		// Arrange
		var serializer = new MessagePackPluggableSerializer();
		var corrupt = new byte[] { 0xFE, 0xFD, 0xFC };

		// Act & Assert
		var ex = Should.Throw<SerializationException>(() =>
			serializer.DeserializeObject(corrupt, typeof(TestPluggableMessage)));
		ex.InnerException.ShouldNotBeNull();
	}

	[Fact]
	[RequiresUnreferencedCode("Test")]
	[RequiresDynamicCode("Test")]
	public void PluggableSerializer_Serialize_WithUnserializableType_ThrowsSerializationException()
	{
		// Arrange
		var serializer = new MessagePackPluggableSerializer();
		var badObj = new NonSerializableForTests { Value = "bad" };

		// Act & Assert
		var ex = Should.Throw<SerializationException>(() =>
			serializer.Serialize(badObj));
		ex.InnerException.ShouldNotBeNull();
	}

	[Fact]
	[RequiresUnreferencedCode("Test")]
	[RequiresDynamicCode("Test")]
	public void PluggableSerializer_SerializeObject_WithUnserializableType_ThrowsSerializationException()
	{
		// Arrange
		var serializer = new MessagePackPluggableSerializer();
		var badObj = new NonSerializableForTests { Value = "bad" };

		// Act & Assert
		var ex = Should.Throw<SerializationException>(() =>
			serializer.SerializeObject(badObj, typeof(NonSerializableForTests)));
		ex.InnerException.ShouldNotBeNull();
	}

	#endregion

	#region Extension Methods Error Handling

	[Fact]
	public void AddMessagePackSerialization_WithNullServices_ThrowsArgumentNull()
	{
		// Arrange
		IServiceCollection services = null!;

		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			services.AddMessagePackSerialization());
	}

	[Fact]
	public void AddMessagePackSerialization_Generic_WithNullServices_ThrowsArgumentNull()
	{
		// Arrange
		IServiceCollection services = null!;

		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			services.AddMessagePackSerialization<DispatchMessagePackSerializer>());
	}

	[Fact]
	public void GetPluggableSerializer_WithNullOptions_ThrowsArgumentNull()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			MessagePackSerializationExtensions.GetPluggableSerializer(null!));
	}

	[Fact]
	public void AddMessagePackPluggableSerialization_WithNullServices_ThrowsArgumentNull()
	{
		// Arrange
		IServiceCollection services = null!;

		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			services.AddMessagePackPluggableSerialization());
	}

	[Fact]
	public void AddMessagePackPluggableSerialization_WithOptionsNull_ThrowsArgumentNull()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			services.AddMessagePackPluggableSerialization(null!));
	}

	#endregion

	/// <summary>
	/// Non-serializable type for error testing.
	/// </summary>
	private sealed class NonSerializableForTests
	{
		public string Value { get; set; } = string.Empty;
	}
}
