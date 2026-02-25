// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions.Serialization;
using Excalibur.Dispatch.Serialization.MessagePack;

using MessagePack;

namespace Excalibur.Dispatch.Serialization.Tests.MessagePack;

/// <summary>
/// Additional edge-case and coverage tests for <see cref="AotMessagePackSerializer"/>.
/// Targets: compression variants, multiple types, reusability, and error paths.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Serialization")]
public sealed class AotMessagePackSerializerEdgeCasesShould : UnitTestBase
{
	/// <summary>
	/// Standard test options (not using StaticCompositeResolver to work with test types).
	/// </summary>
	private static MessagePackSerializerOptions TestOptions =>
		MessagePackSerializerOptions.Standard.WithCompression(MessagePackCompression.Lz4BlockArray);

	#region Compression Variants

	[Fact]
	public void Serialize_WithLz4Block_RoundTrips()
	{
		// Arrange
		var opts = MessagePackSerializerOptions.Standard.WithCompression(MessagePackCompression.Lz4Block);
		var serializer = new AotMessagePackSerializer(opts);
		var message = new TestMessage { Id = 10, Name = "Lz4Block" };

		// Act
		var bytes = serializer.Serialize(message);
		var result = serializer.Deserialize<TestMessage>(bytes);

		// Assert
		result.Id.ShouldBe(10);
		result.Name.ShouldBe("Lz4Block");
	}

	[Fact]
	public void Serialize_WithNoCompression_RoundTrips()
	{
		// Arrange
		var opts = MessagePackSerializerOptions.Standard.WithCompression(MessagePackCompression.None);
		var serializer = new AotMessagePackSerializer(opts);
		var message = new TestMessage { Id = 20, Name = "NoCompress" };

		// Act
		var bytes = serializer.Serialize(message);
		var result = serializer.Deserialize<TestMessage>(bytes);

		// Assert
		result.Id.ShouldBe(20);
		result.Name.ShouldBe("NoCompress");
	}

	#endregion

	#region Multiple Types

	[Fact]
	public void Serialize_TestPluggableMessage_RoundTrips()
	{
		// Arrange
		var serializer = new AotMessagePackSerializer(TestOptions);
		var message = new TestPluggableMessage { Value = 55, Text = "Pluggable" };

		// Act
		var bytes = serializer.Serialize(message);
		var result = serializer.Deserialize<TestPluggableMessage>(bytes);

		// Assert
		result.Value.ShouldBe(55);
		result.Text.ShouldBe("Pluggable");
	}

	#endregion

	#region Reusability

	[Fact]
	public void Serialize_CalledMultipleTimes_IsReusable()
	{
		// Arrange
		var serializer = new AotMessagePackSerializer(TestOptions);

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

	[Fact]
	public void Serialize_DifferentInstances_ProduceCompatibleOutput()
	{
		// Arrange
		var serializer1 = new AotMessagePackSerializer(TestOptions);
		var serializer2 = new AotMessagePackSerializer(TestOptions);
		var message = new TestMessage { Id = 77, Name = "CrossInstance" };

		// Act
		var bytes = serializer1.Serialize(message);
		var result = serializer2.Deserialize<TestMessage>(bytes);

		// Assert
		result.Id.ShouldBe(77);
		result.Name.ShouldBe("CrossInstance");
	}

	#endregion

	#region Large Payload

	[Fact]
	public void Serialize_WithVeryLargePayload_RoundTrips()
	{
		// Arrange
		var serializer = new AotMessagePackSerializer(TestOptions);
		var largeStr = new string('B', 100_000);
		var message = new TestMessage { Id = 88, Name = largeStr };

		// Act
		var bytes = serializer.Serialize(message);
		var result = serializer.Deserialize<TestMessage>(bytes);

		// Assert
		result.Id.ShouldBe(88);
		result.Name.Length.ShouldBe(100_000);
	}

	#endregion

	#region Null/Empty Boundaries

	[Fact]
	public void Deserialize_WithEmptyArray_ThrowsException()
	{
		// Arrange
		var serializer = new AotMessagePackSerializer(TestOptions);

		// Act & Assert - empty byte array should cause deserialization failure
		Should.Throw<Exception>(() =>
			serializer.Deserialize<TestMessage>(Array.Empty<byte>()));
	}

	#endregion

	#region Interface

	[Fact]
	public void ImplementsIMessageSerializer_WithCustomOptions()
	{
		// Arrange
		var serializer = new AotMessagePackSerializer(TestOptions);

		// Assert
		serializer.ShouldBeAssignableTo<IMessageSerializer>();
	}

	[Fact]
	public void SerializerName_IsConsistent_WithCustomOptions()
	{
		// Arrange
		var serializer = new AotMessagePackSerializer(TestOptions);

		// Assert
		serializer.SerializerName.ShouldBe("MessagePack-AOT");
	}

	[Fact]
	public void SerializerVersion_IsConsistent_WithCustomOptions()
	{
		// Arrange
		var serializer = new AotMessagePackSerializer(TestOptions);

		// Assert
		serializer.SerializerVersion.ShouldBe("1.0.0");
	}

	#endregion
}
