// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Buffers;
using System.Diagnostics.CodeAnalysis;

namespace Excalibur.Dispatch.Abstractions.Serialization;

/// <summary>
/// Binary message serializer with support for zero-allocation patterns.
/// </summary>
/// <remarks>
/// <para>
/// This interface defines 2 core methods for zero-allocation serialization plus metadata properties.
/// Async variants, stream operations, options overloads, and convenience helpers are available as
/// extension methods in <see cref="BinaryMessageSerializerExtensions"/>.
/// </para>
/// <para>
/// Inherits <see cref="IMessageSerializer.Serialize{T}(T)"/> and <see cref="IMessageSerializer.Deserialize{T}(byte[])"/>
/// as the byte-array based core operations.
/// </para>
/// </remarks>
public interface IBinaryMessageSerializer : IMessageSerializer
{
	/// <summary>
	/// Gets the content type for this serializer (e.g., "application/json", "application/x-msgpack").
	/// </summary>
	string ContentType { get; }

	/// <summary>
	/// Gets a value indicating whether this serializer supports compression.
	/// </summary>
	bool SupportsCompression { get; }

	/// <summary>
	/// Gets the format name for this serializer (e.g., "JSON", "MessagePack", "Protobuf").
	/// </summary>
	string Format { get; }

	/// <summary>
	/// Serializes a message directly to an IBufferWriter for zero-allocation scenarios.
	/// </summary>
	/// <typeparam name="T"> The type of the message to serialize. </typeparam>
	/// <param name="message"> The message to serialize. </param>
	/// <param name="bufferWriter"> The buffer writer to write to. </param>
	void Serialize<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T>(T message, IBufferWriter<byte> bufferWriter);

	/// <summary>
	/// Deserializes from a ReadOnlySpan for zero-allocation scenarios.
	/// </summary>
	/// <typeparam name="T"> The type of the message to deserialize. </typeparam>
	/// <param name="data"> The serialized data span. </param>
	/// <returns> The deserialized message. </returns>
	T Deserialize<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T>(ReadOnlySpan<byte> data);
}
