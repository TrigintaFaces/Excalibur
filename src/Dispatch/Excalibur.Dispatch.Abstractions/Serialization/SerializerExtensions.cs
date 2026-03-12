// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Buffers;
using System.Text;

namespace Excalibur.Dispatch.Abstractions.Serialization;

/// <summary>
/// Convenience extension methods for <see cref="ISerializer"/> providing overloads
/// for common buffer types (byte[], Stream, ReadOnlyMemory, ReadOnlySequence, string).
/// PipeWriter/PipeReader overloads are in SerializerPipeExtensions (Excalibur.Dispatch package).
/// </summary>
/// <remarks>
/// <para>
/// Follows the System.Text.Json pattern: core interface has minimal methods
/// (IBufferWriter + ReadOnlySpan), and this class provides convenience overloads
/// that delegate to the core methods.
/// </para>
/// </remarks>
public static class SerializerExtensions
{
	/// <summary>
	/// Serializes a value to a new byte array.
	/// </summary>
	/// <typeparam name="T">The type to serialize.</typeparam>
	/// <param name="serializer">The serializer.</param>
	/// <param name="value">The value to serialize.</param>
	/// <returns>A byte array containing the serialized data.</returns>
	public static byte[] SerializeToBytes<T>(this ISerializer serializer, T value)
	{
		ArgumentNullException.ThrowIfNull(serializer);

		var bufferWriter = new ArrayBufferWriter<byte>();
		serializer.Serialize(value, bufferWriter);
		return bufferWriter.WrittenSpan.ToArray();
	}

	/// <summary>
	/// Deserializes a value from a byte array.
	/// </summary>
	/// <typeparam name="T">The type to deserialize to.</typeparam>
	/// <param name="serializer">The serializer.</param>
	/// <param name="data">The byte array containing serialized data.</param>
	/// <returns>The deserialized value.</returns>
	public static T Deserialize<T>(this ISerializer serializer, byte[] data)
	{
		ArgumentNullException.ThrowIfNull(serializer);
		ArgumentNullException.ThrowIfNull(data);

		return serializer.Deserialize<T>(data.AsSpan());
	}

	/// <summary>
	/// Deserializes a value from a <see cref="ReadOnlyMemory{T}"/>.
	/// </summary>
	/// <typeparam name="T">The type to deserialize to.</typeparam>
	/// <param name="serializer">The serializer.</param>
	/// <param name="data">The memory containing serialized data.</param>
	/// <returns>The deserialized value.</returns>
	public static T Deserialize<T>(this ISerializer serializer, ReadOnlyMemory<byte> data)
	{
		ArgumentNullException.ThrowIfNull(serializer);

		return serializer.Deserialize<T>(data.Span);
	}

	/// <summary>
	/// Deserializes a value from a <see cref="ReadOnlySequence{T}"/>.
	/// </summary>
	/// <typeparam name="T">The type to deserialize to.</typeparam>
	/// <param name="serializer">The serializer.</param>
	/// <param name="data">The sequence containing serialized data.</param>
	/// <returns>The deserialized value.</returns>
	public static T Deserialize<T>(this ISerializer serializer, ReadOnlySequence<byte> data)
	{
		ArgumentNullException.ThrowIfNull(serializer);

		if (data.IsSingleSegment)
		{
			return serializer.Deserialize<T>(data.FirstSpan);
		}

		// Multi-segment: copy to contiguous buffer
		var buffer = data.Length <= 256
			? stackalloc byte[(int)data.Length]
			: new byte[data.Length];
		data.CopyTo(buffer);
		return serializer.Deserialize<T>(buffer);
	}

	/// <summary>
	/// Serializes a value to a <see cref="Stream"/> asynchronously.
	/// </summary>
	/// <typeparam name="T">The type to serialize.</typeparam>
	/// <param name="serializer">The serializer.</param>
	/// <param name="stream">The stream to write to.</param>
	/// <param name="value">The value to serialize.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>A task representing the asynchronous operation.</returns>
	public static async ValueTask SerializeAsync<T>(
		this ISerializer serializer,
		Stream stream,
		T value,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(serializer);
		ArgumentNullException.ThrowIfNull(stream);

		cancellationToken.ThrowIfCancellationRequested();

		var bufferWriter = new ArrayBufferWriter<byte>();
		serializer.Serialize(value, bufferWriter);
		await stream.WriteAsync(bufferWriter.WrittenMemory, cancellationToken).ConfigureAwait(false);
	}

	/// <summary>
	/// Deserializes a value from a <see cref="Stream"/> asynchronously.
	/// </summary>
	/// <typeparam name="T">The type to deserialize to.</typeparam>
	/// <param name="serializer">The serializer.</param>
	/// <param name="stream">The stream to read from.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>The deserialized value.</returns>
	public static async ValueTask<T> DeserializeAsync<T>(
		this ISerializer serializer,
		Stream stream,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(serializer);
		ArgumentNullException.ThrowIfNull(stream);

		cancellationToken.ThrowIfCancellationRequested();

		using var memoryStream = new MemoryStream();
		await stream.CopyToAsync(memoryStream, cancellationToken).ConfigureAwait(false);

		if (!memoryStream.TryGetBuffer(out var segment))
		{
			segment = new ArraySegment<byte>(memoryStream.ToArray());
		}

		return serializer.Deserialize<T>(segment.AsSpan());
	}

	/// <summary>
	/// Serializes a value to a UTF-8 encoded string.
	/// </summary>
	/// <typeparam name="T">The type to serialize.</typeparam>
	/// <param name="serializer">The serializer.</param>
	/// <param name="value">The value to serialize.</param>
	/// <returns>A UTF-8 string representation of the serialized data.</returns>
	/// <remarks>
	/// This is primarily useful for human-readable serializers (JSON).
	/// For binary serializers, the result will be a Base64-like encoding of the raw bytes.
	/// </remarks>
	public static string SerializeToString<T>(this ISerializer serializer, T value)
	{
		ArgumentNullException.ThrowIfNull(serializer);

		var bufferWriter = new ArrayBufferWriter<byte>();
		serializer.Serialize(value, bufferWriter);
		return Encoding.UTF8.GetString(bufferWriter.WrittenSpan);
	}

	/// <summary>
	/// Deserializes a value from a UTF-8 encoded string.
	/// </summary>
	/// <typeparam name="T">The type to deserialize to.</typeparam>
	/// <param name="serializer">The serializer.</param>
	/// <param name="data">The UTF-8 string to deserialize.</param>
	/// <returns>The deserialized value.</returns>
	/// <remarks>
	/// This is primarily useful for human-readable serializers (JSON).
	/// </remarks>
	public static T DeserializeFromString<T>(this ISerializer serializer, string data)
	{
		ArgumentNullException.ThrowIfNull(serializer);
		ArgumentNullException.ThrowIfNull(data);

		var bytes = Encoding.UTF8.GetBytes(data);
		return serializer.Deserialize<T>(bytes.AsSpan());
	}
}
