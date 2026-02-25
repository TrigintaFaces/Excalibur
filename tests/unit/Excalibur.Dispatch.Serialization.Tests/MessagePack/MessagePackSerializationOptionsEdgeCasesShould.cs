// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Serialization.MessagePack;

using MessagePack;

namespace Excalibur.Dispatch.Serialization.Tests.MessagePack;

/// <summary>
/// Additional edge-case and coverage tests for <see cref="MessagePackSerializationOptions"/>.
/// Targets: the <see cref="MessagePackSerializationOptions.MessagePackSerializerOptions"/> property
/// branches with various configurations, ensuring all getter paths are exercised.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Serialization")]
public sealed class MessagePackSerializationOptionsEdgeCasesShould : UnitTestBase
{
	#region MessagePackSerializerOptions Property

	[Fact]
	public void MessagePackSerializerOptions_WithCompressionTrue_ReturnsLz4Block()
	{
		// Arrange
		var options = new MessagePackSerializationOptions
		{
			UseLz4Compression = true,
		};

		// Act
		var serializerOptions = options.MessagePackSerializerOptions;

		// Assert
		serializerOptions.ShouldNotBeNull();
		serializerOptions.Compression.ShouldBe(MessagePackCompression.Lz4Block);
	}

	[Fact]
	public void MessagePackSerializerOptions_WithCompressionFalse_ReturnsNoneCompression()
	{
		// Arrange
		var options = new MessagePackSerializationOptions
		{
			UseLz4Compression = false,
		};

		// Act
		var serializerOptions = options.MessagePackSerializerOptions;

		// Assert
		serializerOptions.ShouldNotBeNull();
		serializerOptions.Compression.ShouldBe(MessagePackCompression.None);
	}

	[Fact]
	public void MessagePackSerializerOptions_CalledMultipleTimes_ReturnsConsistentResults()
	{
		// Arrange
		var options = new MessagePackSerializationOptions
		{
			UseLz4Compression = true,
		};

		// Act
		var result1 = options.MessagePackSerializerOptions;
		var result2 = options.MessagePackSerializerOptions;

		// Assert
		result1.Compression.ShouldBe(result2.Compression);
	}

	[Fact]
	public void MessagePackSerializerOptions_AfterChangingCompression_ReflectsNewValue()
	{
		// Arrange
		var options = new MessagePackSerializationOptions
		{
			UseLz4Compression = false,
		};

		// Act - get without compression
		var noCompression = options.MessagePackSerializerOptions;
		noCompression.Compression.ShouldBe(MessagePackCompression.None);

		// Change to compressed
		options.UseLz4Compression = true;
		var withCompression = options.MessagePackSerializerOptions;

		// Assert
		withCompression.Compression.ShouldBe(MessagePackCompression.Lz4Block);
	}

	#endregion

	#region Property Combinations

	[Fact]
	public void AllProperties_CanBeSetSimultaneously()
	{
		// Arrange & Act
		var options = new MessagePackSerializationOptions
		{
			UseLz4Compression = true,
		};

		// Assert
		options.UseLz4Compression.ShouldBeTrue();
	}

	[Fact]
	public void UseLz4Compression_DefaultsFalse_ThenCanBeToggled()
	{
		// Arrange
		var options = new MessagePackSerializationOptions();

		// Assert default
		options.UseLz4Compression.ShouldBeFalse();

		// Act - toggle
		options.UseLz4Compression = true;
		options.UseLz4Compression.ShouldBeTrue();

		options.UseLz4Compression = false;
		options.UseLz4Compression.ShouldBeFalse();
	}

	#endregion

	#region Serialization With Options

	[Fact]
	public void MessagePackSerializerOptions_WithCompression_CanSerializeAndDeserialize()
	{
		// Arrange
		var options = new MessagePackSerializationOptions { UseLz4Compression = true };
		var serializerOptions = options.MessagePackSerializerOptions;
		var message = new TestMessage { Id = 42, Name = "CompressedOptions" };

		// Act
		var bytes = global::MessagePack.MessagePackSerializer.Serialize(message, serializerOptions);
		var result = global::MessagePack.MessagePackSerializer.Deserialize<TestMessage>(bytes, serializerOptions);

		// Assert
		result.Id.ShouldBe(42);
		result.Name.ShouldBe("CompressedOptions");
	}

	[Fact]
	public void MessagePackSerializerOptions_WithoutCompression_CanSerializeAndDeserialize()
	{
		// Arrange
		var options = new MessagePackSerializationOptions { UseLz4Compression = false };
		var serializerOptions = options.MessagePackSerializerOptions;
		var message = new TestMessage { Id = 77, Name = "NoCompressOptions" };

		// Act
		var bytes = global::MessagePack.MessagePackSerializer.Serialize(message, serializerOptions);
		var result = global::MessagePack.MessagePackSerializer.Deserialize<TestMessage>(bytes, serializerOptions);

		// Assert
		result.Id.ShouldBe(77);
		result.Name.ShouldBe("NoCompressOptions");
	}

	#endregion
}
