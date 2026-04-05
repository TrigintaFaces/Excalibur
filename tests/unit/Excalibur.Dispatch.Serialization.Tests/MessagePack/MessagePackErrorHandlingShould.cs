// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.CodeAnalysis;

using Excalibur.Dispatch.Abstractions.Serialization;
using Excalibur.Dispatch.Serialization.MessagePack;

using MessagePack;

using MpkSerializer = Excalibur.Dispatch.Serialization.MessagePack.MessagePackSerializer;

namespace Excalibur.Dispatch.Serialization.Tests.MessagePack;

/// <summary>
/// Tests for error handling in the consolidated <see cref="MpkSerializer"/>.
/// Verifies proper exception types and messages.
/// </summary>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Serialization)]
public sealed class MessagePackErrorHandlingShould : UnitTestBase
{
	#region Deserialize Error Handling (default options)

	[Fact]
	[RequiresUnreferencedCode("Test")]
	[RequiresDynamicCode("Test")]
	public void Serializer_Deserialize_WithCorruptData_ThrowsSerializationException()
	{
		// Arrange
		var serializer = new MpkSerializer();
		var corrupt = new byte[] { 0xFF, 0xFE, 0xFD, 0xFC };

		// Act & Assert
		Should.Throw<SerializationException>(() =>
			serializer.Deserialize<TestMessage>(corrupt.AsSpan()));
	}

	[Fact]
	[RequiresUnreferencedCode("Test")]
	[RequiresDynamicCode("Test")]
	public void Serializer_Deserialize_WithEmptyArray_ThrowsSerializationException()
	{
		// Arrange
		var serializer = new MpkSerializer();
		var empty = Array.Empty<byte>();

		// Act & Assert
		Should.Throw<SerializationException>(() =>
			serializer.Deserialize<TestMessage>(empty.AsSpan()));
	}

	[Fact]
	[RequiresUnreferencedCode("Test")]
	[RequiresDynamicCode("Test")]
	public void Serializer_Deserialize_WithTruncatedData_ThrowsSerializationException()
	{
		// Arrange
		var serializer = new MpkSerializer();
		var original = new TestMessage { Id = 42, Name = "Test" };
		var bytes = serializer.SerializeToBytes(original);

		// Truncate the data
		var truncated = bytes.Take(bytes.Length / 2).ToArray();

		// Act & Assert
		Should.Throw<SerializationException>(() =>
			serializer.Deserialize<TestMessage>(truncated.AsSpan()));
	}

	#endregion

	#region Serialize/Deserialize Null Handling (Standard options)

	[Fact]
	public void Serializer_Serialize_WithNull_ProducesValidOutput()
	{
		// Arrange
		var serializer = new MpkSerializer(MessagePackSerializerOptions.Standard);

		// Act - New consolidated serializer follows STJ pattern: null is serializable
		var result = serializer.SerializeToBytes<TestMessage>(null!);

		// Assert - MessagePack serializes null as nil (0xC0)
		result.ShouldNotBeNull();
	}

	[Fact]
	public void Serializer_Deserialize_WithNull_ThrowsArgumentNull()
	{
		// Arrange
		var serializer = new MpkSerializer(MessagePackSerializerOptions.Standard);

		// Act & Assert - Must call extension method explicitly; instance method resolves
		// via implicit byte[]->ReadOnlySpan conversion, bypassing the null guard.
		Should.Throw<ArgumentNullException>(() =>
			SerializerExtensions.Deserialize<TestMessage>(serializer, (byte[])null!));
	}

	[Fact]
	[RequiresUnreferencedCode("Test")]
	[RequiresDynamicCode("Test")]
	public void Serializer_WithStandardOptions_Deserialize_WithCorruptData_ThrowsSerializationException()
	{
		// Arrange
		var serializer = new MpkSerializer(MessagePackSerializerOptions.Standard);
		var corrupt = new byte[] { 0xFF, 0xFE };

		// Act & Assert
		Should.Throw<SerializationException>(() =>
			serializer.Deserialize<TestMessage>(corrupt.AsSpan()));
	}

	#endregion

	#region Null Constructor Handling

	[Fact]
	public void Serializer_Constructor_WithNull_UsesDefaults()
	{
		// Arrange & Act - consolidated serializer accepts null, defaults to Standard
		var serializer = new MpkSerializer(null);

		// Assert
		serializer.ShouldNotBeNull();
		serializer.Name.ShouldBe("MessagePack");
	}

	[Fact]
	public void Serializer_WithNoCompressionOptions_Serialize_WithNull_ProducesValidOutput()
	{
		// Arrange
		var opts = MessagePackSerializerOptions.Standard.WithCompression(MessagePackCompression.None);
		var serializer = new MpkSerializer(opts);

		// Act - New consolidated serializer follows STJ pattern: null is serializable
		var result = serializer.SerializeToBytes<TestMessage>(null!);

		// Assert
		result.ShouldNotBeNull();
	}

	[Fact]
	public void Serializer_WithNoCompressionOptions_Deserialize_WithNull_ThrowsArgumentNull()
	{
		// Arrange
		var opts = MessagePackSerializerOptions.Standard.WithCompression(MessagePackCompression.None);
		var serializer = new MpkSerializer(opts);

		// Act & Assert - Must call extension method explicitly
		Should.Throw<ArgumentNullException>(() =>
			SerializerExtensions.Deserialize<TestMessage>(serializer, (byte[])null!));
	}

	[Fact]
	[RequiresUnreferencedCode("Test")]
	[RequiresDynamicCode("Test")]
	public void Serializer_Deserialize_WithNilData_ThrowsSerializationException()
	{
		// Arrange
		var serializer = new MpkSerializer();
		var nilData = new byte[] { 0xC0 }; // MessagePack nil

		// Act & Assert
		var ex = Should.Throw<SerializationException>(() =>
			serializer.Deserialize<TestMessage>(nilData.AsSpan()));
		ex.Message.ShouldContain("null");
	}

	#endregion

	#region Pluggable Serializer Error Handling

	[Fact]
	public void PluggableSerializer_Serialize_WithNull_ProducesValidOutput()
	{
		// Arrange
		var serializer = new MpkSerializer();

		// Act - New consolidated serializer follows STJ pattern: null is serializable via ISerializer path
		var result = serializer.SerializeToBytes<TestPluggableMessage>(null!);

		// Assert
		result.ShouldNotBeNull();
	}

	[Fact]
	public void PluggableSerializer_SerializeObject_WithNullValue_ThrowsArgumentNull()
	{
		// Arrange
		var serializer = new MpkSerializer();

		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			serializer.SerializeObject(null!, typeof(TestPluggableMessage)));
	}

	[Fact]
	public void PluggableSerializer_SerializeObject_WithNullType_ThrowsArgumentNull()
	{
		// Arrange
		var serializer = new MpkSerializer();
		var value = new TestPluggableMessage();

		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			serializer.SerializeObject(value, null!));
	}

	[Fact]
	public void PluggableSerializer_DeserializeObject_WithNullType_ThrowsArgumentNull()
	{
		// Arrange
		var serializer = new MpkSerializer();
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
		var serializer = new MpkSerializer();
		var corrupt = new byte[] { 0xFF, 0xAA, 0xBB };

		// Act & Assert
		var ex = Should.Throw<SerializationException>(() =>
			serializer.Deserialize<TestPluggableMessage>(corrupt.AsSpan()));
		ex.InnerException.ShouldNotBeNull();
	}

	[Fact]
	[RequiresUnreferencedCode("Test")]
	[RequiresDynamicCode("Test")]
	public void PluggableSerializer_Deserialize_WithNilData_ThrowsSerializationException()
	{
		// Arrange
		var serializer = new MpkSerializer();
		var nilData = new byte[] { 0xC0 };

		// Act & Assert
		var ex = Should.Throw<SerializationException>(() =>
			serializer.Deserialize<TestPluggableMessage>(nilData.AsSpan()));
		ex.Message.ShouldContain("null");
	}

	[Fact]
	[RequiresUnreferencedCode("Test")]
	[RequiresDynamicCode("Test")]
	public void PluggableSerializer_DeserializeObject_WithNilData_ThrowsSerializationException()
	{
		// Arrange
		var serializer = new MpkSerializer();
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
		var serializer = new MpkSerializer();
		var corrupt = new byte[] { 0xFE, 0xFD, 0xFC };

		// Act & Assert
		var ex = Should.Throw<SerializationException>(() =>
			serializer.DeserializeObject(corrupt, typeof(TestPluggableMessage)));
		ex.InnerException.ShouldNotBeNull();
	}

	[Fact]
	[RequiresUnreferencedCode("Test")]
	[RequiresDynamicCode("Test")]
	public void PluggableSerializer_Serialize_WithUnserializableType_ThrowsException()
	{
		// Arrange
		var serializer = new MpkSerializer();
		var badObj = new NonSerializableForTests { Value = "bad" };

		// Act & Assert - The consolidated ISerializer.Serialize path propagates the native
		// MessagePack exception (exception wrapping is in Deserialize path only).
		Should.Throw<Exception>(() =>
			serializer.SerializeToBytes(badObj));
	}

	[Fact]
	[RequiresUnreferencedCode("Test")]
	[RequiresDynamicCode("Test")]
	public void PluggableSerializer_SerializeObject_WithUnserializableType_ThrowsSerializationException()
	{
		// Arrange
		var serializer = new MpkSerializer();
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
			services.AddMessagePackSerializer());
	}

	[Fact]
	public void AddMessagePackSerializer_WithNullServices_ThrowsArgumentNull()
	{
		// Arrange
		IServiceCollection services = null!;

		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			services.AddMessagePackSerializer());
	}

	[Fact]
	public void AddMessagePackPluggableSerialization_WithOptionsNull_ThrowsArgumentNull()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			services.AddMessagePackSerializer(null!));
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
