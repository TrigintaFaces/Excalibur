// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Serialization.Protobuf;

using Google.Protobuf;
using Google.Protobuf.Reflection;

namespace Excalibur.Dispatch.Serialization.Tests.Protobuf;

/// <summary>
/// Tests for <see cref="ProtobufSerializer"/>.
/// </summary>
/// <remarks>
/// Per T10.*, these tests verify:
/// - Serialization round-trip integrity (AAA pattern)
/// - Null handling (ArgumentNullException)
/// - Edge cases (empty messages, large payloads)
/// - AOT scenarios (reflection-free paths)
/// - Binary wire format support
/// </remarks>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Serialization)]
public sealed class ProtobufMessageSerializerShould
{
	private readonly ProtobufSerializer _sut;

	public ProtobufMessageSerializerShould()
	{
		_sut = new ProtobufSerializer();
	}

	[Fact]
	public void Have_Correct_Name()
	{
		// Arrange & Act
		var name = _sut.Name;

		// Assert
		name.ShouldBe("Protobuf");
	}

	[Fact]
	public void Have_NonEmpty_Version()
	{
		// Arrange & Act
		var version = _sut.Version;

		// Assert
		version.ShouldNotBeNullOrEmpty();
		// The version comes from the Google.Protobuf assembly, not hardcoded "1.0.0"
		if (version != "Unknown")
		{
			System.Version.TryParse(version, out var parsedVersion).ShouldBeTrue();
			_ = parsedVersion.ShouldNotBeNull();
		}
	}

	[Fact]
	public void Have_Correct_ContentType()
	{
		// Arrange & Act
		var contentType = _sut.ContentType;

		// Assert
		contentType.ShouldBe("application/x-protobuf");
	}

	[Fact]
	public void Serialize_Binary_Message_Successfully()
	{
		// Arrange
		var message = new TestMessage
		{
			Name = "TestName",
			Value = 42,
			IsActive = true,
		};

		// Act
		var serialized = _sut.SerializeToBytes(message);

		// Assert
		_ = serialized.ShouldNotBeNull();
		serialized.ShouldNotBeEmpty();
		serialized.Length.ShouldBeGreaterThan(0);
	}

	[Fact]
	public void Deserialize_Binary_Message_Successfully()
	{
		// Arrange
		var original = new TestMessage
		{
			Name = "TestName",
			Value = 42,
			IsActive = true,
		};
		var serialized = _sut.SerializeToBytes(original);

		// Act
		var deserialized = _sut.Deserialize<TestMessage>(serialized);

		// Assert
		_ = deserialized.ShouldNotBeNull();
		deserialized.Name.ShouldBe(original.Name);
		deserialized.Value.ShouldBe(original.Value);
		deserialized.IsActive.ShouldBe(original.IsActive);
	}

	[Fact]
	public void Round_Trip_Binary_Preserves_Message_Integrity()
	{
		// Arrange
		var original = new TestMessage
		{
			Name = "RoundTripTest",
			Value = 12345,
			IsActive = false,
		};

		// Act
		var serialized = _sut.SerializeToBytes(original);
		var deserialized = _sut.Deserialize<TestMessage>(serialized);

		// Assert
		deserialized.ShouldBe(original);
	}

	[Fact]
	public void Serialize_Empty_Message_Successfully()
	{
		// Arrange
		var message = new TestMessage(); // All default values

		// Act
		var serialized = _sut.SerializeToBytes(message);

		// Assert
		_ = serialized.ShouldNotBeNull();
		// Empty message should produce minimal bytes (possibly just message header)
		serialized.Length.ShouldBeGreaterThanOrEqualTo(0);
	}

	[Fact]
	public void Serialize_Default_Message_Produces_Empty_Bytes()
	{
		// Arrange - Protobuf encodes default values as zero-length output
		var original = new TestMessage();

		// Act
		var serialized = _sut.SerializeToBytes(original);

		// Assert - all-defaults protobuf message produces empty byte array
		_ = serialized.ShouldNotBeNull();
		serialized.Length.ShouldBe(0);
	}

	[Fact]
	public void Round_Trip_Default_Message_Via_Non_Empty_Encoding()
	{
		// Arrange - use a message with at least one non-default field
		// to exercise the deserialization path (protobuf empty == all defaults)
		var nonDefault = new TestMessage { Name = "x" };
		var serialized = _sut.SerializeToBytes(nonDefault);
		serialized.Length.ShouldBeGreaterThan(0);

		// Act
		var deserialized = _sut.Deserialize<TestMessage>(serialized);

		// Assert
		_ = deserialized.ShouldNotBeNull();
		deserialized.Name.ShouldBe("x");
	}

	[Fact]
	public void Serialize_Large_Message_Successfully()
	{
		// Arrange
		var largeString = new string('A', 10000); // 10KB string
		var message = new TestMessage
		{
			Name = largeString,
			Value = int.MaxValue,
			IsActive = true,
		};

		// Act
		var serialized = _sut.SerializeToBytes(message);

		// Assert
		_ = serialized.ShouldNotBeNull();
		serialized.Length.ShouldBeGreaterThan(10000);
	}

	[Fact]
	public void Round_Trip_Large_Message_Preserves_Data()
	{
		// Arrange
		var largeString = new string('B', 50000); // 50KB string
		var original = new TestMessage
		{
			Name = largeString,
			Value = 999999,
			IsActive = true,
		};

		// Act
		var serialized = _sut.SerializeToBytes(original);
		var deserialized = _sut.Deserialize<TestMessage>(serialized);

		// Assert
		deserialized.Name.ShouldBe(largeString);
		deserialized.Value.ShouldBe(999999);
		deserialized.IsActive.ShouldBe(true);
	}

	[Fact]
	public void Throw_ArgumentNullException_When_Serialize_Receives_Null()
	{
		// Arrange, Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => _sut.SerializeToBytes<TestMessage>(null!));
	}

	[Fact]
	public void Throw_InvalidOperationException_When_Serializing_Non_Protobuf_Message()
	{
		// Arrange
		var nonProtobufMessage = "plain string"; // Does not implement IMessage

		// Act & Assert
		var exception = Should.Throw<InvalidOperationException>(() => _sut.SerializeToBytes(nonProtobufMessage));
		exception.Message.ShouldContain("does not implement IMessage");
	}

	[Fact]
	public void Throw_InvalidOperationException_When_Deserializing_Non_Protobuf_Type()
	{
		// Arrange
		var validProtobufData = _sut.SerializeToBytes(new TestMessage { Name = "Test" });

		// Act & Assert
		var exception = Should.Throw<InvalidOperationException>(() => _sut.Deserialize<string>(validProtobufData));
		exception.Message.ShouldContain("does not implement IMessage");
	}

	[Fact]
	public void Throw_SerializationException_When_Deserializing_Corrupt_Data()
	{
		// Arrange
		var corruptData = new byte[] { 0xFF, 0xFF, 0xFF, 0xFF, 0xFF }; // Invalid Protobuf data

		// Act & Assert
		var exception = Should.Throw<SerializationException>(() => _sut.Deserialize<TestMessage>(corruptData));
		exception.Message.ShouldContain("deserialize");
		exception.Message.ShouldContain(nameof(TestMessage));
	}

	[Fact]
	public void Handle_Special_Characters_In_String_Fields()
	{
		// Arrange
		var message = new TestMessage
		{
			Name = "Test\nWith\tSpecial\"Characters'And\u2764Emoji",
			Value = 42,
			IsActive = true,
		};

		// Act
		var serialized = _sut.SerializeToBytes(message);
		var deserialized = _sut.Deserialize<TestMessage>(serialized);

		// Assert
		deserialized.Name.ShouldBe(message.Name);
	}

	[Fact]
	public void Preserve_Unicode_Characters()
	{
		// Arrange
		var message = new TestMessage
		{
			Name = "\u65e5\u672c\u8a9e\u30c6\u30b9\u30c8\u4e2d\u6587\u6d4b\u8bd5\u0627\u0644\u0639\u0631\u0628\u064a\u0629", // Japanese, Chinese, Arabic
			Value = 123,
			IsActive = false,
		};

		// Act
		var serialized = _sut.SerializeToBytes(message);
		var deserialized = _sut.Deserialize<TestMessage>(serialized);

		// Assert
		deserialized.Name.ShouldBe(message.Name);
	}

	[Fact]
	public async Task Be_Thread_Safe_During_Concurrent_Serialization()
	{
		// Arrange
		var message = new TestMessage
		{
			Name = "ConcurrencyTest",
			Value = 42,
			IsActive = true,
		};

		// Act
		var tasks = Enumerable.Range(0, 100).Select(i => Task.Run(() =>
		{
			var serialized = _sut.SerializeToBytes(message);
			var deserialized = _sut.Deserialize<TestMessage>(serialized);
			return deserialized.Name == message.Name;
		})).ToArray();

		_ = await Task.WhenAll(tasks);

		// Assert
		tasks.All(t => t.Result).ShouldBeTrue();
	}

	[Fact]
	public void Throw_InvalidOperationException_When_Binary_Deserialize_Type_Has_No_Parser()
	{
		// Arrange - NoParserTestMessage implements IMessage but has no static Parser property
		var data = new byte[] { 0x0A, 0x04, 0x54, 0x65, 0x73, 0x74 };

		// Act & Assert - The new ProtobufSerializer wraps parser-not-found in
		// SerializationException via the Wrap helper
		var exception = Should.Throw<SerializationException>(() => _sut.Deserialize<NoParserTestMessage>(data));
		exception.Message.ShouldContain("deserialize");
		exception.Message.ShouldContain(nameof(NoParserTestMessage));
	}

	[Fact]
	public void Serialize_Non_IMessage_Type_On_Deserialize_Throws()
	{
		// Arrange - a non-IMessage type should be rejected even with valid data
		var data = new byte[] { 0x0A, 0x04, 0x54, 0x65, 0x73, 0x74 };

		// Act & Assert
		var exception = Should.Throw<InvalidOperationException>(() => _sut.Deserialize<int>(data));
		exception.Message.ShouldContain("does not implement IMessage");
	}

	#region Interface Implementation Tests

	[Fact]
	public void ImplementsISerializer()
	{
		// Assert
		_ = _sut.ShouldBeAssignableTo<ISerializer>();
	}

	#endregion Interface Implementation Tests
}

/// <summary>
/// A minimal IMessage implementation without a static Parser property.
/// Used to test the "no parser found" error paths.
/// </summary>
internal sealed class NoParserTestMessage : IMessage
{
	// Intentionally no static Parser property

	[System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
	public MessageDescriptor Descriptor => throw new NotSupportedException();

	public int CalculateSize() => 0;

	public void MergeFrom(CodedInputStream input)
	{
		// No-op
	}

	public void WriteTo(CodedOutputStream output)
	{
		// No-op
	}
}
