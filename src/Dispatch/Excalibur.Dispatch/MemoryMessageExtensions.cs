// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Buffers;
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Serialization;

namespace Excalibur.Dispatch.Messaging;

/// <summary>
/// Extension methods for creating memory messages.
/// </summary>
public static class MemoryMessageExtensions
{
	private const int MaxCacheEntries = 1024;
	private const int DefaultSerializedBufferSize = 1024;
	private const int MaxSerializedBufferHint = 256 * 1024;
	private static readonly ConcurrentDictionary<Type, int> s_serializedBufferSizeHints = new();
	[ThreadStatic]
	private static FixedCapacityBufferWriter? s_bufferWriter;

	/// <summary>
	/// Creates a memory message from a byte array.
	/// </summary>
	/// <param name="data"> The message data. </param>
	/// <param name="contentType"> The content type. </param>
	/// <returns> A new memory message. </returns>
	public static MemoryMessage FromBytes(byte[] data, string contentType = "application/octet-stream")
	{
		ArgumentNullException.ThrowIfNull(data);
		return new MemoryMessage(data.AsMemory(), contentType);
	}

	/// <summary>
	/// Creates a memory message from a pooled buffer.
	/// </summary>
	/// <param name="memoryPool"> The memory pool to rent from. </param>
	/// <param name="data"> The data to copy into the pooled buffer. </param>
	/// <param name="contentType"> The content type. </param>
	/// <returns> A new memory message with pooled memory. </returns>
	public static MemoryMessage FromPooledBuffer(
		MemoryPool<byte> memoryPool,
		ReadOnlySpan<byte> data,
		string contentType = "application/octet-stream")
	{
		ArgumentNullException.ThrowIfNull(memoryPool);

		var owner = memoryPool.Rent(data.Length);
		data.CopyTo(owner.Memory.Span);

		return new MemoryMessage(owner, data.Length, contentType);
	}

	/// <summary>
	/// Creates a typed memory message from an object.
	/// </summary>
	/// <typeparam name="T"> The type of the content. </typeparam>
	/// <param name="content"> The content to serialize. </param>
	/// <param name="serializer"> The serializer to use. </param>
	/// <param name="memoryPool"> The memory pool to use for the buffer. </param>
	/// <returns> A new typed memory message. </returns>
	[RequiresUnreferencedCode("JSON serialization for generic types requires preserved members.")]
	[RequiresDynamicCode("JSON serialization for generic types requires dynamic code generation.")]
	public static MemoryMessageOfT<T> FromContent<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T>(
			T content,
			DispatchJsonSerializer serializer,
			MemoryPool<byte> memoryPool)
	where T : class
	{
		ArgumentNullException.ThrowIfNull(content);
		ArgumentNullException.ThrowIfNull(serializer);
		ArgumentNullException.ThrowIfNull(memoryPool);

		if (TrySerializeToPooledOwner(content, typeof(T), serializer, memoryPool, out var owner, out var payloadLength))
		{
			return new MemoryMessageOfT<T>(content, owner, payloadLength, "application/json");
		}

		// Fallback for payloads that exceed pooled-buffer capacity.
		var tempBytes = serializer.SerializeToUtf8Bytes(content, typeof(T));
		UpdateSerializedBufferSizeHint(typeof(T), tempBytes.Length);
		return new MemoryMessageOfT<T>(content, new ArrayBackedMemoryOwner(tempBytes), tempBytes.Length, "application/json");
	}

	/// <summary>
	/// Converts a regular dispatch message to a memory message.
	/// </summary>
	/// <param name="message"> The message to convert. </param>
	/// <param name="serializer"> The serializer to use. </param>
	/// <param name="memoryPool"> The memory pool to use. </param>
	/// <returns> A new memory message. </returns>
	public static MemoryMessage ToMemoryMessage(
		this IDispatchMessage message,
		DispatchJsonSerializer serializer,
		MemoryPool<byte> memoryPool)
	{
		ArgumentNullException.ThrowIfNull(message);
		ArgumentNullException.ThrowIfNull(serializer);
		ArgumentNullException.ThrowIfNull(memoryPool);

		var messageType = message.GetType();
		if (TrySerializeToPooledOwner(message, messageType, serializer, memoryPool, out var owner, out var payloadLength))
		{
			return new MemoryMessage(owner, payloadLength, "application/json");
		}

		var bytes = serializer.SerializeToUtf8Bytes(message, messageType);
		UpdateSerializedBufferSizeHint(messageType, bytes.Length);
		return new MemoryMessage(new ArrayBackedMemoryOwner(bytes), bytes.Length, "application/json");
	}

	private static bool TrySerializeToPooledOwner(
		object? value,
		Type type,
		DispatchJsonSerializer serializer,
		MemoryPool<byte> memoryPool,
		out IMemoryOwner<byte> owner,
		out int payloadLength)
	{
		var bufferSizeHint = GetSerializedBufferSizeHint(type);
		if (TrySerializeToPooledOwner(
			value,
			type,
			serializer,
			memoryPool,
			bufferSizeHint,
			out owner,
			out payloadLength))
		{
			return true;
		}

		// Retry with a larger bounded buffer to avoid repeated byte[] fallback on recurring large message types.
		var retrySize = bufferSizeHint >= (MaxSerializedBufferHint / 8)
			? MaxSerializedBufferHint
			: Math.Min(MaxSerializedBufferHint, bufferSizeHint * 8);

		if (retrySize > bufferSizeHint &&
			TrySerializeToPooledOwner(
				value,
				type,
				serializer,
				memoryPool,
				retrySize,
				out owner,
				out payloadLength))
		{
			UpdateSerializedBufferSizeHint(type, payloadLength);
			return true;
		}

		owner = null!;
		payloadLength = 0;
		return false;
	}

	private static bool TrySerializeToPooledOwner(
		object? value,
		Type type,
		DispatchJsonSerializer serializer,
		MemoryPool<byte> memoryPool,
		int bufferSize,
		out IMemoryOwner<byte> owner,
		out int payloadLength)
	{
		var rented = memoryPool.Rent(bufferSize);
		var writer = s_bufferWriter ??= new FixedCapacityBufferWriter();
		writer.Reset(rented.Memory);

		try
		{
			serializer.SerializeToUtf8(writer, value, type);
			owner = rented;
			payloadLength = writer.WrittenCount;
			writer.Clear();
			return true;
		}
		catch (InsufficientMemoryException)
		{
			writer.Clear();
			rented.Dispose();
			owner = null!;
			payloadLength = 0;
			return false;
		}
		catch
		{
			writer.Clear();
			rented.Dispose();
			throw;
		}
	}

	private static int GetSerializedBufferSizeHint(Type type)
	{
		if (s_serializedBufferSizeHints.TryGetValue(type, out var hint))
		{
			return hint;
		}

		return DefaultSerializedBufferSize;
	}

	private static void UpdateSerializedBufferSizeHint(Type type, int serializedLength)
	{
		if (serializedLength <= DefaultSerializedBufferSize)
		{
			return;
		}

		var suggested = RoundUpToPowerOfTwo(serializedLength);
		if (suggested > MaxSerializedBufferHint)
		{
			suggested = MaxSerializedBufferHint;
		}

		while (true)
		{
			if (!s_serializedBufferSizeHints.TryGetValue(type, out var current))
			{
				// Bounded cache: skip caching when full to prevent unbounded memory growth
				if (s_serializedBufferSizeHints.Count >= MaxCacheEntries)
				{
					return;
				}

				if (s_serializedBufferSizeHints.TryAdd(type, suggested))
				{
					return;
				}

				continue;
			}

			if (current >= suggested)
			{
				return;
			}

			if (s_serializedBufferSizeHints.TryUpdate(type, suggested, current))
			{
				return;
			}
		}
	}

	private static int RoundUpToPowerOfTwo(int value)
	{
		var rounded = DefaultSerializedBufferSize;
		while (rounded < value && rounded < MaxSerializedBufferHint)
		{
			rounded <<= 1;
		}

		return rounded;
	}

	private sealed class ArrayBackedMemoryOwner(byte[] buffer) : IMemoryOwner<byte>
	{
		private byte[]? _buffer = buffer ?? throw new ArgumentNullException(nameof(buffer));

		public Memory<byte> Memory => _buffer ?? Memory<byte>.Empty;

		public void Dispose()
		{
			_buffer = null;
		}
	}

	private sealed class FixedCapacityBufferWriter : IBufferWriter<byte>
	{
		private Memory<byte> _buffer;

		public int WrittenCount { get; private set; }

		public void Reset(Memory<byte> buffer)
		{
			_buffer = buffer;
			WrittenCount = 0;
		}

		public void Clear()
		{
			_buffer = Memory<byte>.Empty;
			WrittenCount = 0;
		}

		public void Advance(int count)
		{
			if ((uint)count > (uint)(_buffer.Length - WrittenCount))
			{
				throw new ArgumentOutOfRangeException(nameof(count));
			}

			WrittenCount += count;
		}

		public Memory<byte> GetMemory(int sizeHint = 0)
		{
			EnsureCapacity(sizeHint);
			return _buffer.Slice(WrittenCount);
		}

		public Span<byte> GetSpan(int sizeHint = 0)
		{
			EnsureCapacity(sizeHint);
			return _buffer.Span.Slice(WrittenCount);
		}

		private void EnsureCapacity(int sizeHint)
		{
			ArgumentOutOfRangeException.ThrowIfNegative(sizeHint);

			if (sizeHint == 0)
			{
				sizeHint = 1;
			}

			if (_buffer.Length - WrittenCount < sizeHint)
			{
				throw new InsufficientMemoryException();
			}
		}
	}
}
