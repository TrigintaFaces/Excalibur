// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Buffers;
using System.Buffers.Binary;
using System.IO.Pipelines;
using System.Runtime.CompilerServices;

using Excalibur.Dispatch.Abstractions;

using MessagePack;
using MessagePack.Resolvers;

namespace Excalibur.Dispatch.Serialization.MessagePack;

/// <summary>
/// High-performance MessagePack serializer with zero-copy support.
/// </summary>
public sealed class MessagePackZeroCopySerializer : IZeroCopySerializer
{
	private readonly MessagePackSerializerOptions _options;

	/// <summary>
	/// Initializes a new instance of the <see cref="MessagePackZeroCopySerializer" /> class.
	/// </summary>
	public MessagePackZeroCopySerializer()
	{
		// Configure MessagePack for maximum performance
		_options = MessagePackSerializerOptions.Standard
			.WithResolver(ContractlessStandardResolver.Instance)
			.WithCompression(MessagePackCompression.Lz4BlockArray);

	}

	/// <inheritdoc />
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public async ValueTask SerializeAsync<T>(
		PipeWriter writer,
		T message,
		CancellationToken cancellationToken)
	{
		try
		{
			// Serialize directly to PipeWriter (which implements IBufferWriter<byte>)
			// avoiding intermediate ArrayBufferWriter allocation and copy
			MessagePackSerializer.Serialize(
				writer,
				message,
				_options,
				cancellationToken);

			// Flush to make data available
			var result = await writer.FlushAsync(cancellationToken).ConfigureAwait(false);

			if (result.IsCanceled)
			{
				throw new OperationCanceledException();
			}
		}
		catch (Exception ex)
		{
			// Complete writer with error
			await writer.CompleteAsync(ex).ConfigureAwait(false);
			throw;
		}
	}

	/// <inheritdoc />
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public async ValueTask<T> DeserializeAsync<T>(
		PipeReader reader,
		CancellationToken cancellationToken)
	{
		try
		{
			while (true)
			{
				// Read from pipe
				var result = await reader.ReadAsync(cancellationToken).ConfigureAwait(false);
				var buffer = result.Buffer;

				// Try to deserialize from buffer
				if (TryDeserialize<T>(ref buffer, out var message, out var consumed))
				{
					// Tell pipe how much we consumed
					reader.AdvanceTo(consumed, buffer.End);
					return message;
				}

				// Not enough data, wait for more
				reader.AdvanceTo(buffer.Start, buffer.End);

				// Check if completed
				if (result.IsCompleted)
				{
					throw new EndOfStreamException(
							ErrorMessages.PipeCompletedBeforeMessageDeserialized);
				}
			}
		}
		catch (Exception ex)
		{
			// Complete reader with error
			await reader.CompleteAsync(ex).ConfigureAwait(false);
			throw;
		}
	}

	/// <inheritdoc />
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public int Serialize<T>(T message, Memory<byte> buffer)
	{
		var writer = new MemoryBufferWriter(buffer);
		MessagePackSerializer.Serialize(writer, message, _options);
		return writer.WrittenCount;
	}

	/// <inheritdoc />
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public T Deserialize<T>(ReadOnlyMemory<byte> buffer) => MessagePackSerializer.Deserialize<T>(buffer, _options);

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private bool TryDeserialize<T>(
		ref ReadOnlySequence<byte> buffer,
		out T message,
		out SequencePosition consumed)
	{
		message = default!;
		consumed = buffer.Start;

		// Check if we have enough data for a message
		if (buffer.Length < sizeof(int))
		{
			return false;
		}

		// Read message length prefix without allocation
		var lengthSpan = buffer.Slice(0, sizeof(int));
		Span<byte> lengthBytes = stackalloc byte[sizeof(int)];
		lengthSpan.CopyTo(lengthBytes);
		var messageLength = BinaryPrimitives.ReadInt32LittleEndian(lengthBytes);

		// Check if we have the full message
		if (buffer.Length < sizeof(int) + messageLength)
		{
			return false;
		}

		// Deserialize the message
		var messageBuffer = buffer.Slice(sizeof(int), messageLength);
		message = MessagePackSerializer.Deserialize<T>(messageBuffer, _options);

		// Calculate consumed position
		consumed = buffer.GetPosition(sizeof(int) + messageLength);
		return true;
	}

	/// <summary>
	/// Memory buffer writer for zero-allocation serialization.
	/// </summary>
	[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
	private struct MemoryBufferWriter(Memory<byte> buffer) : IBufferWriter<byte>
	{
		public int WrittenCount { get; private set; }

		public void Advance(int count) => WrittenCount += count;

		public readonly Memory<byte> GetMemory(int sizeHint = 0) => buffer.Slice(WrittenCount);

		public readonly Span<byte> GetSpan(int sizeHint = 0) => buffer.Span.Slice(WrittenCount);
	}
}
