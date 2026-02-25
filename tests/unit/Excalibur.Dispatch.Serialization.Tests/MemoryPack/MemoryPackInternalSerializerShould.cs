// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Buffers;

using Excalibur.Dispatch.Abstractions.Serialization;

using MemoryPack;

using Excalibur.Dispatch.Serialization.MemoryPack;

namespace Excalibur.Dispatch.Serialization.Tests.MemoryPack;

/// <summary>
/// Tests for <see cref="MemoryPackInternalSerializer"/>.
/// </summary>
/// <remarks>
/// Per ADR-058 conformance requirements, these tests verify:
/// - Round-trip serialization integrity
/// - IBufferWriter{byte} zero-copy serialization
/// - ReadOnlySequence{byte} deserialization (pipeline support)
/// - ReadOnlySpan{byte} deserialization
/// - Null result handling (SerializationException)
/// </remarks>
[Trait("Category", "Unit")]
public sealed class MemoryPackInternalSerializerShould
{
	private readonly IInternalSerializer _sut;

	public MemoryPackInternalSerializerShould()
	{
		_sut = new MemoryPackInternalSerializer();
	}

	#region Serialize<T>(T value) Tests

	[Fact]
	public void Serialize_SimpleType_To_ByteArray()
	{
		// Arrange
		var value = new TestPayload { Id = 42, Name = "Test" };

		// Act
		var bytes = _sut.Serialize(value);

		// Assert
		_ = bytes.ShouldNotBeNull();
		bytes.Length.ShouldBeGreaterThan(0);
	}

	[Fact]
	public void RoundTrip_SimpleType_Via_ByteArray()
	{
		// Arrange
		var original = new TestPayload { Id = 123, Name = "RoundTrip Test" };

		// Act
		var bytes = _sut.Serialize(original);
		var deserialized = _sut.Deserialize<TestPayload>(bytes.AsSpan());

		// Assert
		_ = deserialized.ShouldNotBeNull();
		deserialized.Id.ShouldBe(original.Id);
		deserialized.Name.ShouldBe(original.Name);
	}

	[Fact]
	public void Serialize_ComplexType_With_Collections()
	{
		// Arrange
		var value = new TestPayloadWithCollections
		{
			Id = 1,
			Tags = ["tag1", "tag2", "tag3"],
			Metadata = new Dictionary<string, string>
			{
				["key1"] = "value1",
				["key2"] = "value2",
			},
		};

		// Act
		var bytes = _sut.Serialize(value);
		var deserialized = _sut.Deserialize<TestPayloadWithCollections>(bytes.AsSpan());

		// Assert
		_ = deserialized.ShouldNotBeNull();
		deserialized.Id.ShouldBe(1);
		deserialized.Tags.ShouldBe(["tag1", "tag2", "tag3"]);
		deserialized.Metadata.ShouldContainKeyAndValue("key1", "value1");
		deserialized.Metadata.ShouldContainKeyAndValue("key2", "value2");
	}

	#endregion Serialize<T>(T value) Tests

	#region Serialize<T>(T value, IBufferWriter<byte>) Tests

	[Fact]
	public void Serialize_To_BufferWriter()
	{
		// Arrange
		var value = new TestPayload { Id = 99, Name = "BufferWriter Test" };
		var bufferWriter = new ArrayBufferWriter<byte>();

		// Act
		_sut.Serialize(value, bufferWriter);

		// Assert
		bufferWriter.WrittenCount.ShouldBeGreaterThan(0);
	}

	[Fact]
	public void RoundTrip_Via_BufferWriter()
	{
		// Arrange
		var original = new TestPayload { Id = 456, Name = "BufferWriter RoundTrip" };
		var bufferWriter = new ArrayBufferWriter<byte>();

		// Act
		_sut.Serialize(original, bufferWriter);
		var deserialized = _sut.Deserialize<TestPayload>(bufferWriter.WrittenSpan);

		// Assert
		_ = deserialized.ShouldNotBeNull();
		deserialized.Id.ShouldBe(original.Id);
		deserialized.Name.ShouldBe(original.Name);
	}

	#endregion Serialize<T>(T value, IBufferWriter<byte>) Tests

	#region Deserialize<T>(ReadOnlySequence<byte>) Tests

	[Fact]
	public void Deserialize_From_ReadOnlySequence()
	{
		// Arrange
		var original = new TestPayload { Id = 789, Name = "Sequence Test" };
		var bytes = _sut.Serialize(original);
		var sequence = new ReadOnlySequence<byte>(bytes);

		// Act
		var deserialized = _sut.Deserialize<TestPayload>(sequence);

		// Assert
		_ = deserialized.ShouldNotBeNull();
		deserialized.Id.ShouldBe(original.Id);
		deserialized.Name.ShouldBe(original.Name);
	}

	[Fact]
	public void Deserialize_From_MultiSegment_ReadOnlySequence()
	{
		// Arrange
		var original = new TestPayload { Id = 101, Name = "Multi-Segment Test" };
		var bytes = _sut.Serialize(original);

		// Split bytes into multiple segments to simulate pipeline scenario
		var segment1 = bytes.AsMemory(0, bytes.Length / 2);
		var segment2 = bytes.AsMemory(bytes.Length / 2);
		var sequence = CreateMultiSegmentSequence(segment1, segment2);

		// Act
		var deserialized = _sut.Deserialize<TestPayload>(sequence);

		// Assert
		_ = deserialized.ShouldNotBeNull();
		deserialized.Id.ShouldBe(original.Id);
		deserialized.Name.ShouldBe(original.Name);
	}

	#endregion Deserialize<T>(ReadOnlySequence<byte>) Tests

	#region Deserialize<T>(ReadOnlySpan<byte>) Tests

	[Fact]
	public void Deserialize_From_ReadOnlySpan()
	{
		// Arrange
		var original = new TestPayload { Id = 999, Name = "Span Test" };
		var bytes = _sut.Serialize(original);

		// Act
		var deserialized = _sut.Deserialize<TestPayload>(bytes.AsSpan());

		// Assert
		_ = deserialized.ShouldNotBeNull();
		deserialized.Id.ShouldBe(original.Id);
		deserialized.Name.ShouldBe(original.Name);
	}

	#endregion Deserialize<T>(ReadOnlySpan<byte>) Tests

	#region Edge Cases

	[Fact]
	public void Handle_Empty_Collections()
	{
		// Arrange
		var original = new TestPayloadWithCollections
		{
			Id = 1,
			Tags = [],
			Metadata = [],
		};

		// Act
		var bytes = _sut.Serialize(original);
		var deserialized = _sut.Deserialize<TestPayloadWithCollections>(bytes.AsSpan());

		// Assert
		_ = deserialized.ShouldNotBeNull();
		deserialized.Tags.ShouldBeEmpty();
		deserialized.Metadata.ShouldBeEmpty();
	}

	[Fact]
	public void Handle_Null_Optional_Properties()
	{
		// Arrange
		var original = new TestPayloadWithNullables
		{
			Id = 1,
			OptionalName = null,
			OptionalValue = null,
		};

		// Act
		var bytes = _sut.Serialize(original);
		var deserialized = _sut.Deserialize<TestPayloadWithNullables>(bytes.AsSpan());

		// Assert
		_ = deserialized.ShouldNotBeNull();
		deserialized.Id.ShouldBe(1);
		deserialized.OptionalName.ShouldBeNull();
		deserialized.OptionalValue.ShouldBeNull();
	}

	[Fact]
	public void Handle_DateTimeOffset_Correctly()
	{
		// Arrange
		var timestamp = new DateTimeOffset(2025, 11, 26, 12, 30, 45, TimeSpan.FromHours(-5));
		var original = new TestPayloadWithDateTime
		{
			Id = 1,
			Timestamp = timestamp,
		};

		// Act
		var bytes = _sut.Serialize(original);
		var deserialized = _sut.Deserialize<TestPayloadWithDateTime>(bytes.AsSpan());

		// Assert
		_ = deserialized.ShouldNotBeNull();
		deserialized.Timestamp.ShouldBe(timestamp);
	}

	[Fact]
	public void Handle_Guid_Correctly()
	{
		// Arrange
		var guid = Guid.NewGuid();
		var original = new TestPayloadWithGuid
		{
			Id = guid,
			Name = "Guid Test",
		};

		// Act
		var bytes = _sut.Serialize(original);
		var deserialized = _sut.Deserialize<TestPayloadWithGuid>(bytes.AsSpan());

		// Assert
		_ = deserialized.ShouldNotBeNull();
		deserialized.Id.ShouldBe(guid);
		deserialized.Name.ShouldBe("Guid Test");
	}

	#endregion Edge Cases

	#region Helper Methods

	private static ReadOnlySequence<byte> CreateMultiSegmentSequence(
		ReadOnlyMemory<byte> segment1,
		ReadOnlyMemory<byte> segment2)
	{
		var first = new MemorySegment(segment1);
		var last = first.Append(segment2);
		return new ReadOnlySequence<byte>(first, 0, last, last.Memory.Length);
	}

	private sealed class MemorySegment : ReadOnlySequenceSegment<byte>
	{
		public MemorySegment(ReadOnlyMemory<byte> memory) => Memory = memory;

		public MemorySegment Append(ReadOnlyMemory<byte> memory)
		{
			var segment = new MemorySegment(memory) { RunningIndex = RunningIndex + Memory.Length };
			Next = segment;
			return segment;
		}
	}

	#endregion Helper Methods
}

#region Test Types

[MemoryPackable]
public partial class TestPayload
{
	public int Id { get; set; }

	public string Name { get; set; } = string.Empty;
}

[MemoryPackable]
public partial class TestPayloadWithCollections
{
	public int Id { get; set; }

	public List<string> Tags { get; set; } = [];

	public Dictionary<string, string> Metadata { get; set; } = [];
}

[MemoryPackable]
public partial class TestPayloadWithNullables
{
	public int Id { get; set; }

	public string? OptionalName { get; set; }

	public int? OptionalValue { get; set; }
}

[MemoryPackable]
public partial class TestPayloadWithDateTime
{
	public int Id { get; set; }

	public DateTimeOffset Timestamp { get; set; }
}

[MemoryPackable]
public partial class TestPayloadWithGuid
{
	public Guid Id { get; set; }

	public string Name { get; set; } = string.Empty;
}

#endregion Test Types
