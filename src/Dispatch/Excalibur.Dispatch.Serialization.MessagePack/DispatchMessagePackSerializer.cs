// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics.CodeAnalysis;

using Excalibur.Dispatch.Abstractions.Serialization;

using MessagePack;

namespace Excalibur.Dispatch.Serialization.MessagePack;

/// <summary>
/// MessagePack-based implementation of IMessageSerializer providing high-performance binary serialization with LZ4 compression. Optimized
/// for throughput and data size reduction in messaging scenarios.
/// </summary>
/// <param name="options"> Optional MessagePack serialization options. If null, uses standard options with LZ4Block compression. </param>
public sealed class DispatchMessagePackSerializer(MessagePackSerializerOptions? options = null) : IMessageSerializer
{
	private readonly MessagePackSerializerOptions _options =
		options ?? MessagePackSerializerOptions.Standard.WithCompression(MessagePackCompression.Lz4BlockArray);

	/// <summary>
	/// Gets the name identifier for this serializer implementation.
	/// </summary>
	/// <value>The current <see cref="SerializerName"/> value.</value>
	public string SerializerName => "MessagePack";

	/// <summary>
	/// Gets the version of this serializer for compatibility tracking.
	/// </summary>
	/// <value>The current <see cref="SerializerVersion"/> value.</value>
	public string SerializerVersion => "1.0.0";

	/// <summary>
	/// Serializes the specified message to a binary format using MessagePack with compression.
	/// </summary>
	/// <typeparam name="T"> The type of the message to serialize. </typeparam>
	/// <param name="message"> The message object to serialize. </param>
	/// <returns> The serialized message as a byte array. </returns>
	[RequiresUnreferencedCode("Serialization may require unreferenced code for type-specific handling.")]
	[RequiresDynamicCode("Serialization may require dynamic code generation for type-specific handling.")]
	public byte[] Serialize<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T>(T message) =>
			MessagePackSerializer.Serialize(message, _options);

	/// <summary>
	/// Deserializes the specified binary data into a message of the specified type using MessagePack.
	/// </summary>
	/// <typeparam name="T"> The type of the message to deserialize. </typeparam>
	/// <param name="data"> The binary data to deserialize. </param>
	/// <returns> The deserialized message object. </returns>
	[RequiresUnreferencedCode("Deserialization may require unreferenced code for type-specific handling.")]
	[RequiresDynamicCode("Deserialization may require dynamic code generation for type-specific handling.")]
	public T Deserialize<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T>(byte[] data) =>
			MessagePackSerializer.Deserialize<T>(data, _options);
}
