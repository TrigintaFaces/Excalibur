// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Buffers;
using System.Diagnostics.CodeAnalysis;

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Serialization;

namespace Excalibur.Dispatch.Messaging;

/// <summary>
/// Extension methods for creating memory messages.
/// </summary>
public static class MemoryMessageExtensions
{
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

		return new MemoryMessage(owner, contentType);
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
			IUtf8JsonSerializer serializer,
			MemoryPool<byte> memoryPool)
	where T : class
	{
		ArgumentNullException.ThrowIfNull(content);
		ArgumentNullException.ThrowIfNull(serializer);
		ArgumentNullException.ThrowIfNull(memoryPool);

		// Serialize to a temporary buffer first to get the size
		var tempBytes = serializer.SerializeToUtf8Bytes(content);

		// Rent memory from the pool
		var owner = memoryPool.Rent(tempBytes.Length);
		tempBytes.CopyTo(owner.Memory.Span);

		// Create the message with pre-deserialized content
		return new MemoryMessageOfT<T>(content: content, body: owner.Memory.Slice(0, tempBytes.Length), contentType: "application/json");
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
		IUtf8JsonSerializer serializer,
		MemoryPool<byte> memoryPool)
	{
		ArgumentNullException.ThrowIfNull(message);
		ArgumentNullException.ThrowIfNull(serializer);
		ArgumentNullException.ThrowIfNull(memoryPool);

		var bytes = serializer.SerializeToUtf8Bytes(message, message.GetType());
		var owner = memoryPool.Rent(bytes.Length);
		bytes.CopyTo(owner.Memory.Span);

		return new MemoryMessage(owner, "application/json");
	}
}
