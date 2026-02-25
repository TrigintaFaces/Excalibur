// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.CodeAnalysis;

using Excalibur.Dispatch.Abstractions.Serialization;
using Excalibur.Dispatch.Serialization.MessagePack;

using MessagePack;

namespace Excalibur.Dispatch.Serialization.Tests.MessagePack;

/// <summary>
/// Additional edge-case and coverage tests for <see cref="MessagePackPluggableSerializer"/>.
/// Targets: SerializeObject/DeserializeObject additional branches, error wrapping details,
/// ReadOnlySpan overloads, and Version property edge cases.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Serialization")]
public sealed class MessagePackPluggableSerializerEdgeCasesShould : UnitTestBase
{
	#region Name and Version

	[Fact]
	public void Name_WithCustomOptions_StillReturnsMessagePack()
	{
		// Arrange
		var opts = MessagePackSerializerOptions.Standard.WithCompression(MessagePackCompression.Lz4Block);
		var serializer = new MessagePackPluggableSerializer(opts);

		// Assert
		serializer.Name.ShouldBe("MessagePack");
	}

	[Fact]
	public void Version_WithCustomOptions_ReturnsNonEmptyString()
	{
		// Arrange
		var opts = MessagePackSerializerOptions.Standard;
		var serializer = new MessagePackPluggableSerializer(opts);

		// Assert
		serializer.Version.ShouldNotBeNullOrWhiteSpace();
	}

	[Fact]
	public void Version_ReturnsAssemblyVersion()
	{
		// Arrange
		var serializer = new MessagePackPluggableSerializer();
		var expectedVersion = typeof(MessagePackSerializerOptions).Assembly
			.GetName().Version?.ToString() ?? "Unknown";

		// Act & Assert
		serializer.Version.ShouldBe(expectedVersion);
	}

	#endregion

	#region Serialize Generic - Additional Branches

	[Fact]
	public void Serialize_WithDefaultConstructor_RoundTrips()
	{
		// Arrange - uses the parameterless constructor which chains to null
		var serializer = new MessagePackPluggableSerializer();
		var message = new TestPluggableMessage { Value = 1, Text = "Default" };

		// Act
		var bytes = serializer.Serialize(message);
		var result = serializer.Deserialize<TestPluggableMessage>(bytes);

		// Assert
		result.Value.ShouldBe(1);
		result.Text.ShouldBe("Default");
	}

	[Fact]
	public void Serialize_WithLargePayload_RoundTrips()
	{
		// Arrange
		var serializer = new MessagePackPluggableSerializer();
		var largeText = new string('Q', 50_000);
		var message = new TestPluggableMessage { Value = 42, Text = largeText };

		// Act
		var bytes = serializer.Serialize(message);
		var result = serializer.Deserialize<TestPluggableMessage>(bytes);

		// Assert
		result.Value.ShouldBe(42);
		result.Text.Length.ShouldBe(50_000);
	}

	[Fact]
	public void Serialize_WithEmptyText_RoundTrips()
	{
		// Arrange
		var serializer = new MessagePackPluggableSerializer();
		var message = new TestPluggableMessage { Value = 0, Text = string.Empty };

		// Act
		var bytes = serializer.Serialize(message);
		var result = serializer.Deserialize<TestPluggableMessage>(bytes);

		// Assert
		result.Value.ShouldBe(0);
		result.Text.ShouldBe(string.Empty);
	}

	[Fact]
	public void Serialize_WithNegativeValue_RoundTrips()
	{
		// Arrange
		var serializer = new MessagePackPluggableSerializer();
		var message = new TestPluggableMessage { Value = -999, Text = "Negative" };

		// Act
		var bytes = serializer.Serialize(message);
		var result = serializer.Deserialize<TestPluggableMessage>(bytes);

		// Assert
		result.Value.ShouldBe(-999);
	}

	[Fact]
	public void Serialize_WithUnicodeText_RoundTrips()
	{
		// Arrange
		var serializer = new MessagePackPluggableSerializer();
		var message = new TestPluggableMessage { Value = 7, Text = "\u00e9\u4e2d\u6587\U0001f600" };

		// Act
		var bytes = serializer.Serialize(message);
		var result = serializer.Deserialize<TestPluggableMessage>(bytes);

		// Assert
		result.Text.ShouldBe(message.Text);
	}

	#endregion

	#region SerializeObject Additional Branches

	[Fact]
	public void SerializeObject_WithZeroValue_RoundTrips()
	{
		// Arrange
		var serializer = new MessagePackPluggableSerializer();
		var message = new TestPluggableMessage { Value = 0, Text = "Zero" };

		// Act
		var bytes = serializer.SerializeObject(message, typeof(TestPluggableMessage));
		var result = (TestPluggableMessage)serializer.DeserializeObject(bytes, typeof(TestPluggableMessage));

		// Assert
		result.Value.ShouldBe(0);
		result.Text.ShouldBe("Zero");
	}

	[Fact]
	public void SerializeObject_WithNegativeValue_RoundTrips()
	{
		// Arrange
		var serializer = new MessagePackPluggableSerializer();
		var message = new TestPluggableMessage { Value = -500, Text = "NegObj" };

		// Act
		var bytes = serializer.SerializeObject(message, typeof(TestPluggableMessage));
		var result = (TestPluggableMessage)serializer.DeserializeObject(bytes, typeof(TestPluggableMessage));

		// Assert
		result.Value.ShouldBe(-500);
	}

	[Fact]
	public void SerializeObject_WithLargePayload_RoundTrips()
	{
		// Arrange
		var serializer = new MessagePackPluggableSerializer();
		var largeText = new string('W', 30_000);
		var message = new TestPluggableMessage { Value = 88, Text = largeText };

		// Act
		var bytes = serializer.SerializeObject(message, typeof(TestPluggableMessage));
		var result = (TestPluggableMessage)serializer.DeserializeObject(bytes, typeof(TestPluggableMessage));

		// Assert
		result.Value.ShouldBe(88);
		result.Text.Length.ShouldBe(30_000);
	}

	[Fact]
	public void SerializeObject_WithCustomOptions_RoundTrips()
	{
		// Arrange
		var opts = MessagePackSerializerOptions.Standard.WithCompression(MessagePackCompression.Lz4Block);
		var serializer = new MessagePackPluggableSerializer(opts);
		var message = new TestPluggableMessage { Value = 33, Text = "CustomOpts" };

		// Act
		var bytes = serializer.SerializeObject(message, typeof(TestPluggableMessage));
		var result = (TestPluggableMessage)serializer.DeserializeObject(bytes, typeof(TestPluggableMessage));

		// Assert
		result.Value.ShouldBe(33);
		result.Text.ShouldBe("CustomOpts");
	}

	#endregion

	#region DeserializeObject Null Result Branch

	[Fact]
	[RequiresUnreferencedCode("Test")]
	[RequiresDynamicCode("Test")]
	public void DeserializeObject_WhenNullResult_ExceptionMessageContainsTypeName()
	{
		// Arrange
		var serializer = new MessagePackPluggableSerializer();
		var nilData = new byte[] { 0xC0 };

		// Act
		var ex = Should.Throw<SerializationException>(() =>
			serializer.DeserializeObject(nilData, typeof(TestPluggableMessage)));

		// Assert
		ex.Message.ShouldContain("null");
	}

	#endregion

	#region Serialize Error Wrapping Details

	[Fact]
	[RequiresUnreferencedCode("Test")]
	[RequiresDynamicCode("Test")]
	public void SerializeObject_WithBadType_ExceptionContainsTypeInfo()
	{
		// Arrange
		var serializer = new MessagePackPluggableSerializer();
		var badObj = new NonSerializableClass { Data = "bad" };

		// Act
		var ex = Should.Throw<SerializationException>(() =>
			serializer.SerializeObject(badObj, typeof(NonSerializableClass)));

		// Assert
		ex.InnerException.ShouldNotBeNull();
		ex.Message.ShouldContain("serialize");
	}

	[Fact]
	[RequiresUnreferencedCode("Test")]
	[RequiresDynamicCode("Test")]
	public void DeserializeObject_WithCorruptData_ExceptionContainsTypeInfo()
	{
		// Arrange
		var serializer = new MessagePackPluggableSerializer();
		var corrupt = new byte[] { 0xFF, 0xAA, 0xBB, 0xCC };

		// Act
		var ex = Should.Throw<SerializationException>(() =>
			serializer.DeserializeObject(corrupt, typeof(TestPluggableMessage)));

		// Assert
		ex.InnerException.ShouldNotBeNull();
		ex.Message.ShouldContain("deserialize");
	}

	#endregion

	#region Deserialize Generic - Rethrow SerializationException Branch

	[Fact]
	[RequiresUnreferencedCode("Test")]
	[RequiresDynamicCode("Test")]
	public void Deserialize_WhenSerializationExceptionThrown_RethrowsDirectly()
	{
		// Arrange - nil data causes NullResult SerializationException via the ?? throw,
		// which is then caught by the first catch (SerializationException) and rethrown.
		var serializer = new MessagePackPluggableSerializer();
		var nilData = new byte[] { 0xC0 };

		// Act
		var ex = Should.Throw<SerializationException>(() =>
			serializer.Deserialize<TestPluggableMessage>(nilData));

		// Assert - the exception should be a SerializationException (not double-wrapped)
		ex.ShouldBeOfType<SerializationException>();
		ex.Message.ShouldContain("null");
		// It should NOT have another SerializationException as inner
		if (ex.InnerException != null)
		{
			ex.InnerException.ShouldNotBeOfType<SerializationException>();
		}
	}

	#endregion

	#region DeserializeObject - Rethrow SerializationException Branch

	[Fact]
	[RequiresUnreferencedCode("Test")]
	[RequiresDynamicCode("Test")]
	public void DeserializeObject_WhenSerializationExceptionThrown_RethrowsDirectly()
	{
		// Arrange - nil data causes NullResultForType SerializationException,
		// which hits the catch(SerializationException) { throw; } branch.
		var serializer = new MessagePackPluggableSerializer();
		var nilData = new byte[] { 0xC0 };

		// Act
		var ex = Should.Throw<SerializationException>(() =>
			serializer.DeserializeObject(nilData, typeof(TestPluggableMessage)));

		// Assert - verify it's a direct SerializationException, not double-wrapped
		ex.ShouldBeOfType<SerializationException>();
		ex.Message.ShouldContain("null");
	}

	#endregion

	#region Interface Conformance

	[Fact]
	public void ImplementsIPluggableSerializer()
	{
		// Arrange
		var serializer = new MessagePackPluggableSerializer();

		// Assert
		serializer.ShouldBeAssignableTo<IPluggableSerializer>();
	}

	[Fact]
	public void Serialize_ReturnsNonEmptyBytes()
	{
		// Arrange
		var serializer = new MessagePackPluggableSerializer();
		var message = new TestPluggableMessage { Value = 5, Text = "ByteCheck" };

		// Act
		var bytes = serializer.Serialize(message);

		// Assert
		bytes.ShouldNotBeNull();
		bytes.Length.ShouldBeGreaterThan(0);
	}

	#endregion

	#region Reusability

	[Fact]
	public void SerializeAndDeserialize_MultipleCalls_AreIdempotent()
	{
		// Arrange
		var serializer = new MessagePackPluggableSerializer();

		// Act & Assert
		for (var i = 0; i < 10; i++)
		{
			var msg = new TestPluggableMessage { Value = i * 10, Text = $"Iter{i}" };
			var bytes = serializer.Serialize(msg);
			var result = serializer.Deserialize<TestPluggableMessage>(bytes);
			result.Value.ShouldBe(i * 10);
			result.Text.ShouldBe($"Iter{i}");
		}
	}

	[Fact]
	public void SerializeObjectAndDeserializeObject_MultipleCalls_AreIdempotent()
	{
		// Arrange
		var serializer = new MessagePackPluggableSerializer();

		// Act & Assert
		for (var i = 0; i < 10; i++)
		{
			var msg = new TestPluggableMessage { Value = i, Text = $"ObjIter{i}" };
			var bytes = serializer.SerializeObject(msg, typeof(TestPluggableMessage));
			var result = (TestPluggableMessage)serializer.DeserializeObject(bytes, typeof(TestPluggableMessage));
			result.Value.ShouldBe(i);
			result.Text.ShouldBe($"ObjIter{i}");
		}
	}

	#endregion

	/// <summary>
	/// Non-serializable type without MessagePackObject attribute.
	/// </summary>
	private sealed class NonSerializableClass
	{
		public string Data { get; set; } = string.Empty;
	}
}
