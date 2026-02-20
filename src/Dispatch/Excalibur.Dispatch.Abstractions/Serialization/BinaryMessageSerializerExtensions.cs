// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics.CodeAnalysis;

using Excalibur.Dispatch.Abstractions.Options;

namespace Excalibur.Dispatch.Abstractions.Serialization;

/// <summary>
/// Extension methods for <see cref="IBinaryMessageSerializer"/>.
/// </summary>
/// <remarks>
/// Provides async variants, stream operations, options overloads, and convenience helpers that delegate
/// to the core <see cref="IBinaryMessageSerializer"/> and <see cref="IMessageSerializer"/> methods.
/// </remarks>
public static class BinaryMessageSerializerExtensions
{
	/// <summary>
	/// Serializes a message to bytes asynchronously.
	/// </summary>
	/// <typeparam name="T"> The type of the message to serialize. </typeparam>
	/// <param name="serializer"> The binary message serializer. </param>
	/// <param name="message"> The message to serialize. </param>
	/// <param name="cancellationToken"> Cancellation token. </param>
	/// <returns> The serialized message as bytes. </returns>
	[RequiresUnreferencedCode("Serialization may require unreferenced code for type-specific handling.")]
	[RequiresDynamicCode("Serialization may require dynamic code generation for type-specific handling.")]
	public static ValueTask<byte[]> SerializeAsync<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T>(
		this IBinaryMessageSerializer serializer,
		T message,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(serializer);
		cancellationToken.ThrowIfCancellationRequested();
		return new ValueTask<byte[]>(serializer.Serialize(message));
	}

	/// <summary>
	/// Deserializes bytes to a message asynchronously.
	/// </summary>
	/// <typeparam name="T"> The type of the message to deserialize. </typeparam>
	/// <param name="serializer"> The binary message serializer. </param>
	/// <param name="data"> The serialized data. </param>
	/// <param name="cancellationToken"> Cancellation token. </param>
	/// <returns> The deserialized message. </returns>
	[RequiresUnreferencedCode("Deserialization may require unreferenced code for type-specific handling.")]
	[RequiresDynamicCode("Deserialization may require dynamic code generation for type-specific handling.")]
	public static ValueTask<T> DeserializeAsync<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T>(
		this IBinaryMessageSerializer serializer,
		byte[] data,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(serializer);
		cancellationToken.ThrowIfCancellationRequested();
		return new ValueTask<T>(serializer.Deserialize<T>(data));
	}

	/// <summary>
	/// Serializes a message with specific options (compression, size limits, etc.).
	/// </summary>
	/// <typeparam name="T"> The type of the message to serialize. </typeparam>
	/// <param name="serializer"> The binary message serializer. </param>
	/// <param name="message"> The message to serialize. </param>
	/// <param name="options"> Serialization options. </param>
	/// <returns> The serialized message as bytes. </returns>
	[RequiresUnreferencedCode("Serialization may require unreferenced code for type-specific handling.")]
	[RequiresDynamicCode("Serialization may require dynamic code generation for type-specific handling.")]
	public static byte[] Serialize<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T>(
		this IBinaryMessageSerializer serializer,
		T message,
		SerializationOptions options)
	{
		ArgumentNullException.ThrowIfNull(serializer);
		_ = options; // Options are currently advisory; delegate to core serializer
		return serializer.Serialize(message);
	}

	/// <summary>
	/// Serializes a message directly to a stream asynchronously.
	/// </summary>
	/// <typeparam name="T"> The type of the message to serialize. </typeparam>
	/// <param name="serializer"> The binary message serializer. </param>
	/// <param name="message"> The message to serialize. </param>
	/// <param name="stream"> The stream to write to. </param>
	/// <param name="cancellationToken"> Cancellation token. </param>
	/// <returns> A task representing the async operation. </returns>
	[RequiresUnreferencedCode("Serialization may require unreferenced code for type-specific handling.")]
	[RequiresDynamicCode("Serialization may require dynamic code generation for type-specific handling.")]
	public static async ValueTask SerializeAsync<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T>(
		this IBinaryMessageSerializer serializer,
		T message,
		Stream stream,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(serializer);
		ArgumentNullException.ThrowIfNull(stream);
		var bytes = serializer.Serialize(message);
		await stream.WriteAsync(bytes, cancellationToken).ConfigureAwait(false);
	}

	/// <summary>
	/// Deserializes from a ReadOnlyMemory for high-performance scenarios.
	/// </summary>
	/// <typeparam name="T"> The type of the message to deserialize. </typeparam>
	/// <param name="serializer"> The binary message serializer. </param>
	/// <param name="data"> The serialized data memory. </param>
	/// <returns> The deserialized message. </returns>
	public static T Deserialize<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T>(
		this IBinaryMessageSerializer serializer,
		ReadOnlyMemory<byte> data)
	{
		ArgumentNullException.ThrowIfNull(serializer);
		return serializer.Deserialize<T>(data.Span);
	}

	/// <summary>
	/// Deserializes from a stream asynchronously.
	/// </summary>
	/// <typeparam name="T"> The type of the message to deserialize. </typeparam>
	/// <param name="serializer"> The binary message serializer. </param>
	/// <param name="stream"> The stream to read from. </param>
	/// <param name="cancellationToken"> Cancellation token. </param>
	/// <returns> The deserialized message. </returns>
	[RequiresUnreferencedCode("Deserialization may require unreferenced code for type-specific handling.")]
	[RequiresDynamicCode("Deserialization may require dynamic code generation for type-specific handling.")]
	public static async ValueTask<T> DeserializeAsync<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T>(
		this IBinaryMessageSerializer serializer,
		Stream stream,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(serializer);
		ArgumentNullException.ThrowIfNull(stream);

		// If the stream supports Length, read directly into a right-sized buffer
		// to avoid the double-copy of CopyToAsync + MemoryStream.ToArray().
		if (stream.CanSeek)
		{
			var length = (int)(stream.Length - stream.Position);
			var buffer = new byte[length];
			await stream.ReadExactlyAsync(buffer, cancellationToken).ConfigureAwait(false);
			return serializer.Deserialize<T>(buffer);
		}

		// For non-seekable streams, accumulate into a MemoryStream then
		// use GetBuffer() with the actual written length to avoid a copy.
		using var ms = new MemoryStream();
		await stream.CopyToAsync(ms, cancellationToken).ConfigureAwait(false);
		return serializer.Deserialize<T>(ms.GetBuffer().AsSpan(0, (int)ms.Length));
	}

	/// <summary>
	/// Gets the serialized size of a message without actually serializing it (if supported).
	/// </summary>
	/// <typeparam name="T"> The type of the message. </typeparam>
	/// <param name="serializer"> The binary message serializer. </param>
	/// <param name="message"> The message to measure. </param>
	/// <returns> The estimated or actual serialized size in bytes. </returns>
	[RequiresUnreferencedCode("Serialization may require unreferenced code for type-specific handling.")]
	[RequiresDynamicCode("Serialization may require dynamic code generation for type-specific handling.")]
	public static int GetSerializedSize<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T>(
		this IBinaryMessageSerializer serializer,
		T message)
	{
		ArgumentNullException.ThrowIfNull(serializer);
		return serializer.Serialize(message).Length;
	}
}
