// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Serialization.MessagePack;

using MessagePack;

using MpkSerializer = Excalibur.Dispatch.Serialization.MessagePack.MessagePackSerializer;

namespace Excalibur.Dispatch.Serialization.Tests.MessagePack;

/// <summary>
/// Stress and boundary tests for the consolidated <see cref="MpkSerializer"/>.
/// Tests edge cases with extreme values and repeated operations.
/// </summary>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Serialization)]
public sealed class MessagePackSerializerStressShould : UnitTestBase
{
	#region Boundary Value Tests

	[Fact]
	public void DefaultSerializer_HandlesVeryLongString()
	{
		// Arrange
		var serializer = new MpkSerializer();
		var veryLongStr = new string('V', 100_000);
		var message = new TestMessage { Id = 1, Name = veryLongStr };

		// Act
		var bytes = serializer.SerializeToBytes(message);
		var result = serializer.Deserialize<TestMessage>(bytes);

		// Assert
		result.Name.Length.ShouldBe(100_000);
	}

	[Fact]
	public void NoCompressionSerializer_HandlesVeryLongString()
	{
		// Arrange
		var opts = MessagePackSerializerOptions.Standard.WithCompression(MessagePackCompression.None);
		var serializer = new MpkSerializer(opts);
		var veryLongStr = new string('W', 100_000);
		var message = new TestMessage { Id = 2, Name = veryLongStr };

		// Act
		var bytes = serializer.SerializeToBytes(message);
		var result = serializer.Deserialize<TestMessage>(bytes);

		// Assert
		result.Name.Length.ShouldBe(100_000);
	}

	[Fact]
	public void StandardSerializer_HandlesVeryLongString()
	{
		// Arrange
		var serializer = new MpkSerializer(MessagePackSerializerOptions.Standard);
		var veryLongStr = new string('X', 100_000);
		var message = new TestMessage { Id = 3, Name = veryLongStr };

		// Act
		var bytes = serializer.SerializeToBytes(message);
		var result = serializer.Deserialize<TestMessage>(bytes);

		// Assert
		result.Name.Length.ShouldBe(100_000);
	}

	[Fact]
	public void Serializer_HandlesVeryLongString_WithPluggableMessage()
	{
		// Arrange
		var serializer = new MpkSerializer();
		var veryLongStr = new string('Y', 100_000);
		var message = new TestPluggableMessage { Value = 4, Text = veryLongStr };

		// Act
		var bytes = serializer.SerializeToBytes(message);
		var result = serializer.Deserialize<TestPluggableMessage>(bytes);

		// Assert
		result.Text.Length.ShouldBe(100_000);
	}

	#endregion

	#region Repeated Operation Tests

	[Fact]
	public void DefaultSerializer_HandlesRepeatedSerialization()
	{
		// Arrange
		var serializer = new MpkSerializer();

		// Act & Assert - serialize/deserialize 50 times
		for (var i = 0; i < 50; i++)
		{
			var message = new TestMessage { Id = i, Name = $"Repeat{i}" };
			var bytes = serializer.SerializeToBytes(message);
			var result = serializer.Deserialize<TestMessage>(bytes);
			result.Id.ShouldBe(i);
		}
	}

	[Fact]
	public void NoCompressionSerializer_HandlesRepeatedSerialization()
	{
		// Arrange
		var opts = MessagePackSerializerOptions.Standard.WithCompression(MessagePackCompression.None);
		var serializer = new MpkSerializer(opts);

		// Act & Assert
		for (var i = 0; i < 50; i++)
		{
			var message = new TestMessage { Id = i, Name = $"MsgRepeat{i}" };
			var bytes = serializer.SerializeToBytes(message);
			var result = serializer.Deserialize<TestMessage>(bytes);
			result.Id.ShouldBe(i);
		}
	}

	[Fact]
	public void Serializer_HandlesRepeatedSerialization_WithPluggableMessage()
	{
		// Arrange
		var serializer = new MpkSerializer();

		// Act & Assert
		for (var i = 0; i < 50; i++)
		{
			var message = new TestPluggableMessage { Value = i, Text = $"PlugRepeat{i}" };
			var bytes = serializer.SerializeToBytes(message);
			var result = serializer.Deserialize<TestPluggableMessage>(bytes);
			result.Value.ShouldBe(i);
		}
	}

	[Fact]
	public void Serializer_HandlesRepeatedSerializeObject()
	{
		// Arrange
		var serializer = new MpkSerializer();

		// Act & Assert
		for (var i = 0; i < 50; i++)
		{
			var message = new TestPluggableMessage { Value = i * 10, Text = $"ObjRepeat{i}" };
			var bytes = serializer.SerializeObject(message, typeof(TestPluggableMessage));
			var result = (TestPluggableMessage)serializer.DeserializeObject(bytes, typeof(TestPluggableMessage));
			result.Value.ShouldBe(i * 10);
		}
	}

	#endregion

	#region Unicode Stress Tests

	[Fact]
	public void DefaultSerializer_HandlesExtendedUnicode()
	{
		// Arrange - includes emoji, CJK, Arabic, Hebrew
		var serializer = new MpkSerializer();
		var unicodeStr = "\u00e9\u00e0\u00fc\u4e2d\u6587\u0627\u0644\u05e2\U0001f600\U0001f680";
		var message = new TestMessage { Id = 1, Name = unicodeStr };

		// Act
		var bytes = serializer.SerializeToBytes(message);
		var result = serializer.Deserialize<TestMessage>(bytes);

		// Assert
		result.Name.ShouldBe(unicodeStr);
	}

	[Fact]
	public void NoCompressionSerializer_HandlesExtendedUnicode()
	{
		// Arrange
		var opts = MessagePackSerializerOptions.Standard.WithCompression(MessagePackCompression.None);
		var serializer = new MpkSerializer(opts);
		var unicodeStr = "\u00e9\u00e0\u00fc\u4e2d\u6587\u0627\u0644\u05e2\U0001f600\U0001f680";
		var message = new TestMessage { Id = 2, Name = unicodeStr };

		// Act
		var bytes = serializer.SerializeToBytes(message);
		var result = serializer.Deserialize<TestMessage>(bytes);

		// Assert
		result.Name.ShouldBe(unicodeStr);
	}

	[Fact]
	public void Serializer_HandlesExtendedUnicode_WithPluggableMessage()
	{
		// Arrange
		var serializer = new MpkSerializer();
		var unicodeStr = "\u00e9\u00e0\u00fc\u4e2d\u6587\u0627\u0644\u05e2\U0001f600\U0001f680";
		var message = new TestPluggableMessage { Value = 3, Text = unicodeStr };

		// Act
		var bytes = serializer.SerializeToBytes(message);
		var result = serializer.Deserialize<TestPluggableMessage>(bytes);

		// Assert
		result.Text.ShouldBe(unicodeStr);
	}

	[Fact]
	public void StandardSerializer_HandlesExtendedUnicode()
	{
		// Arrange
		var serializer = new MpkSerializer(MessagePackSerializerOptions.Standard);
		var unicodeStr = "\u00e9\u00e0\u00fc\u4e2d\u6587\u0627\u0644\u05e2\U0001f600\U0001f680";
		var message = new TestMessage { Id = 4, Name = unicodeStr };

		// Act
		var bytes = serializer.SerializeToBytes(message);
		var result = serializer.Deserialize<TestMessage>(bytes);

		// Assert
		result.Name.ShouldBe(unicodeStr);
	}

	#endregion

	#region Compression Comparison

	[Fact]
	public void Lz4Serializer_CompressesLargeData()
	{
		// Arrange - with LZ4 compression
		var compressedOpts = MessagePackSerializerOptions.Standard
			.WithCompression(MessagePackCompression.Lz4BlockArray);
		var compressedSerializer = new MpkSerializer(compressedOpts);

		// No compression
		var noCompressionOpts = MessagePackSerializerOptions.Standard
			.WithCompression(MessagePackCompression.None);
		var uncompressedSerializer = new MpkSerializer(noCompressionOpts);

		var largeStr = new string('R', 10_000);
		var message = new TestMessage { Id = 1, Name = largeStr };

		// Act
		var compressedBytes = compressedSerializer.SerializeToBytes(message);
		var uncompressedBytes = uncompressedSerializer.SerializeToBytes(message);

		// Assert - compressed should be smaller for repetitive data
		compressedBytes.Length.ShouldBeLessThan(uncompressedBytes.Length);
	}

	[Fact]
	public void Lz4Block_CompressesLargeData()
	{
		// Arrange
		var compressedOpts = MessagePackSerializerOptions.Standard
			.WithCompression(MessagePackCompression.Lz4Block);
		var compressedSerializer = new MpkSerializer(compressedOpts);

		var uncompressedOpts = MessagePackSerializerOptions.Standard
			.WithCompression(MessagePackCompression.None);
		var uncompressedSerializer = new MpkSerializer(uncompressedOpts);

		var largeStr = new string('S', 10_000);
		var message = new TestMessage { Id = 2, Name = largeStr };

		// Act
		var compressedBytes = compressedSerializer.SerializeToBytes(message);
		var uncompressedBytes = uncompressedSerializer.SerializeToBytes(message);

		// Assert
		compressedBytes.Length.ShouldBeLessThan(uncompressedBytes.Length);
	}

	#endregion

	#region Edge Case Values

	[Fact]
	public void Serializer_HandleEmptyString()
	{
		// Arrange
		var serializer = new MpkSerializer();
		var message = new TestMessage { Id = 0, Name = string.Empty };

		// Act
		var bytes = serializer.SerializeToBytes(message);
		var result = serializer.Deserialize<TestMessage>(bytes);

		// Assert
		result.Name.ShouldBe(string.Empty);
	}

	[Fact]
	public void Serializer_HandleWhitespaceOnlyString()
	{
		// Arrange
		var serializer = new MpkSerializer();
		var message = new TestMessage { Id = 1, Name = "   \t\n\r   " };

		// Act
		var bytes = serializer.SerializeToBytes(message);
		var result = serializer.Deserialize<TestMessage>(bytes);

		// Assert
		result.Name.ShouldBe("   \t\n\r   ");
	}

	[Fact]
	public void Serializer_HandleNullCharInString()
	{
		// Arrange - string with embedded null character
		var serializer = new MpkSerializer();
		var message = new TestMessage { Id = 2, Name = "Before\0After" };

		// Act
		var bytes = serializer.SerializeToBytes(message);
		var result = serializer.Deserialize<TestMessage>(bytes);

		// Assert
		result.Name.ShouldBe("Before\0After");
	}

	#endregion
}
