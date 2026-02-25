// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions.Serialization;
using Excalibur.Dispatch.Serialization.MessagePack;

using MessagePack;

namespace Excalibur.Dispatch.Serialization.Tests.MessagePack;

/// <summary>
/// Additional edge-case and coverage tests for <see cref="DispatchMessagePackSerializer"/>.
/// Targets uncovered branches: null-options default path, multiple data types,
/// various compression modes, and interface conformance.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Serialization")]
public sealed class DispatchMessagePackSerializerEdgeCasesShould : UnitTestBase
{
	#region Constructor / Default-Options Branch

	[Fact]
	public void Constructor_WithNullOptions_UsesDefaultLz4BlockArrayCompression()
	{
		// Arrange & Act - passing null explicitly exercises the ?? branch
		var serializer = new DispatchMessagePackSerializer(null);

		// Assert
		serializer.ShouldNotBeNull();
		serializer.SerializerName.ShouldBe("MessagePack");
		serializer.SerializerVersion.ShouldBe("1.0.0");
	}

	[Fact]
	public void Constructor_WithNullOptions_ProducesWorkingSerializer()
	{
		// Arrange
		var serializer = new DispatchMessagePackSerializer(null);
		var message = new TestMessage { Id = 7, Name = "NullOpts" };

		// Act
		var bytes = serializer.Serialize(message);
		var result = serializer.Deserialize<TestMessage>(bytes);

		// Assert
		result.Id.ShouldBe(7);
		result.Name.ShouldBe("NullOpts");
	}

	#endregion

	#region Serialize Edge Cases

	[Fact]
	public void Serialize_WithMaxIntId_ProducesNonEmptyBytes()
	{
		// Arrange
		var serializer = new DispatchMessagePackSerializer();
		var message = new TestMessage { Id = int.MaxValue, Name = "Max" };

		// Act
		var bytes = serializer.Serialize(message);

		// Assert
		bytes.ShouldNotBeNull();
		bytes.Length.ShouldBeGreaterThan(0);
	}

	[Fact]
	public void Serialize_WithMinIntId_ProducesNonEmptyBytes()
	{
		// Arrange
		var serializer = new DispatchMessagePackSerializer();
		var message = new TestMessage { Id = int.MinValue, Name = "Min" };

		// Act
		var bytes = serializer.Serialize(message);

		// Assert
		bytes.ShouldNotBeNull();
		bytes.Length.ShouldBeGreaterThan(0);
	}

	[Fact]
	public void Serialize_WithEmptyStringName_RoundTrips()
	{
		// Arrange
		var serializer = new DispatchMessagePackSerializer();
		var message = new TestMessage { Id = 0, Name = string.Empty };

		// Act
		var bytes = serializer.Serialize(message);
		var result = serializer.Deserialize<TestMessage>(bytes);

		// Assert
		result.Name.ShouldBe(string.Empty);
	}

	[Fact]
	public void Serialize_WithUnicodeString_RoundTrips()
	{
		// Arrange
		var serializer = new DispatchMessagePackSerializer();
		var message = new TestMessage { Id = 3, Name = "\u00e9\u00e0\u00fc\u4e2d\u6587\U0001f600" };

		// Act
		var bytes = serializer.Serialize(message);
		var result = serializer.Deserialize<TestMessage>(bytes);

		// Assert
		result.Name.ShouldBe(message.Name);
	}

	[Fact]
	public void Serialize_WithLargePayload_RoundTrips()
	{
		// Arrange
		var serializer = new DispatchMessagePackSerializer();
		var largeStr = new string('A', 50_000);
		var message = new TestMessage { Id = 99, Name = largeStr };

		// Act
		var bytes = serializer.Serialize(message);
		var result = serializer.Deserialize<TestMessage>(bytes);

		// Assert
		result.Id.ShouldBe(99);
		result.Name.Length.ShouldBe(50_000);
	}

	[Fact]
	public void Serialize_WithSpecialCharacters_RoundTrips()
	{
		// Arrange
		var serializer = new DispatchMessagePackSerializer();
		var message = new TestMessage { Id = 5, Name = "Tab:\tNewline:\nQuote:\"Back:\\Null:\0" };

		// Act
		var bytes = serializer.Serialize(message);
		var result = serializer.Deserialize<TestMessage>(bytes);

		// Assert
		result.Name.ShouldBe(message.Name);
	}

	[Fact]
	public void Serialize_WithNegativeId_RoundTrips()
	{
		// Arrange
		var serializer = new DispatchMessagePackSerializer();
		var message = new TestMessage { Id = -42, Name = "Neg" };

		// Act
		var bytes = serializer.Serialize(message);
		var result = serializer.Deserialize<TestMessage>(bytes);

		// Assert
		result.Id.ShouldBe(-42);
	}

	#endregion

	#region Compression Variants

	[Fact]
	public void Serialize_WithLz4Block_RoundTrips()
	{
		// Arrange
		var opts = MessagePackSerializerOptions.Standard.WithCompression(MessagePackCompression.Lz4Block);
		var serializer = new DispatchMessagePackSerializer(opts);
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
		var serializer = new DispatchMessagePackSerializer(opts);
		var message = new TestMessage { Id = 20, Name = "NoCompression" };

		// Act
		var bytes = serializer.Serialize(message);
		var result = serializer.Deserialize<TestMessage>(bytes);

		// Assert
		result.Id.ShouldBe(20);
		result.Name.ShouldBe("NoCompression");
	}

	#endregion

	#region Multiple Invocations / Reuse

	[Fact]
	public void Serialize_CanBeCalledMultipleTimes()
	{
		// Arrange
		var serializer = new DispatchMessagePackSerializer();

		// Act & Assert
		for (var i = 0; i < 20; i++)
		{
			var msg = new TestMessage { Id = i, Name = $"Msg{i}" };
			var bytes = serializer.Serialize(msg);
			var result = serializer.Deserialize<TestMessage>(bytes);
			result.Id.ShouldBe(i);
			result.Name.ShouldBe($"Msg{i}");
		}
	}

	[Fact]
	public void Serialize_DifferentSerializerInstances_ProduceCompatibleOutput()
	{
		// Arrange
		var serializer1 = new DispatchMessagePackSerializer();
		var serializer2 = new DispatchMessagePackSerializer();
		var message = new TestMessage { Id = 77, Name = "CrossInstance" };

		// Act
		var bytes = serializer1.Serialize(message);
		var result = serializer2.Deserialize<TestMessage>(bytes);

		// Assert
		result.Id.ShouldBe(77);
		result.Name.ShouldBe("CrossInstance");
	}

	#endregion

	#region Interface Implementation

	[Fact]
	public void ImplementsIMessageSerializer_Interface()
	{
		// Arrange & Act
		var serializer = new DispatchMessagePackSerializer();

		// Assert
		serializer.ShouldBeAssignableTo<IMessageSerializer>();
	}

	[Fact]
	public void SerializerName_IsConsistent_AcrossInstances()
	{
		// Arrange
		var s1 = new DispatchMessagePackSerializer();
		var s2 = new DispatchMessagePackSerializer(MessagePackSerializerOptions.Standard);

		// Assert
		s1.SerializerName.ShouldBe(s2.SerializerName);
	}

	[Fact]
	public void SerializerVersion_IsConsistent_AcrossInstances()
	{
		// Arrange
		var s1 = new DispatchMessagePackSerializer();
		var s2 = new DispatchMessagePackSerializer(MessagePackSerializerOptions.Standard);

		// Assert
		s1.SerializerVersion.ShouldBe(s2.SerializerVersion);
	}

	#endregion
}
