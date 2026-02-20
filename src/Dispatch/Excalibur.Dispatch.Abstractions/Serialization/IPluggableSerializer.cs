// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics.CodeAnalysis;

namespace Excalibur.Dispatch.Abstractions.Serialization;

/// <summary>
/// Abstraction for pluggable serializers used in internal persistence (Outbox, Inbox, Event Store).
/// </summary>
/// <remarks>
/// <para>
/// This interface defines serializers that can be plugged into the <see cref="IPayloadSerializer"/>
/// facade for internal message persistence. Each implementation is identified by a unique ID
/// (see <see cref="SerializerIds"/>) and registered via <see cref="ISerializerRegistry"/>.
/// </para>
/// <para>
/// <b>Key Design Points:</b>
/// </para>
/// <list type="bullet">
///   <item>Implementations must be thread-safe and stateless</item>
///   <item>Uses <see cref="ReadOnlySpan{T}"/> for zero-allocation deserialization</item>
///   <item>Designed for high-performance internal persistence, not external transport</item>
/// </list>
/// <para>
/// <b>Built-in Implementations:</b>
/// </para>
/// <list type="bullet">
///   <item>MemoryPackMessageSerializer (ID: 1) - Default, highest performance</item>
///   <item>SystemTextJsonMessageSerializer (ID: 2) - Human-readable, debugging</item>
///   <item>MessagePackMessageSerializer (ID: 3) - Cross-language compatibility</item>
/// </list>
/// <para>
/// See the pluggable serialization architecture documentation for the magic byte format.
/// </para>
/// </remarks>
public interface IPluggableSerializer
{
	/// <summary>
	/// Gets the unique name of this serializer (e.g., "MemoryPack", "System.Text.Json").
	/// </summary>
	/// <remarks>
	/// This name is used for serializer lookup by name via <see cref="ISerializerRegistry.GetByName"/>.
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
	/// Serializes an object to a byte array.
	/// </summary>
	/// <typeparam name="T">The type to serialize.</typeparam>
	/// <param name="value">The value to serialize.</param>
	/// <returns>The serialized byte array (without magic byte prefix).</returns>
	/// <exception cref="ArgumentNullException">Thrown when value is null.</exception>
	/// <exception cref="SerializationException">Thrown when serialization fails.</exception>
	/// <remarks>
	/// The returned byte array contains only the serialized payload. The magic byte
	/// prefix is added by <see cref="IPayloadSerializer"/>.
	/// </remarks>
	[RequiresUnreferencedCode("Serialization may require unreferenced code for type-specific handling.")]
	[RequiresDynamicCode("Serialization may require dynamic code generation for type-specific handling.")]
	byte[] Serialize<T>(T value);

	/// <summary>
	/// Deserializes a byte span to an object.
	/// </summary>
	/// <typeparam name="T">The type to deserialize to.</typeparam>
	/// <param name="data">The byte span to deserialize (without magic byte prefix).</param>
	/// <returns>The deserialized object.</returns>
	/// <exception cref="SerializationException">Thrown when deserialization fails.</exception>
	/// <remarks>
	/// <para>
	/// The data parameter does not include the magic byte prefix - this is stripped
	/// by <see cref="IPayloadSerializer"/> before calling this method.
	/// </para>
	/// <para>
	/// Using <see cref="ReadOnlySpan{T}"/> enables zero-allocation slicing when the
	/// <see cref="IPayloadSerializer"/> removes the magic byte from the stored payload.
	/// </para>
	/// </remarks>
	[RequiresUnreferencedCode("Deserialization may require unreferenced code for type-specific handling.")]
	[RequiresDynamicCode("Deserialization may require dynamic code generation for type-specific handling.")]
	T Deserialize<T>(ReadOnlySpan<byte> data);

	/// <summary>
	/// Serializes an object to a byte array using the specified runtime type.
	/// </summary>
	/// <param name="value">The value to serialize.</param>
	/// <param name="type">The runtime type to use for serialization.</param>
	/// <returns>The serialized byte array (without magic byte prefix).</returns>
	/// <exception cref="ArgumentNullException">Thrown when value or type is null.</exception>
	/// <exception cref="SerializationException">Thrown when serialization fails.</exception>
	/// <remarks>
	/// <para>
	/// This overload is necessary for binary serializers (MemoryPack, MessagePack) that require
	/// concrete types. When the compile-time type is an interface (e.g., <c>IDispatchMessage</c>),
	/// binary serializers fail because they cannot serialize interface types directly.
	/// </para>
	/// <para>
	/// Use this method when you have an object reference typed as an interface but need to
	/// serialize using the actual runtime type.
	/// </para>
	/// </remarks>
	[RequiresUnreferencedCode("Serialization may require unreferenced code for runtime type handling.")]
	[RequiresDynamicCode("Serialization may require dynamic code generation for runtime type handling.")]
	byte[] SerializeObject(object value, Type type);

	/// <summary>
	/// Deserializes a byte span to an object using the specified runtime type.
	/// </summary>
	/// <param name="data">The byte span to deserialize (without magic byte prefix).</param>
	/// <param name="type">The runtime type to deserialize to.</param>
	/// <returns>The deserialized object.</returns>
	/// <exception cref="ArgumentNullException">Thrown when type is null.</exception>
	/// <exception cref="SerializationException">Thrown when deserialization fails.</exception>
	/// <remarks>
	/// <para>
	/// This overload is necessary for migration scenarios where the concrete type is only known
	/// at runtime (e.g., from stored type name metadata). Binary serializers (MemoryPack, MessagePack)
	/// require concrete types for deserialization.
	/// </para>
	/// <para>
	/// See the serializer migration strategy documentation that uses this method.
	/// </para>
	/// </remarks>
	[RequiresUnreferencedCode("Deserialization may require unreferenced code for runtime type handling.")]
	[RequiresDynamicCode("Deserialization may require dynamic code generation for runtime type handling.")]
	object DeserializeObject(ReadOnlySpan<byte> data, Type type);
}
