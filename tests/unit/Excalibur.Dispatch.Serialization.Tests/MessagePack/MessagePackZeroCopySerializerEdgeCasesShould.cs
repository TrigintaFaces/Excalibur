// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Buffers;

using Excalibur.Dispatch.Abstractions.Serialization;
using Excalibur.Dispatch.Serialization.MessagePack;

using MessagePack;

using MpkSerializer = Excalibur.Dispatch.Serialization.MessagePack.MessagePackSerializer;

namespace Excalibur.Dispatch.Serialization.Tests.MessagePack;

/// <summary>
/// Additional edge-case and coverage tests for <see cref="MpkSerializer"/>.
/// Targets: additional deserialization paths, error propagation branches,
/// Deserialize(ReadOnlySpan) additional cases, and IBufferWriter edge cases.
/// Originally tested the now-deleted MessagePackZeroCopySerializer; updated to test the
/// consolidated <see cref="MpkSerializer"/>.
/// </summary>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Serialization)]
public sealed class MessagePackZeroCopySerializerEdgeCasesShould : UnitTestBase
{
	#region Deserialize - Additional Cases

	[Fact]
	public void Deserialize_WithStandardData_Works()
	{
		// Arrange - serialize using the same options the serializer uses internally
		var serializer = new MpkSerializer();
		var original = new TestMessage { Id = 99, Name = "Standard" };
		var data = serializer.SerializeToBytes(original);

		// Act
		var result = serializer.Deserialize<TestMessage>(data);

		// Assert
		result.Id.ShouldBe(99);
		result.Name.ShouldBe("Standard");
	}

	[Fact]
	public void Deserialize_WithPluggableMessage_Works()
	{
		// Arrange
		var serializer = new MpkSerializer();
		var original = new TestPluggableMessage { Value = 55, Text = "PluggableZeroCopy" };
		var data = serializer.SerializeToBytes(original);

		// Act
		var result = serializer.Deserialize<TestPluggableMessage>(data);

		// Assert
		result.Value.ShouldBe(55);
		result.Text.ShouldBe("PluggableZeroCopy");
	}

	[Fact]
	public void Deserialize_MultipleDifferentTypes_AllSucceed()
	{
		// Arrange
		var serializer = new MpkSerializer();

		var msg = new TestMessage { Id = 10, Name = "TypeA" };
		var plug = new TestPluggableMessage { Value = 20, Text = "TypeB" };

		var msgData = serializer.SerializeToBytes(msg);
		var plugData = serializer.SerializeToBytes(plug);

		// Act
		var resultMsg = serializer.Deserialize<TestMessage>(msgData);
		var resultPlug = serializer.Deserialize<TestPluggableMessage>(plugData);

		// Assert
		resultMsg.Id.ShouldBe(10);
		resultPlug.Value.ShouldBe(20);
	}

	#endregion

	#region IBufferWriter Serialize - Additional Paths

	[Fact]
	public void Serialize_ToBufferWriter_WithMultipleMessages_AllWriteSuccessfully()
	{
		// Arrange
		var serializer = new MpkSerializer();

		for (var i = 0; i < 5; i++)
		{
			var bufferWriter = new ArrayBufferWriter<byte>();
			var message = new TestMessage { Id = i, Name = $"Multi{i}" };

			// Act
			serializer.Serialize(message, bufferWriter);

			// Assert
			bufferWriter.WrittenCount.ShouldBeGreaterThan(0);
		}
	}

	[Fact]
	public void Serialize_ToBufferWriter_WithNegativeId_Writes()
	{
		// Arrange
		var serializer = new MpkSerializer();
		var message = new TestMessage { Id = -1, Name = "NegBuf" };
		var bufferWriter = new ArrayBufferWriter<byte>();

		// Act
		serializer.Serialize(message, bufferWriter);

		// Assert
		bufferWriter.WrittenCount.ShouldBeGreaterThan(0);
	}

	[Fact]
	public void Serialize_ToBufferWriter_WithSpecialCharacters_Writes()
	{
		// Arrange
		var serializer = new MpkSerializer();
		var message = new TestMessage { Id = 3, Name = "Tab:\t\nNewline" };
		var bufferWriter = new ArrayBufferWriter<byte>();

		// Act
		serializer.Serialize(message, bufferWriter);

		// Assert
		bufferWriter.WrittenCount.ShouldBeGreaterThan(0);
	}

	[Fact]
	public void Serialize_ToBufferWriter_WithUnicodeCharacters_Writes()
	{
		// Arrange
		var serializer = new MpkSerializer();
		var message = new TestMessage { Id = 4, Name = "\u00e9\u4e2d\u6587\U0001f600" };
		var bufferWriter = new ArrayBufferWriter<byte>();

		// Act
		serializer.Serialize(message, bufferWriter);

		// Assert
		bufferWriter.WrittenCount.ShouldBeGreaterThan(0);
	}

	[Fact]
	public void Serialize_ToBufferWriter_OutputIsDeserializable()
	{
		// Arrange - verify that buffer-written output can be deserialized
		var serializer = new MpkSerializer();
		var original = new TestMessage { Id = 88, Name = "BufToSync" };
		var bufferWriter = new ArrayBufferWriter<byte>();

		// Act
		serializer.Serialize(original, bufferWriter);
		var result = serializer.Deserialize<TestMessage>(bufferWriter.WrittenSpan);

		// Assert
		result.Id.ShouldBe(88);
		result.Name.ShouldBe("BufToSync");
	}

	#endregion

	#region Interface Implementation

	[Fact]
	public void ImplementsISerializer_Interface()
	{
		// Arrange
		var serializer = new MpkSerializer();

		// Assert
		serializer.ShouldBeAssignableTo<ISerializer>();
	}

	#endregion

	#region Constructor

	[Fact]
	public void Constructor_CreatesNonNullInstance()
	{
		// Act
		var serializer = new MpkSerializer();

		// Assert
		serializer.ShouldNotBeNull();
	}

	[Fact]
	public void Constructor_CreatesReusableInstance()
	{
		// Arrange
		var serializer = new MpkSerializer();

		// Act - use the same instance for multiple serializations
		for (var i = 0; i < 5; i++)
		{
			var original = new TestMessage { Id = i, Name = $"Reuse{i}" };
			var data = serializer.SerializeToBytes(original);
			var result = serializer.Deserialize<TestMessage>(data);
			result.Id.ShouldBe(i);
		}
	}

	#endregion
}
