// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR
// AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Buffers;
using System.Collections.Concurrent;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Excalibur.Dispatch.Serialization;

/// <summary>
/// Zero-copy serializer for high-performance message serialization.
/// </summary>
public sealed class ZeroCopySerializer(ArrayPool<byte>? bufferPool = null)
{
	private readonly ArrayPool<byte> _bufferPool = bufferPool ?? ArrayPool<byte>.Shared;

	/// <summary>
	/// Serializes a message directly to the provided span.
	/// </summary>
	/// <exception cref="ArgumentException"> </exception>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static void SerializeTo<T>(ref T message, Span<byte> destination)
		where T : unmanaged
	{
		var messageSize = Unsafe.SizeOf<T>();
		var totalSize = Unsafe.SizeOf<MessageHeader>() + messageSize;

		if (destination.Length < totalSize)
		{
			throw new ArgumentException(
				string.Format(
					CultureInfo.CurrentCulture,
					Resources.ZeroCopySerializer_DestinationBufferTooSmallFormat,
					totalSize,
					destination.Length));
		}

		// Write header
		var header = new MessageHeader
		{
			Magic = MessageHeader.MagicValue,
			Version = 1,
			TypeId = GetTypeId<T>(),
			PayloadSize = messageSize,
			Timestamp = DateTimeOffset.UtcNow.Ticks,
			Checksum = 0, // Will be calculated after
		};

		MemoryMarshal.Write(destination, in header);

		// Write message
		var messageDestination = destination.Slice(Unsafe.SizeOf<MessageHeader>());
		MemoryMarshal.Write(messageDestination, in message);

		// Calculate and update checksum
		var checksum = CalculateChecksum(destination.Slice(0, totalSize));
		MemoryMarshal.Cast<byte, MessageHeader>(destination)[0].Checksum = checksum;
	}

	/// <summary>
	/// Deserializes a message from a buffer.
	/// </summary>
	/// <exception cref="ArgumentException"> </exception>
	/// <exception cref="InvalidOperationException"> </exception>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static T Deserialize<T>(ReadOnlySpan<byte> source)
		where T : unmanaged
	{
		var headerSize = Unsafe.SizeOf<MessageHeader>();
		if (source.Length < headerSize)
		{
			throw new ArgumentException(
				Resources.ZeroCopySerializer_SourceBufferTooSmallForHeader);
		}

		// Read and validate header
		var header = MemoryMarshal.Read<MessageHeader>(source);

		if (header.Magic != MessageHeader.MagicValue)
		{
			throw new InvalidOperationException(
				Resources.ZeroCopySerializer_InvalidMessageMagicValue);
		}

		if (header.TypeId != GetTypeId<T>())
		{
			throw new InvalidOperationException(
				string.Format(
					CultureInfo.CurrentCulture,
					Resources.ZeroCopySerializer_TypeMismatchFormat,
					GetTypeId<T>(),
					header.TypeId));
		}

		var totalSize = headerSize + header.PayloadSize;
		if (source.Length < totalSize)
		{
			throw new ArgumentException(
				Resources.ZeroCopySerializer_SourceBufferTooSmallForPayload);
		}

		// Validate checksum
		var checksum = CalculateChecksum(source.Slice(0, totalSize));
		if (checksum != header.Checksum)
		{
			throw new InvalidOperationException(
				Resources.ZeroCopySerializer_ChecksumValidationFailed);
		}

		// Read message
		var messageSource = source.Slice(headerSize);
		return MemoryMarshal.Read<T>(messageSource);
	}

	/// <summary>
	/// Serializes a message directly to a buffer.
	/// </summary>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public IMemoryOwner<byte> Serialize<T>(ref T message)
		where T : unmanaged
	{
		var messageSize = Unsafe.SizeOf<T>();
		var totalSize = Unsafe.SizeOf<MessageHeader>() + messageSize;

		var buffer = _bufferPool.Rent(totalSize);
		var memory = buffer.AsMemory(0, totalSize);

		SerializeTo(ref message, memory.Span);

		return new OwnedBuffer(_bufferPool, buffer, totalSize);
	}

	/// <summary>
	/// Tries to deserialize a message, returning false on failure.
	/// </summary>
	public bool TryDeserialize<T>(ReadOnlySpan<byte> source, out T message)
		where T : unmanaged
	{
		message = default;

		try
		{
			message = Deserialize<T>(source);
			return true;
		}
		catch
		{
			return false;
		}
	}

	private static readonly ConcurrentDictionary<Type, uint> TypeIdCache = new();

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static uint GetTypeId<T>()
		where T : unmanaged
	{
		return TypeIdCache.GetOrAdd(typeof(T), static type =>
		{
			// Use deterministic FNV-1a hash of FullName instead of Type.GetHashCode(),
			// which is not stable across AppDomains or process restarts.
			var name = type.FullName ?? type.Name;
			unchecked
			{
				var hash = 2166136261u;
				foreach (var c in name)
				{
					hash = (hash ^ c) * 16777619;
				}

				return hash;
			}
		});
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static uint CalculateChecksum(ReadOnlySpan<byte> data)
	{
		uint checksum = 0;
		var uints = MemoryMarshal.Cast<byte, uint>(data);

		foreach (var value in uints)
		{
			checksum ^= value;
			checksum = (checksum << 1) | (checksum >> 31);
		}

		// Handle remaining bytes
		var remaining = data.Length & 3;
		if (remaining > 0)
		{
			uint lastValue = 0;
			for (var i = data.Length - remaining; i < data.Length; i++)
			{
				lastValue = (lastValue << 8) | data[i];
			}

			checksum ^= lastValue;
		}

		return checksum;
	}

	private sealed class OwnedBuffer(ArrayPool<byte> pool, byte[] buffer, int length) : IMemoryOwner<byte>
	{
		private byte[]? _buffer = buffer;

		public Memory<byte> Memory => _buffer?.AsMemory(0, length) ?? Memory<byte>.Empty;

		public void Dispose()
		{
			var buffer = _buffer;
			if (buffer != null)
			{
				_buffer = null;
				pool.Return(buffer);
			}
		}
	}
}
