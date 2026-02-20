// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics.CodeAnalysis;

using Excalibur.Dispatch.Abstractions.Serialization;

using MessagePack;
using MessagePack.Resolvers;

namespace Excalibur.Dispatch.Serialization.MessagePack;

/// <summary>
/// AOT-compatible MessagePack serializer that uses compile-time code generation.
/// </summary>
/// <remarks>
/// This implementation is designed for Native AOT compatibility by using MessagePack's source generation features and avoiding
/// reflection-based serialization at runtime.
/// </remarks>
/// <remarks> Initializes a new instance of the AOT-compatible MessagePack serializer. </remarks>
/// <param name="options"> Optional MessagePack serializer options. </param>
public sealed class AotMessagePackSerializer(MessagePackSerializerOptions? options = null) : IMessageSerializer
{
	private readonly MessagePackSerializerOptions _options = options ?? MessagePackSerializerOptions.Standard
		.WithResolver(StaticCompositeResolver.Instance)
		.WithCompression(MessagePackCompression.Lz4BlockArray);

	/// <summary>
	/// Gets the name of this serializer.
	/// </summary>
	/// <value>The current <see cref="SerializerName"/> value.</value>
	public string SerializerName => "MessagePack-AOT";

	/// <summary>
	/// Gets the version of this serializer.
	/// </summary>
	/// <value>The current <see cref="SerializerVersion"/> value.</value>
	public string SerializerVersion => "1.0.0";

	/// <summary>
	/// Serializes a message to bytes using AOT-compatible MessagePack serialization.
	/// </summary>
	/// <typeparam name="T"> The type of message to serialize. </typeparam>
	/// <param name="message"> The message to serialize. </param>
	/// <returns> The serialized message as bytes. </returns>
	[RequiresUnreferencedCode("Serialization may require unreferenced code for type-specific handling.")]
	[RequiresDynamicCode("Serialization may require dynamic code generation for type-specific handling.")]
	public byte[] Serialize<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T>(T message)
	{
		ArgumentNullException.ThrowIfNull(message);
		return MessagePackSerializer.Serialize(message, _options);
	}

	/// <summary>
	/// Deserializes bytes to a message using AOT-compatible MessagePack deserialization.
	/// </summary>
	/// <typeparam name="T"> The type of message to deserialize. </typeparam>
	/// <param name="data"> The bytes to deserialize. </param>
	/// <returns> The deserialized message. </returns>
	[RequiresUnreferencedCode("Deserialization may require unreferenced code for type-specific handling.")]
	[RequiresDynamicCode("Deserialization may require dynamic code generation for type-specific handling.")]
	public T Deserialize<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T>(byte[] data)
	{
		ArgumentNullException.ThrowIfNull(data);
		return MessagePackSerializer.Deserialize<T>(data, _options);
	}
}
