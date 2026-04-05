// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions.Serialization;
using Excalibur.Dispatch.Serialization.MessagePack;

using MessagePack;

using MpkSerializer = Excalibur.Dispatch.Serialization.MessagePack.MessagePackSerializer;

namespace Excalibur.Dispatch.Serialization.Tests.MessagePack;

/// <summary>
/// Additional edge-case and coverage tests for <see cref="MpkSerializer"/>.
/// Tests options interaction, error messages, various configurations, and error-message branches.
/// Originally tested the now-deleted MessagePackMessageSerializer; updated to test the
/// consolidated <see cref="MpkSerializer"/> with equivalent behavior.
/// </summary>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Serialization)]
public sealed class MessagePackMessageSerializerEdgeCasesShould : UnitTestBase
{
	#region Options Configuration

	[Fact]
	public void Constructor_WithCompressionEnabled_CreatesWorkingSerializer()
	{
		// Arrange - Use native MessagePack options with LZ4 compression
		var opts = MessagePackSerializerOptions.Standard.WithCompression(MessagePackCompression.Lz4Block);

		// Act
		var serializer = new MpkSerializer(opts);

		// Assert
		serializer.ShouldNotBeNull();
		serializer.Name.ShouldBe("MessagePack");
	}

	[Fact]
	public void Constructor_WithAllOptionsSet_CreatesWorkingSerializer()
	{
		// Arrange
		var opts = MessagePackSerializerOptions.Standard.WithCompression(MessagePackCompression.Lz4Block);

		// Act
		var serializer = new MpkSerializer(opts);
		var message = new TestMessage { Id = 55, Name = "AllOpts" };
		var bytes = serializer.SerializeToBytes(message);
		var result = serializer.Deserialize<TestMessage>(bytes);

		// Assert
		result.Id.ShouldBe(55);
		result.Name.ShouldBe("AllOpts");
	}

	[Fact]
	public void Constructor_WithDefaultOptions_CompressionIsNone()
	{
		// Arrange - Default constructor uses Standard options (no compression)

		// Act
		var serializer = new MpkSerializer();
		var message = new TestMessage { Id = 1, Name = "NoCompress" };
		var bytes = serializer.SerializeToBytes(message);
		var result = serializer.Deserialize<TestMessage>(bytes);

		// Assert - just verify round-trip works without compression
		result.Id.ShouldBe(1);
		result.Name.ShouldBe("NoCompress");
	}

	#endregion

	#region Deserialize Error Messages

	[Fact]
	public void Deserialize_WhenResultIsNull_ExceptionContainsExpectedMessage()
	{
		// Arrange
		var serializer = new MpkSerializer();
		var nilData = new byte[] { 0xC0 };

		// Act - Now throws SerializationException (not InvalidOperationException)
		var ex = Should.Throw<SerializationException>(() =>
			serializer.Deserialize<TestMessage>(nilData.AsSpan()));

		// Assert - verify the error message contains null indication
		ex.Message.ShouldNotBeNullOrWhiteSpace();
		ex.Message.ShouldContain("null");
	}

	#endregion

	#region Multiple Message Types

	[Fact]
	public void Serialize_AndDeserialize_TestPluggableMessage_RoundTrips()
	{
		// Arrange
		var serializer = new MpkSerializer();
		var message = new TestPluggableMessage { Value = 999, Text = "Pluggable" };

		// Act
		var bytes = serializer.SerializeToBytes(message);
		var result = serializer.Deserialize<TestPluggableMessage>(bytes);

		// Assert
		result.Value.ShouldBe(999);
		result.Text.ShouldBe("Pluggable");
	}

	[Fact]
	public void Serialize_AndDeserialize_WithCompressionEnabled_TestPluggableMessage_RoundTrips()
	{
		// Arrange
		var opts = MessagePackSerializerOptions.Standard.WithCompression(MessagePackCompression.Lz4Block);
		var serializer = new MpkSerializer(opts);
		var message = new TestPluggableMessage { Value = 42, Text = "CompressedPluggable" };

		// Act
		var bytes = serializer.SerializeToBytes(message);
		var result = serializer.Deserialize<TestPluggableMessage>(bytes);

		// Assert
		result.Value.ShouldBe(42);
		result.Text.ShouldBe("CompressedPluggable");
	}

	#endregion

	#region Large Data

	[Fact]
	public void Serialize_WithVeryLargeString_RoundTrips()
	{
		// Arrange
		var serializer = new MpkSerializer();
		var largeStr = new string('Z', 100_000);
		var message = new TestMessage { Id = 88, Name = largeStr };

		// Act
		var bytes = serializer.SerializeToBytes(message);
		var result = serializer.Deserialize<TestMessage>(bytes);

		// Assert
		result.Id.ShouldBe(88);
		result.Name.Length.ShouldBe(100_000);
	}

	#endregion

	#region Reusability

	[Fact]
	public void Serialize_CalledMultipleTimes_IsReusable()
	{
		// Arrange
		var serializer = new MpkSerializer();

		// Act & Assert
		for (var i = 0; i < 15; i++)
		{
			var msg = new TestMessage { Id = i, Name = $"Reuse{i}" };
			var bytes = serializer.SerializeToBytes(msg);
			var result = serializer.Deserialize<TestMessage>(bytes);
			result.Id.ShouldBe(i);
			result.Name.ShouldBe($"Reuse{i}");
		}
	}

	#endregion

	#region Interface

	[Fact]
	public void ImplementsISerializer_WithValidOptions()
	{
		// Arrange
		var opts = MessagePackSerializerOptions.Standard.WithCompression(MessagePackCompression.Lz4Block);
		var serializer = new MpkSerializer(opts);

		// Assert
		serializer.ShouldBeAssignableTo<ISerializer>();
	}

	#endregion
}
