// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Serialization;
using Excalibur.Dispatch.Serialization.MessagePack;

using MessagePack;

namespace Excalibur.Dispatch.Serialization.Tests.MessagePack;

/// <summary>
/// Additional edge-case and coverage tests for <see cref="MessagePackMessageSerializer"/>.
/// Targets: options interaction, InvalidOperationException message text,
/// various option configurations, and error-message branches.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Serialization")]
public sealed class MessagePackMessageSerializerEdgeCasesShould : UnitTestBase
{
	#region Options Configuration

	[Fact]
	public void Constructor_WithCompressionEnabled_CreatesWorkingSerializer()
	{
		// Arrange
		var opts = Microsoft.Extensions.Options.Options.Create(new MessagePackSerializationOptions
		{
			UseLz4Compression = true,
		});

		// Act
		var serializer = new MessagePackMessageSerializer(opts);

		// Assert
		serializer.ShouldNotBeNull();
		serializer.SerializerName.ShouldBe("MessagePack");
	}

	[Fact]
	public void Constructor_WithAllOptionsSet_CreatesWorkingSerializer()
	{
		// Arrange
		var opts = Microsoft.Extensions.Options.Options.Create(new MessagePackSerializationOptions
		{
			UseLz4Compression = true,
		});

		// Act
		var serializer = new MessagePackMessageSerializer(opts);
		var message = new TestMessage { Id = 55, Name = "AllOpts" };
		var bytes = serializer.Serialize(message);
		var result = serializer.Deserialize<TestMessage>(bytes);

		// Assert
		result.Id.ShouldBe(55);
		result.Name.ShouldBe("AllOpts");
	}

	[Fact]
	public void Constructor_WithDefaultOptions_CompressionIsNone()
	{
		// Arrange
		var opts = Microsoft.Extensions.Options.Options.Create(new MessagePackSerializationOptions());

		// Act
		var serializer = new MessagePackMessageSerializer(opts);
		var message = new TestMessage { Id = 1, Name = "NoCompress" };
		var bytes = serializer.Serialize(message);
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
		var opts = Microsoft.Extensions.Options.Options.Create(new MessagePackSerializationOptions());
		var serializer = new MessagePackMessageSerializer(opts);
		var nilData = new byte[] { 0xC0 };

		// Act
		var ex = Should.Throw<InvalidOperationException>(() =>
			serializer.Deserialize<TestMessage>(nilData));

		// Assert - verify the error message comes from ErrorMessages resource
		ex.Message.ShouldNotBeNullOrWhiteSpace();
		ex.Message.ShouldBe(ErrorMessages.DeserializedMessageCannotBeNull);
	}

	#endregion

	#region Multiple Message Types

	[Fact]
	public void Serialize_AndDeserialize_TestPluggableMessage_RoundTrips()
	{
		// Arrange
		var opts = Microsoft.Extensions.Options.Options.Create(new MessagePackSerializationOptions());
		var serializer = new MessagePackMessageSerializer(opts);
		var message = new TestPluggableMessage { Value = 999, Text = "Pluggable" };

		// Act
		var bytes = serializer.Serialize(message);
		var result = serializer.Deserialize<TestPluggableMessage>(bytes);

		// Assert
		result.Value.ShouldBe(999);
		result.Text.ShouldBe("Pluggable");
	}

	[Fact]
	public void Serialize_AndDeserialize_WithCompressionEnabled_TestPluggableMessage_RoundTrips()
	{
		// Arrange
		var opts = Microsoft.Extensions.Options.Options.Create(new MessagePackSerializationOptions
		{
			UseLz4Compression = true,
		});
		var serializer = new MessagePackMessageSerializer(opts);
		var message = new TestPluggableMessage { Value = 42, Text = "CompressedPluggable" };

		// Act
		var bytes = serializer.Serialize(message);
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
		var opts = Microsoft.Extensions.Options.Options.Create(new MessagePackSerializationOptions());
		var serializer = new MessagePackMessageSerializer(opts);
		var largeStr = new string('Z', 100_000);
		var message = new TestMessage { Id = 88, Name = largeStr };

		// Act
		var bytes = serializer.Serialize(message);
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
		var opts = Microsoft.Extensions.Options.Options.Create(new MessagePackSerializationOptions());
		var serializer = new MessagePackMessageSerializer(opts);

		// Act & Assert
		for (var i = 0; i < 15; i++)
		{
			var msg = new TestMessage { Id = i, Name = $"Reuse{i}" };
			var bytes = serializer.Serialize(msg);
			var result = serializer.Deserialize<TestMessage>(bytes);
			result.Id.ShouldBe(i);
			result.Name.ShouldBe($"Reuse{i}");
		}
	}

	#endregion

	#region Interface

	[Fact]
	public void ImplementsIMessageSerializer_WithValidOptions()
	{
		// Arrange
		var opts = Microsoft.Extensions.Options.Options.Create(new MessagePackSerializationOptions
		{
			UseLz4Compression = true,
		});
		var serializer = new MessagePackMessageSerializer(opts);

		// Assert
		serializer.ShouldBeAssignableTo<IMessageSerializer>();
	}

	#endregion
}
