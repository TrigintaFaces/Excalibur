// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Buffers;
using System.Diagnostics.CodeAnalysis;

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Serialization;

namespace Excalibur.Dispatch.Messaging.Tests.Messaging;

[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait("Component", "Dispatch.Core")]
[SuppressMessage("Trimming", "IL2026:RequiresUnreferencedCode", Justification = "Test code")]
[SuppressMessage("AOT", "IL3050:RequiresDynamicCode", Justification = "Test code")]
public sealed class MemoryMessageExtensionsShould
{
	[Fact]
	public void FromPooledBuffer_ReturnsOwnedMessage()
	{
		using var pool = new OversizedMemoryPool(extraBytes: 64);
		var payload = new byte[] { 1, 2, 3, 4, 5 };

		using var message = MemoryMessageExtensions.FromPooledBuffer(pool, payload, "application/octet-stream");

		message.OwnsMemory.ShouldBeTrue();
		message.Body.Length.ShouldBe(payload.Length);
		message.Body.Span.SequenceEqual(payload).ShouldBeTrue();
	}

	[Fact]
	public void FromContent_UsesExactSerializedLengthAndOwnedMemory()
	{
		var serializer = new DispatchJsonSerializer();
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
		var serializer = new DispatchJsonSerializer();
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
		var serializer = new DispatchJsonSerializer();
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
		var serializer = new DispatchJsonSerializer();
		using var pool = new OversizedMemoryPool(extraBytes: 64);
		var dispatchMessage = new TestDispatchMessage { Value = new string('x', 4000), Count = 99 };
		var expected = serializer.SerializeToUtf8Bytes(dispatchMessage, dispatchMessage.GetType());

		using var memoryMessage = dispatchMessage.ToMemoryMessage(serializer, pool);

		memoryMessage.OwnsMemory.ShouldBeTrue();
		memoryMessage.Body.Length.ShouldBe(expected.Length);
		memoryMessage.Body.Span.SequenceEqual(expected).ShouldBeTrue();
	}

	[Fact]
	public void ToMemoryMessage_Should_ProduceConsistentOutput_ForRepeatedLargePayloadType()
	{
		var serializer = new DispatchJsonSerializer();
		using var pool = new OversizedMemoryPool(extraBytes: 64);
		var dispatchMessage = new AdaptiveHintDispatchMessage { Value = new string('x', 5000), Count = 17 };

		using var first = dispatchMessage.ToMemoryMessage(serializer, pool);
		using var second = dispatchMessage.ToMemoryMessage(serializer, pool);

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

	private sealed class OversizedMemoryPool(int extraBytes) : MemoryPool<byte>
	{
		public override int MaxBufferSize => int.MaxValue;

		public override IMemoryOwner<byte> Rent(int minBufferSize = -1)
		{
			var requestedSize = minBufferSize <= 0 ? 1 : minBufferSize;
			var buffer = new byte[requestedSize + extraBytes];
			return new ArrayMemoryOwner(buffer);
		}

		protected override void Dispose(bool disposing) { }

		private sealed class ArrayMemoryOwner(byte[] buffer) : IMemoryOwner<byte>
		{
			public Memory<byte> Memory => buffer;
			public void Dispose() { }
		}
	}
}
