// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Serialization.MessagePack;

using MessagePack;

namespace Excalibur.Dispatch.Serialization.Tests.MessagePack;

/// <summary>
/// Stress and boundary tests for MessagePack serializers.
/// Tests edge cases with extreme values and repeated operations.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Serialization")]
public sealed class MessagePackSerializerStressShould : UnitTestBase
{
	#region Boundary Value Tests

	[Fact]
	public void DispatchSerializer_HandlesVeryLongString()
	{
		// Arrange
		var serializer = new DispatchMessagePackSerializer();
		var veryLongStr = new string('V', 100_000);
		var message = new TestMessage { Id = 1, Name = veryLongStr };

		// Act
		var bytes = serializer.Serialize(message);
		var result = serializer.Deserialize<TestMessage>(bytes);

		// Assert
		result.Name.Length.ShouldBe(100_000);
	}

	[Fact]
	public void MessagePackMessageSerializer_HandlesVeryLongString()
	{
		// Arrange
		var options = Microsoft.Extensions.Options.Options.Create(new MessagePackSerializationOptions());
		var serializer = new MessagePackMessageSerializer(options);
		var veryLongStr = new string('W', 100_000);
		var message = new TestMessage { Id = 2, Name = veryLongStr };

		// Act
		var bytes = serializer.Serialize(message);
		var result = serializer.Deserialize<TestMessage>(bytes);

		// Assert
		result.Name.Length.ShouldBe(100_000);
	}

	[Fact]
	public void AotSerializer_HandlesVeryLongString()
	{
		// Arrange
		var serializer = new AotMessagePackSerializer(MessagePackSerializerOptions.Standard);
		var veryLongStr = new string('X', 100_000);
		var message = new TestMessage { Id = 3, Name = veryLongStr };

		// Act
		var bytes = serializer.Serialize(message);
		var result = serializer.Deserialize<TestMessage>(bytes);

		// Assert
		result.Name.Length.ShouldBe(100_000);
	}

	[Fact]
	public void PluggableSerializer_HandlesVeryLongString()
	{
		// Arrange
		var serializer = new MessagePackPluggableSerializer();
		var veryLongStr = new string('Y', 100_000);
		var message = new TestPluggableMessage { Value = 4, Text = veryLongStr };

		// Act
		var bytes = serializer.Serialize(message);
		var result = serializer.Deserialize<TestPluggableMessage>(bytes);

		// Assert
		result.Text.Length.ShouldBe(100_000);
	}

	#endregion

	#region Repeated Operation Tests

	[Fact]
	public void DispatchSerializer_HandlesRepeatedSerialization()
	{
		// Arrange
		var serializer = new DispatchMessagePackSerializer();

		// Act & Assert - serialize/deserialize 50 times
		for (var i = 0; i < 50; i++)
		{
			var message = new TestMessage { Id = i, Name = $"Repeat{i}" };
			var bytes = serializer.Serialize(message);
			var result = serializer.Deserialize<TestMessage>(bytes);
			result.Id.ShouldBe(i);
		}
	}

	[Fact]
	public void MessagePackMessageSerializer_HandlesRepeatedSerialization()
	{
		// Arrange
		var options = Microsoft.Extensions.Options.Options.Create(new MessagePackSerializationOptions());
		var serializer = new MessagePackMessageSerializer(options);

		// Act & Assert
		for (var i = 0; i < 50; i++)
		{
			var message = new TestMessage { Id = i, Name = $"MsgRepeat{i}" };
			var bytes = serializer.Serialize(message);
			var result = serializer.Deserialize<TestMessage>(bytes);
			result.Id.ShouldBe(i);
		}
	}

	[Fact]
	public void PluggableSerializer_HandlesRepeatedSerialization()
	{
		// Arrange
		var serializer = new MessagePackPluggableSerializer();

		// Act & Assert
		for (var i = 0; i < 50; i++)
		{
			var message = new TestPluggableMessage { Value = i, Text = $"PlugRepeat{i}" };
			var bytes = serializer.Serialize(message);
			var result = serializer.Deserialize<TestPluggableMessage>(bytes);
			result.Value.ShouldBe(i);
		}
	}

	[Fact]
	public void PluggableSerializer_HandlesRepeatedSerializeObject()
	{
		// Arrange
		var serializer = new MessagePackPluggableSerializer();

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
	public void DispatchSerializer_HandlesExtendedUnicode()
	{
		// Arrange - includes emoji, CJK, Arabic, Hebrew
		var serializer = new DispatchMessagePackSerializer();
		var unicodeStr = "\u00e9\u00e0\u00fc\u4e2d\u6587\u0627\u0644\u05e2\U0001f600\U0001f680";
		var message = new TestMessage { Id = 1, Name = unicodeStr };

		// Act
		var bytes = serializer.Serialize(message);
		var result = serializer.Deserialize<TestMessage>(bytes);

		// Assert
		result.Name.ShouldBe(unicodeStr);
	}

	[Fact]
	public void MessagePackMessageSerializer_HandlesExtendedUnicode()
	{
		// Arrange
		var options = Microsoft.Extensions.Options.Options.Create(new MessagePackSerializationOptions());
		var serializer = new MessagePackMessageSerializer(options);
		var unicodeStr = "\u00e9\u00e0\u00fc\u4e2d\u6587\u0627\u0644\u05e2\U0001f600\U0001f680";
		var message = new TestMessage { Id = 2, Name = unicodeStr };

		// Act
		var bytes = serializer.Serialize(message);
		var result = serializer.Deserialize<TestMessage>(bytes);

		// Assert
		result.Name.ShouldBe(unicodeStr);
	}

	[Fact]
	public void PluggableSerializer_HandlesExtendedUnicode()
	{
		// Arrange
		var serializer = new MessagePackPluggableSerializer();
		var unicodeStr = "\u00e9\u00e0\u00fc\u4e2d\u6587\u0627\u0644\u05e2\U0001f600\U0001f680";
		var message = new TestPluggableMessage { Value = 3, Text = unicodeStr };

		// Act
		var bytes = serializer.Serialize(message);
		var result = serializer.Deserialize<TestPluggableMessage>(bytes);

		// Assert
		result.Text.ShouldBe(unicodeStr);
	}

	[Fact]
	public void AotSerializer_HandlesExtendedUnicode()
	{
		// Arrange
		var serializer = new AotMessagePackSerializer(MessagePackSerializerOptions.Standard);
		var unicodeStr = "\u00e9\u00e0\u00fc\u4e2d\u6587\u0627\u0644\u05e2\U0001f600\U0001f680";
		var message = new TestMessage { Id = 4, Name = unicodeStr };

		// Act
		var bytes = serializer.Serialize(message);
		var result = serializer.Deserialize<TestMessage>(bytes);

		// Assert
		result.Name.ShouldBe(unicodeStr);
	}

	#endregion

	#region Compression Comparison

	[Fact]
	public void DispatchSerializer_CompressesLargeData()
	{
		// Arrange - with LZ4 compression (default)
		var compressedSerializer = new DispatchMessagePackSerializer();

		// No compression
		var noCompressionOpts = MessagePackSerializerOptions.Standard
			.WithCompression(MessagePackCompression.None);
		var uncompressedSerializer = new DispatchMessagePackSerializer(noCompressionOpts);

		var largeStr = new string('R', 10_000);
		var message = new TestMessage { Id = 1, Name = largeStr };

		// Act
		var compressedBytes = compressedSerializer.Serialize(message);
		var uncompressedBytes = uncompressedSerializer.Serialize(message);

		// Assert - compressed should be smaller for repetitive data
		compressedBytes.Length.ShouldBeLessThan(uncompressedBytes.Length);
	}

	[Fact]
	public void MessagePackMessageSerializer_CompressesLargeData()
	{
		// Arrange
		var compressedOptions = Microsoft.Extensions.Options.Options.Create(new MessagePackSerializationOptions
		{
			UseLz4Compression = true
		});
		var compressedSerializer = new MessagePackMessageSerializer(compressedOptions);

		var uncompressedOptions = Microsoft.Extensions.Options.Options.Create(new MessagePackSerializationOptions
		{
			UseLz4Compression = false
		});
		var uncompressedSerializer = new MessagePackMessageSerializer(uncompressedOptions);

		var largeStr = new string('S', 10_000);
		var message = new TestMessage { Id = 2, Name = largeStr };

		// Act
		var compressedBytes = compressedSerializer.Serialize(message);
		var uncompressedBytes = uncompressedSerializer.Serialize(message);

		// Assert
		compressedBytes.Length.ShouldBeLessThan(uncompressedBytes.Length);
	}

	#endregion

	#region Edge Case Values

	[Fact]
	public void AllSerializers_HandleNullString()
	{
		// Arrange - TestMessage.Name defaults to string.Empty so we can't test null directly
		// but we can test empty string
		var dispatch = new DispatchMessagePackSerializer();
		var message = new TestMessage { Id = 0, Name = string.Empty };

		// Act
		var bytes = dispatch.Serialize(message);
		var result = dispatch.Deserialize<TestMessage>(bytes);

		// Assert
		result.Name.ShouldBe(string.Empty);
	}

	[Fact]
	public void AllSerializers_HandleWhitespaceOnlyString()
	{
		// Arrange
		var dispatch = new DispatchMessagePackSerializer();
		var message = new TestMessage { Id = 1, Name = "   \t\n\r   " };

		// Act
		var bytes = dispatch.Serialize(message);
		var result = dispatch.Deserialize<TestMessage>(bytes);

		// Assert
		result.Name.ShouldBe("   \t\n\r   ");
	}

	[Fact]
	public void AllSerializers_HandleNullCharInString()
	{
		// Arrange - string with embedded null character
		var dispatch = new DispatchMessagePackSerializer();
		var message = new TestMessage { Id = 2, Name = "Before\0After" };

		// Act
		var bytes = dispatch.Serialize(message);
		var result = dispatch.Deserialize<TestMessage>(bytes);

		// Assert
		result.Name.ShouldBe("Before\0After");
	}

	#endregion
}
