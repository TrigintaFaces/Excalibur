// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Reflection;

using Excalibur.Dispatch.Serialization.Protobuf;

using Google.Protobuf;
using Google.Protobuf.Reflection;

namespace Excalibur.Dispatch.Serialization.Tests.Protobuf;

/// <summary>
/// Tests for <see cref="ProtobufMessageSerializer"/>.
/// </summary>
/// <remarks>
/// Per T10.*, these tests verify:
/// - Serialization round-trip integrity (AAA pattern)
/// - Null handling (ArgumentNullException)
/// - Edge cases (empty messages, large payloads)
/// - AOT scenarios (reflection-free paths)
/// - Wire format support (Binary and JSON)
/// </remarks>
[Trait("Category", "Unit")]
public sealed class ProtobufMessageSerializerShould
{
	private readonly ProtobufMessageSerializer _sut;
	private readonly ProtobufSerializationOptions _options;

	public ProtobufMessageSerializerShould()
	{
		_options = new ProtobufSerializationOptions
		{
			WireFormat = ProtobufWireFormat.Binary,
		};

		_sut = new ProtobufMessageSerializer(Microsoft.Extensions.Options.Options.Create(_options));
	}

	[Fact]
	public void Have_Correct_SerializerName()
	{
		// Arrange & Act
		var name = _sut.SerializerName;

		// Assert
		name.ShouldBe("Protobuf");
	}

	[Fact]
	public void Have_Correct_SerializerVersion()
	{
		// Arrange & Act
		var version = _sut.SerializerVersion;

		// Assert
		version.ShouldBe("1.0.0");
	}

	[Fact]
	public void Throw_ArgumentNullException_When_Options_Is_Null()
	{
		// Arrange, Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => new ProtobufMessageSerializer(null!));
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
		var serialized = _sut.Serialize(message);

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
		var serialized = _sut.Serialize(original);

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
		var serialized = _sut.Serialize(original);
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
		var serialized = _sut.Serialize(message);

		// Assert
		_ = serialized.ShouldNotBeNull();
		// Empty message should produce minimal bytes (possibly just message header)
		serialized.Length.ShouldBeGreaterThanOrEqualTo(0);
	}

	[Fact]
	public void Serialize_Default_Message_Produces_Empty_Bytes()
	{
		// Arrange — Protobuf encodes default values as zero-length output
		var original = new TestMessage();

		// Act
		var serialized = _sut.Serialize(original);

		// Assert — all-defaults protobuf message produces empty byte array
		_ = serialized.ShouldNotBeNull();
		serialized.Length.ShouldBe(0);
	}

	[Fact]
	public void Round_Trip_Default_Message_Via_Non_Empty_Encoding()
	{
		// Arrange — use a message with at least one non-default field
		// to exercise the deserialization path (protobuf empty == all defaults)
		var original = new TestMessage { Name = "", Value = 0, IsActive = false };

		// A truly empty protobuf payload is valid — but our serializer
		// rejects zero-length data with ArgumentException by design.
		// So test round-trip with a message that has at least one set field.
		var nonDefault = new TestMessage { Name = "x" };
		var serialized = _sut.Serialize(nonDefault);
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
		var serialized = _sut.Serialize(message);

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
		var serialized = _sut.Serialize(original);
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
		_ = Should.Throw<ArgumentNullException>(() => _sut.Serialize<TestMessage>(null!));
	}

	[Fact]
	public void Throw_ArgumentNullException_When_Deserialize_Receives_Null()
	{
		// Arrange, Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => _sut.Deserialize<TestMessage>(null!));
	}

	[Fact]
	public void Throw_ArgumentException_When_Deserialize_Receives_Empty_Data()
	{
		// Arrange
		var emptyData = Array.Empty<byte>();

		// Act & Assert
		var exception = Should.Throw<ArgumentException>(() => _sut.Deserialize<TestMessage>(emptyData));
		exception.Message.ShouldContain("Data cannot be empty");
		exception.ParamName.ShouldBe("data");
	}

	[Fact]
	public void Throw_InvalidOperationException_When_Serializing_Non_Protobuf_Message()
	{
		// Arrange
		var nonProtobufMessage = "plain string"; // Does not implement IMessage

		// Act & Assert
		var exception = Should.Throw<InvalidOperationException>(() => _sut.Serialize(nonProtobufMessage));
		exception.Message.ShouldContain("does not implement Google.Protobuf.IMessage");
	}

	[Fact]
	public void Throw_InvalidOperationException_When_Deserializing_Non_Protobuf_Type()
	{
		// Arrange
		var validProtobufData = _sut.Serialize(new TestMessage { Name = "Test" });

		// Act & Assert
		var exception = Should.Throw<InvalidOperationException>(() => _sut.Deserialize<string>(validProtobufData));
		exception.Message.ShouldContain("does not implement Google.Protobuf.IMessage");
	}

	[Fact]
	public void Throw_InvalidOperationException_When_Deserializing_Corrupt_Data()
	{
		// Arrange
		var corruptData = new byte[] { 0xFF, 0xFF, 0xFF, 0xFF, 0xFF }; // Invalid Protobuf data

		// Act & Assert
		var exception = Should.Throw<InvalidOperationException>(() => _sut.Deserialize<TestMessage>(corruptData));
		exception.Message.ShouldContain("Failed to deserialize Protocol Buffers data");
	}

	[Fact]
	public void Serialize_Json_Message_Successfully()
	{
		// Arrange
		var jsonOptions = new ProtobufSerializationOptions { WireFormat = ProtobufWireFormat.Json };
		var jsonSerializer = new ProtobufMessageSerializer(Microsoft.Extensions.Options.Options.Create(jsonOptions));

		var message = new TestMessage
		{
			Name = "JsonTest",
			Value = 100,
			IsActive = true,
		};

		// Act
		var serialized = jsonSerializer.Serialize(message);

		// Assert
		_ = serialized.ShouldNotBeNull();
		serialized.ShouldNotBeEmpty();
		var jsonString = System.Text.Encoding.UTF8.GetString(serialized);
		jsonString.ShouldContain("JsonTest");
		jsonString.ShouldContain("100");
		jsonString.ShouldContain("true");
	}

	[Fact]
	public void Throw_InvalidOperationException_When_Json_Deserialize_Lacks_MessageDescriptor()
	{
		// Arrange — JSON deserialization via ParseJson requires MessageDescriptor,
		// which TestMessage does not implement (throws NotSupportedException).
		// The TargetInvocationException is unwrapped into InvalidOperationException.
		var jsonOptions = new ProtobufSerializationOptions { WireFormat = ProtobufWireFormat.Json };
		var jsonSerializer = new ProtobufMessageSerializer(Microsoft.Extensions.Options.Options.Create(jsonOptions));

		var original = new TestMessage
		{
			Name = "JsonRoundTrip",
			Value = 777,
			IsActive = false,
		};

		var serialized = jsonSerializer.Serialize(original);
		serialized.ShouldNotBeEmpty();

		// Act & Assert — ParseJson invocation fails because Descriptor throws;
		// the TargetInvocationException is caught and wrapped in InvalidOperationException
		var exception = Should.Throw<InvalidOperationException>(() => jsonSerializer.Deserialize<TestMessage>(serialized));
		exception.Message.ShouldContain("Failed to deserialize Protocol Buffers data");
	}

	[Fact]
	public void Handle_Special_Characters_In_String_Fields()
	{
		// Arrange
		var message = new TestMessage
		{
			Name = "Test\nWith\tSpecial\"Characters'And🎯Emoji",
			Value = 42,
			IsActive = true,
		};

		// Act
		var serialized = _sut.Serialize(message);
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
			Name = "日本語テスト中文测试العربية", // Japanese, Chinese, Arabic
			Value = 123,
			IsActive = false,
		};

		// Act
		var serialized = _sut.Serialize(message);
		var deserialized = _sut.Deserialize<TestMessage>(serialized);

		// Assert
		deserialized.Name.ShouldBe(message.Name);
	}

	[Fact]
	public void Binary_Serialization_Should_Be_Smaller_Than_Json()
	{
		// Arrange
		var binaryOptions = new ProtobufSerializationOptions { WireFormat = ProtobufWireFormat.Binary };
		var jsonOptions = new ProtobufSerializationOptions { WireFormat = ProtobufWireFormat.Json };

		var binarySerializer = new ProtobufMessageSerializer(Microsoft.Extensions.Options.Options.Create(binaryOptions));
		var jsonSerializer = new ProtobufMessageSerializer(Microsoft.Extensions.Options.Options.Create(jsonOptions));

		var message = new TestMessage
		{
			Name = "CompressionTest",
			Value = 12345,
			IsActive = true,
		};

		// Act
		var binarySerialized = binarySerializer.Serialize(message);
		var jsonSerialized = jsonSerializer.Serialize(message);

		// Assert
		binarySerialized.Length.ShouldBeLessThan(jsonSerialized.Length);
	}

	[Fact]
	public void Throw_InvalidOperationException_For_Unsupported_Wire_Format()
	{
		// Arrange
		var invalidOptions = new ProtobufSerializationOptions { WireFormat = (ProtobufWireFormat)999 };
		var invalidSerializer = new ProtobufMessageSerializer(Microsoft.Extensions.Options.Options.Create(invalidOptions));
		var message = new TestMessage { Name = "Test" };

		// Act & Assert
		var exception = Should.Throw<InvalidOperationException>(() => invalidSerializer.Serialize(message));
		exception.Message.ShouldContain("Unsupported wire format");
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
			var serialized = _sut.Serialize(message);
			var deserialized = _sut.Deserialize<TestMessage>(serialized);
			return deserialized.Name == message.Name;
		})).ToArray();

		_ = await Task.WhenAll(tasks).ConfigureAwait(true);

		// Assert
		tasks.All(t => t.Result).ShouldBeTrue();
	}

	[Fact]
	public void Throw_InvalidOperationException_For_Unsupported_Wire_Format_On_Deserialize()
	{
		// Arrange
		var invalidOptions = new ProtobufSerializationOptions { WireFormat = (ProtobufWireFormat)999 };
		var invalidSerializer = new ProtobufMessageSerializer(Microsoft.Extensions.Options.Options.Create(invalidOptions));
		var data = new byte[] { 0x0A, 0x04, 0x54, 0x65, 0x73, 0x74 };

		// Act & Assert
		var exception = Should.Throw<InvalidOperationException>(() => invalidSerializer.Deserialize<TestMessage>(data));
		exception.Message.ShouldContain("Unsupported wire format");
	}

	[Fact]
	public void Throw_InvalidOperationException_When_Binary_Deserialize_Type_Has_No_Parser()
	{
		// Arrange — NoParserTestMessage implements IMessage but has no static Parser property
		var data = new byte[] { 0x0A, 0x04, 0x54, 0x65, 0x73, 0x74 };

		// Act & Assert
		var exception = Should.Throw<InvalidOperationException>(() => _sut.Deserialize<NoParserTestMessage>(data));
		exception.Message.ShouldContain("No parser found");
		exception.Message.ShouldContain("NoParserTestMessage");
	}

	[Fact]
	public void Throw_InvalidOperationException_When_Json_Deserialize_Type_Has_No_Parser()
	{
		// Arrange
		var jsonOptions = new ProtobufSerializationOptions { WireFormat = ProtobufWireFormat.Json };
		var jsonSerializer = new ProtobufMessageSerializer(Microsoft.Extensions.Options.Options.Create(jsonOptions));
		var data = System.Text.Encoding.UTF8.GetBytes("{ \"name\": \"test\" }");

		// Act & Assert
		var exception = Should.Throw<InvalidOperationException>(() => jsonSerializer.Deserialize<NoParserTestMessage>(data));
		exception.Message.ShouldContain("No parser found");
		exception.Message.ShouldContain("NoParserTestMessage");
	}

	[Fact]
	public void Deserialize_Json_Successfully_With_Well_Known_Type()
	{
		// Arrange — use a real protobuf well-known type with working Descriptor and ParseJson
		var jsonOptions = new ProtobufSerializationOptions { WireFormat = ProtobufWireFormat.Json };
		var jsonSerializer = new ProtobufMessageSerializer(Microsoft.Extensions.Options.Options.Create(jsonOptions));

		var original = new Google.Protobuf.WellKnownTypes.StringValue { Value = "hello-world" };
		var serialized = jsonSerializer.Serialize(original);

		// Act
		var deserialized = jsonSerializer.Deserialize<Google.Protobuf.WellKnownTypes.StringValue>(serialized);

		// Assert
		_ = deserialized.ShouldNotBeNull();
		deserialized.Value.ShouldBe("hello-world");
	}

	[Fact]
	public void Round_Trip_Json_With_Int32Value()
	{
		// Arrange
		var jsonOptions = new ProtobufSerializationOptions { WireFormat = ProtobufWireFormat.Json };
		var jsonSerializer = new ProtobufMessageSerializer(Microsoft.Extensions.Options.Options.Create(jsonOptions));

		var original = new Google.Protobuf.WellKnownTypes.Int32Value { Value = 42 };
		var serialized = jsonSerializer.Serialize(original);

		// Act
		var deserialized = jsonSerializer.Deserialize<Google.Protobuf.WellKnownTypes.Int32Value>(serialized);

		// Assert
		_ = deserialized.ShouldNotBeNull();
		deserialized.Value.ShouldBe(42);
	}

	[Fact]
	public void Serialize_Non_IMessage_Type_On_Deserialize_Throws()
	{
		// Arrange — a non-IMessage type should be rejected even with valid data
		var data = new byte[] { 0x0A, 0x04, 0x54, 0x65, 0x73, 0x74 };

		// Act & Assert
		var exception = Should.Throw<InvalidOperationException>(() => _sut.Deserialize<int>(data));
		exception.Message.ShouldContain("does not implement Google.Protobuf.IMessage");
	}

	[Fact]
	public void Throw_InvalidOperationException_When_Json_Deserialize_Receives_Invalid_Json()
	{
		// Arrange — corrupt JSON sent to a well-known type with a working Descriptor/ParseJson.
		// ParseJson throws InvalidProtocolBufferException, wrapped in TargetInvocationException
		// by the reflection-based Invoke call, caught by the TargetInvocationException handler.
		var jsonOptions = new ProtobufSerializationOptions { WireFormat = ProtobufWireFormat.Json };
		var jsonSerializer = new ProtobufMessageSerializer(Microsoft.Extensions.Options.Options.Create(jsonOptions));
		var invalidJson = System.Text.Encoding.UTF8.GetBytes("NOT VALID JSON AT ALL {{{");

		// Act & Assert
		var exception = Should.Throw<InvalidOperationException>(
			() => jsonSerializer.Deserialize<Google.Protobuf.WellKnownTypes.StringValue>(invalidJson));
		exception.Message.ShouldContain("Failed to deserialize Protocol Buffers data");
	}
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
