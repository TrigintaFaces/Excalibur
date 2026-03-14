// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Buffers;

namespace Excalibur.Dispatch.Abstractions.Serialization;

/// <summary>
/// Core serialization contract following the System.Text.Json pattern:
/// minimal core interface with convenience overloads as extension methods
/// in <see cref="SerializerExtensions"/>.
/// </summary>
/// <remarks>
/// <para>
/// This interface replaces IMessageSerializer, IBinaryMessageSerializer, IInternalSerializer,
/// IPluggableSerializer, IJsonSerializer, IUtf8JsonSerializer, IHttpSerializer, and
/// IZeroCopySerializer with a single unified contract.
/// </para>
/// <para>
/// <b>Design:</b>
/// </para>
/// <list type="bullet">
///   <item>4 methods: two generic (IBufferWriter/Span), two runtime-typed (object/Type)</item>
///   <item>3 properties: Name, Version, ContentType</item>
///   <item>Buffer type overloads (byte[], Stream, Pipe, Memory, Sequence, string) are extension methods</item>
///   <item>Implementations must be thread-safe and stateless</item>
///   <item>AOT annotations belong on implementations, not this interface</item>
/// </list>
/// </remarks>
public interface ISerializer
{
	/// <summary>
	/// Gets the unique name of this serializer (e.g., "MemoryPack", "System.Text.Json", "MessagePack").
	/// </summary>
	/// <remarks>
	/// This name is used for serializer lookup by name via <see cref="ISerializerRegistry"/>.
	/// The name should remain constant across versions for compatibility.
	/// </remarks>
	string Name { get; }

	/// <summary>
	/// Gets the version of the serializer library (e.g., "2.1.0").
	/// </summary>
	/// <remarks>
	/// Used for diagnostics and migration scenarios. This is the version of the underlying
	/// serialization library, not a format version.
	/// </remarks>
	string Version { get; }

	/// <summary>
	/// Gets the IANA content type for this serializer (e.g., "application/json", "application/x-msgpack").
	/// </summary>
	/// <remarks>
	/// Used for ASP.NET Core content negotiation via <c>Accept</c> and <c>Content-Type</c> headers.
	/// </remarks>
	string ContentType { get; }

	/// <summary>
	/// Serializes a value to a buffer writer (zero-allocation primary path).
	/// </summary>
	/// <typeparam name="T">The type to serialize.</typeparam>
	/// <param name="value">The value to serialize.</param>
	/// <param name="bufferWriter">The buffer writer to write serialized data to.</param>
	/// <exception cref="ArgumentNullException">Thrown when <paramref name="bufferWriter"/> is null.</exception>
	/// <exception cref="SerializationException">Thrown when serialization fails.</exception>
	void Serialize<T>(T value, IBufferWriter<byte> bufferWriter);

	/// <summary>
	/// Deserializes a value from a contiguous byte span.
	/// </summary>
	/// <typeparam name="T">The type to deserialize to.</typeparam>
	/// <param name="data">The byte span containing serialized data.</param>
	/// <returns>The deserialized value.</returns>
	/// <exception cref="SerializationException">Thrown when deserialization fails.</exception>
	T Deserialize<T>(ReadOnlySpan<byte> data);

	/// <summary>
	/// Serializes a runtime-typed value to bytes.
	/// </summary>
	/// <param name="value">The value to serialize.</param>
	/// <param name="type">The runtime type to use for serialization.</param>
	/// <returns>The serialized byte array.</returns>
	/// <exception cref="ArgumentNullException">Thrown when <paramref name="value"/> or <paramref name="type"/> is null.</exception>
	/// <exception cref="SerializationException">Thrown when serialization fails.</exception>
	/// <remarks>
	/// This overload is necessary for binary serializers (MemoryPack, MessagePack) that require
	/// concrete types when the compile-time type is an interface.
	/// </remarks>
	byte[] SerializeObject(object value, Type type);

	/// <summary>
	/// Deserializes a runtime-typed value from bytes.
	/// </summary>
	/// <param name="data">The byte span containing serialized data.</param>
	/// <param name="type">The runtime type to deserialize to.</param>
	/// <returns>The deserialized object.</returns>
	/// <exception cref="ArgumentNullException">Thrown when <paramref name="type"/> is null.</exception>
	/// <exception cref="SerializationException">Thrown when deserialization fails.</exception>
	object DeserializeObject(ReadOnlySpan<byte> data, Type type);
}
