// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Buffers;

using Excalibur.Dispatch.Abstractions.Serialization;
using Excalibur.Dispatch.Serialization.MessagePack;

using MessagePack;

using MpkSerializer = Excalibur.Dispatch.Serialization.MessagePack.MessagePackSerializer;

namespace Excalibur.Dispatch.Serialization.Tests.MessagePack;

/// <summary>
/// Unit tests for <see cref="MpkSerializer" /> covering the ISerializer (zero-copy) behavior.
/// Originally tested the now-deleted MessagePackZeroCopySerializer; updated to test the
/// consolidated <see cref="MpkSerializer"/> with IBufferWriter-based Serialize and
/// ReadOnlySpan-based Deserialize.
/// </summary>
[Trait(TraitNames.Component, TestComponents.Serialization)]
[Trait(TraitNames.Category, TestCategories.Unit)]
public sealed class MessagePackZeroCopySerializerShould : UnitTestBase
{
	#region Constructor Tests

	[Fact]
	public void Constructor_CreatesSerializer()
	{
		// Arrange & Act
		var serializer = new MpkSerializer();

		// Assert
		_ = serializer.ShouldNotBeNull();
	}

	#endregion Constructor Tests

	#region Serialize and Deserialize Tests

	[Fact]
	public void Serialize_AndDeserialize_RoundTrips()
	{
		// Arrange
		var serializer = new MpkSerializer();
		var original = new TestMessage { Id = 456, Name = "Deserialize Test" };

		// Act
		var bytes = serializer.SerializeToBytes(original);
		var result = serializer.Deserialize<TestMessage>(bytes);

		// Assert
		_ = result.ShouldNotBeNull();
		result.Id.ShouldBe(456);
		result.Name.ShouldBe("Deserialize Test");
	}

	[Fact]
	public void Serialize_WithLz4Compression_RoundTrips()
	{
		// Arrange
		var options = MessagePackSerializerOptions.Standard.WithCompression(MessagePackCompression.Lz4BlockArray);
		var serializer = new MpkSerializer(options);
		var original = new TestMessage { Id = 123, Name = "LZ4 Test" };

		// Act
		var bytes = serializer.SerializeToBytes(original);
		var result = serializer.Deserialize<TestMessage>(bytes);

		// Assert
		_ = result.ShouldNotBeNull();
		result.Id.ShouldBe(123);
		result.Name.ShouldBe("LZ4 Test");
	}

	[Fact]
	public void Deserialize_WithUnicodeString_PreservesCharacters()
	{
		// Arrange
		var serializer = new MpkSerializer();
		var original = new TestMessage { Id = 1, Name = "Unicode: \u00e9\u00e0\u00fc\u4e2d\u6587" };

		// Act
		var bytes = serializer.SerializeToBytes(original);
		var result = serializer.Deserialize<TestMessage>(bytes);

		// Assert
		result.Name.ShouldBe(original.Name);
	}

	[Fact]
	public void Deserialize_WithEmptyName_ReturnsCorrectValue()
	{
		// Arrange
		var serializer = new MpkSerializer();
		var original = new TestMessage { Id = 1, Name = string.Empty };

		// Act
		var bytes = serializer.SerializeToBytes(original);
		var result = serializer.Deserialize<TestMessage>(bytes);

		// Assert
		result.Name.ShouldBe(string.Empty);
	}

	[Fact]
	public void Deserialize_WithZeroId_ReturnsCorrectValue()
	{
		// Arrange
		var serializer = new MpkSerializer();
		var original = new TestMessage { Id = 0, Name = "Zero" };

		// Act
		var bytes = serializer.SerializeToBytes(original);
		var result = serializer.Deserialize<TestMessage>(bytes);

		// Assert
		result.Id.ShouldBe(0);
	}

	[Fact]
	public void Deserialize_WithNegativeId_ReturnsCorrectValue()
	{
		// Arrange
		var serializer = new MpkSerializer();
		var original = new TestMessage { Id = -100, Name = "Negative" };

		// Act
		var bytes = serializer.SerializeToBytes(original);
		var result = serializer.Deserialize<TestMessage>(bytes);

		// Assert
		result.Id.ShouldBe(-100);
	}

	[Fact]
	public void Deserialize_WithMaxIntId_ReturnsCorrectValue()
	{
		// Arrange
		var serializer = new MpkSerializer();
		var original = new TestMessage { Id = int.MaxValue, Name = "Max" };

		// Act
		var bytes = serializer.SerializeToBytes(original);
		var result = serializer.Deserialize<TestMessage>(bytes);

		// Assert
		result.Id.ShouldBe(int.MaxValue);
	}

	[Fact]
	public void Deserialize_WithMinIntId_ReturnsCorrectValue()
	{
		// Arrange
		var serializer = new MpkSerializer();
		var original = new TestMessage { Id = int.MinValue, Name = "Min" };

		// Act
		var bytes = serializer.SerializeToBytes(original);
		var result = serializer.Deserialize<TestMessage>(bytes);

		// Assert
		result.Id.ShouldBe(int.MinValue);
	}

	[Fact]
	public void Deserialize_WithLargeString_ReturnsCorrectValue()
	{
		// Arrange
		var serializer = new MpkSerializer();
		var largeString = new string('X', 10000);
		var original = new TestMessage { Id = 99, Name = largeString };

		// Act
		var bytes = serializer.SerializeToBytes(original);
		var result = serializer.Deserialize<TestMessage>(bytes);

		// Assert
		result.Name.ShouldBe(largeString);
	}

	[Fact]
	public void Deserialize_WithSpecialCharacters_PreservesCharacters()
	{
		// Arrange
		var serializer = new MpkSerializer();
		var original = new TestMessage { Id = 2, Name = "Special: \t\n\r\"'\\/" };

		// Act
		var bytes = serializer.SerializeToBytes(original);
		var result = serializer.Deserialize<TestMessage>(bytes);

		// Assert
		result.Name.ShouldBe(original.Name);
	}

	[Fact]
	public void Deserialize_MultipleCallsWithSameSerializer_AllSucceed()
	{
		// Arrange
		var serializer = new MpkSerializer();
		var messages = Enumerable.Range(1, 10)
			.Select(i => new TestMessage { Id = i, Name = $"Message {i}" })
			.ToList();

		// Act & Assert - Verify serializer can be reused
		for (var i = 0; i < messages.Count; i++)
		{
			var bytes = serializer.SerializeToBytes(messages[i]);
			var result = serializer.Deserialize<TestMessage>(bytes);
			result.Id.ShouldBe(messages[i].Id);
			result.Name.ShouldBe(messages[i].Name);
		}
	}

	#endregion Serialize and Deserialize Tests

	#region IBufferWriter Serialize Tests

	[Fact]
	public void Serialize_ToBufferWriter_ProducesNonEmptyOutput()
	{
		// Arrange
		var serializer = new MpkSerializer();
		var message = new TestMessage { Id = 789, Name = "BufferWriter Test" };
		var bufferWriter = new ArrayBufferWriter<byte>();

		// Act
		serializer.Serialize(message, bufferWriter);

		// Assert
		bufferWriter.WrittenCount.ShouldBeGreaterThan(0);
	}

	[Fact]
	public void Serialize_ToBufferWriter_ProducesDeserializableOutput()
	{
		// Arrange
		var serializer = new MpkSerializer();
		var original = new TestMessage { Id = 42, Name = "Verifiable" };
		var bufferWriter = new ArrayBufferWriter<byte>();

		// Act
		serializer.Serialize(original, bufferWriter);
		var deserialized = serializer.Deserialize<TestMessage>(bufferWriter.WrittenSpan);

		// Assert
		_ = deserialized.ShouldNotBeNull();
		deserialized.Id.ShouldBe(42);
		deserialized.Name.ShouldBe("Verifiable");
	}

	#endregion IBufferWriter Serialize Tests

	#region Interface Implementation Tests

	[Fact]
	public void ImplementsISerializer()
	{
		// Arrange
		var serializer = new MpkSerializer();

		// Assert
		_ = serializer.ShouldBeAssignableTo<ISerializer>();
	}

	#endregion Interface Implementation Tests
}
