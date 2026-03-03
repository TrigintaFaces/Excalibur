// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Buffers;
using System.Text;
using System.Text.Json;

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Serialization;
using Excalibur.Dispatch.Messaging;

namespace Excalibur.Dispatch.Tests.Messaging;

[Trait("Category", "Unit")]
[Trait("Component", "Messaging")]
[Trait("Priority", "0")]
public sealed class MemoryMessageExtensionsShould
{
	[Fact]
	public void FromPooledBuffer_UsesExactPayloadLength()
	{
		using var pool = new OversizedMemoryPool(extraBytes: 64);
		var payload = Encoding.UTF8.GetBytes("payload-123");

		using var message = MemoryMessageExtensions.FromPooledBuffer(pool, payload, "application/octet-stream");

		message.OwnsMemory.ShouldBeTrue();
		message.Body.Length.ShouldBe(payload.Length);
		message.Body.Span.SequenceEqual(payload).ShouldBeTrue();
	}

	[Fact]
	public void FromContent_UsesExactSerializedLengthAndOwnedMemory()
	{
		var serializer = new RuntimeUtf8JsonSerializer();
		using var pool = new OversizedMemoryPool(extraBytes: 64);
		var content = new TestPayload { Id = 42, Name = "alpha" };
		var expected = serializer.SerializeToUtf8Bytes(content, typeof(TestPayload));

		using var message = MemoryMessageExtensions.FromContent(content, serializer, pool);

		message.OwnsMemory.ShouldBeTrue();
		message.IsDeserialized.ShouldBeTrue();
		ReferenceEquals(content, message.Content).ShouldBeTrue();
		message.Body.Length.ShouldBe(expected.Length);
		message.Body.Span.SequenceEqual(expected).ShouldBeTrue();
	}

	[Fact]
	public void ToMemoryMessage_UsesExactSerializedLengthAndOwnedMemory()
	{
		var serializer = new RuntimeUtf8JsonSerializer();
		using var pool = new OversizedMemoryPool(extraBytes: 64);
		var dispatchMessage = new TestDispatchMessage { Value = "hello", Count = 7 };
		var expected = serializer.SerializeToUtf8Bytes(dispatchMessage, dispatchMessage.GetType());

		using var memoryMessage = dispatchMessage.ToMemoryMessage(serializer, pool);

		memoryMessage.OwnsMemory.ShouldBeTrue();
		memoryMessage.Body.Length.ShouldBe(expected.Length);
		memoryMessage.Body.Span.SequenceEqual(expected).ShouldBeTrue();
	}

	[Fact]
	public void FromContent_WithPayloadLargerThanPooledBuffer_FallsBackWithoutTruncation()
	{
		var serializer = new RuntimeUtf8JsonSerializer();
		using var pool = new OversizedMemoryPool(extraBytes: 64);
		var content = new TestPayload { Id = 11, Name = new string('z', 4000) };
		var expected = serializer.SerializeToUtf8Bytes(content, typeof(TestPayload));

		using var message = MemoryMessageExtensions.FromContent(content, serializer, pool);

		message.OwnsMemory.ShouldBeTrue();
		message.Body.Length.ShouldBe(expected.Length);
		message.Body.Span.SequenceEqual(expected).ShouldBeTrue();
	}

	[Fact]
	public void ToMemoryMessage_WithPayloadLargerThanPooledBuffer_FallsBackWithoutTruncation()
	{
		var serializer = new RuntimeUtf8JsonSerializer();
		using var pool = new OversizedMemoryPool(extraBytes: 64);
		var dispatchMessage = new TestDispatchMessage { Value = new string('x', 4000), Count = 99 };
		var expected = serializer.SerializeToUtf8Bytes(dispatchMessage, dispatchMessage.GetType());

		using var memoryMessage = dispatchMessage.ToMemoryMessage(serializer, pool);

		memoryMessage.OwnsMemory.ShouldBeTrue();
		memoryMessage.Body.Length.ShouldBe(expected.Length);
		memoryMessage.Body.Span.SequenceEqual(expected).ShouldBeTrue();
	}

	[Fact]
	public void ToMemoryMessage_Should_UseAdaptiveBufferHint_ForRepeatedLargePayloadType()
	{
		var serializer = new CountingUtf8JsonSerializer();
		using var pool = new OversizedMemoryPool(extraBytes: 64);
		var dispatchMessage = new AdaptiveHintDispatchMessage { Value = new string('x', 5000), Count = 17 };

		using var first = dispatchMessage.ToMemoryMessage(serializer, pool);
		using var second = dispatchMessage.ToMemoryMessage(serializer, pool);

		serializer.SerializeToUtf8BytesCallCount.ShouldBeLessThanOrEqualTo(1);
		second.Body.Length.ShouldBe(first.Body.Length);
	}

	private sealed class TestPayload
	{
		public int Id { get; init; }
		public string Name { get; init; } = string.Empty;
	}

	private sealed class TestDispatchMessage : IDispatchMessage
	{
		public string Value { get; init; } = string.Empty;
		public int Count { get; init; }
	}

	private sealed class AdaptiveHintDispatchMessage : IDispatchMessage
	{
		public string Value { get; init; } = string.Empty;
		public int Count { get; init; }
	}

	private sealed class RuntimeUtf8JsonSerializer : IUtf8JsonSerializer
	{
		[ThreadStatic]
		private static Utf8JsonWriter? jsonWriterCache;

		public string Serialize(object value, Type type) => JsonSerializer.Serialize(value, type);

		public object? Deserialize(string json, Type type) => JsonSerializer.Deserialize(json, type);

		public byte[] SerializeToUtf8Bytes(object? value, Type type) => JsonSerializer.SerializeToUtf8Bytes(value, type);

		public void SerializeToUtf8(IBufferWriter<byte> writer, object? value, Type type)
		{
			var jsonWriter = jsonWriterCache;
			if (jsonWriter is null)
			{
				jsonWriter = new Utf8JsonWriter(writer);
				jsonWriterCache = jsonWriter;
			}
			else
			{
				jsonWriter.Reset(writer);
			}

			JsonSerializer.Serialize(jsonWriter, value, type);
			jsonWriter.Flush();
		}

		public object? DeserializeFromUtf8(ReadOnlySpan<byte> utf8Json, Type type) => JsonSerializer.Deserialize(utf8Json, type);

		public object? DeserializeFromUtf8(ReadOnlyMemory<byte> utf8Json, Type type) => JsonSerializer.Deserialize(utf8Json.Span, type);
	}

	private sealed class CountingUtf8JsonSerializer : IUtf8JsonSerializer
	{
		public int SerializeToUtf8BytesCallCount { get; private set; }

		[ThreadStatic]
		private static Utf8JsonWriter? jsonWriterCache;

		public string Serialize(object value, Type type) => JsonSerializer.Serialize(value, type);

		public object? Deserialize(string json, Type type) => JsonSerializer.Deserialize(json, type);

		public byte[] SerializeToUtf8Bytes(object? value, Type type)
		{
			SerializeToUtf8BytesCallCount++;
			return JsonSerializer.SerializeToUtf8Bytes(value, type);
		}

		public void SerializeToUtf8(IBufferWriter<byte> writer, object? value, Type type)
		{
			var jsonWriter = jsonWriterCache;
			if (jsonWriter is null)
			{
				jsonWriter = new Utf8JsonWriter(writer);
				jsonWriterCache = jsonWriter;
			}
			else
			{
				jsonWriter.Reset(writer);
			}

			JsonSerializer.Serialize(jsonWriter, value, type);
			jsonWriter.Flush();
		}

		public object? DeserializeFromUtf8(ReadOnlySpan<byte> utf8Json, Type type) => JsonSerializer.Deserialize(utf8Json, type);

		public object? DeserializeFromUtf8(ReadOnlyMemory<byte> utf8Json, Type type) => JsonSerializer.Deserialize(utf8Json.Span, type);
	}

	private sealed class OversizedMemoryPool(int extraBytes) : MemoryPool<byte>
	{
		public override int MaxBufferSize => int.MaxValue;

		public override IMemoryOwner<byte> Rent(int minBufferSize = -1)
		{
			var requestedSize = minBufferSize <= 0 ? 1 : minBufferSize;
			return new OversizedMemoryOwner(new byte[requestedSize + extraBytes]);
		}

		protected override void Dispose(bool disposing)
		{
		}

		private sealed class OversizedMemoryOwner(byte[] buffer) : IMemoryOwner<byte>
		{
			private byte[]? _buffer = buffer;

			public Memory<byte> Memory => _buffer ?? Memory<byte>.Empty;

			public void Dispose()
			{
				_buffer = null;
			}
		}
	}
}
