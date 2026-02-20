// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics.CodeAnalysis;

namespace Excalibur.Dispatch.Abstractions.Serialization;

/// <summary>
/// Provides binary serialization capabilities for messages in the dispatch pipeline.
/// </summary>
/// <remarks>
/// Message serializers handle the conversion between strongly-typed message objects and binary data for transport over message brokers,
/// storage, or network protocols. Key responsibilities include:
/// <list type="bullet">
/// <item> Efficient binary serialization and deserialization </item>
/// <item> Version compatibility and evolution support </item>
/// <item> Performance optimization for high-throughput scenarios </item>
/// <item> Cross-language compatibility where needed </item>
/// <item> Schema validation and format detection </item>
/// </list>
/// Implementations should be thread-safe and handle circular references appropriately. Consider memory allocation patterns for
/// high-frequency serialization scenarios.
/// </remarks>
public interface IMessageSerializer
{
	/// <summary>
	/// Gets the unique name identifying this serializer implementation.
	/// </summary>
	/// <remarks>
	/// Used for serializer selection and metadata tracking. Common examples include "SystemTextJson", "MessagePack", "Protobuf", "Avro".
	/// This name should remain constant across versions for compatibility.
	/// </remarks>
	string SerializerName { get; }

	/// <summary>
	/// Gets the version of this serializer implementation.
	/// </summary>
	/// <remarks>
	/// Used for backward compatibility and migration scenarios. The version should follow semantic versioning principles and be incremented
	/// when serialization format changes in incompatible ways.
	/// </remarks>
	string SerializerVersion { get; }

	/// <summary>
	/// Serializes a message object to binary data.
	/// </summary>
	/// <typeparam name="T"> The type of message to serialize. </typeparam>
	/// <param name="message"> The message object to serialize. </param>
	/// <returns> The serialized message as a byte array. </returns>
	/// <remarks>
	/// Implementations should handle null values appropriately and ensure the output is deterministic for the same input. Consider using
	/// buffer pooling for memory efficiency in high-throughput scenarios.
	/// </remarks>
	/// <exception cref="ArgumentNullException"> Thrown when message is null and null values are not supported. </exception>
	/// <exception cref="InvalidOperationException"> Thrown when the message type cannot be serialized. </exception>
	[RequiresUnreferencedCode("Serialization may require unreferenced code for type-specific handling.")]
	[RequiresDynamicCode("Serialization may require dynamic code generation for type-specific handling.")]
	byte[] Serialize<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T>(T message);

	/// <summary>
	/// Deserializes binary data back to a strongly-typed message object.
	/// </summary>
	/// <typeparam name="T"> The expected type of the deserialized message. </typeparam>
	/// <param name="data"> The binary data to deserialize. </param>
	/// <returns> The deserialized message object. </returns>
	/// <remarks>
	/// Implementations should validate the data format and provide meaningful error messages for corrupt or incompatible data. Handle
	/// version mismatches gracefully where possible through migration or compatibility layers.
	/// </remarks>
	/// <exception cref="ArgumentNullException"> Thrown when data is null. </exception>
	/// <exception cref="ArgumentException"> Thrown when data is empty or invalid. </exception>
	/// <exception cref="InvalidOperationException"> Thrown when data cannot be deserialized to the specified type. </exception>
	[RequiresUnreferencedCode("Deserialization may require unreferenced code for type-specific handling.")]
	[RequiresDynamicCode("Deserialization may require dynamic code generation for type-specific handling.")]
	T Deserialize<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T>(byte[] data);
}
