// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics.CodeAnalysis;

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Serialization;

using MessagePack;

using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Serialization.MessagePack;

/// <summary>
/// MessagePack implementation of message serializer.
/// </summary>
public sealed class MessagePackMessageSerializer : IMessageSerializer
{
	private readonly MessagePackSerializerOptions _options;

	/// <summary>
	/// Initializes a new instance of the <see cref="MessagePackMessageSerializer" /> class.
	/// </summary>
	/// <param name="options"> The MessagePack serialization options. </param>
	public MessagePackMessageSerializer(IOptions<MessagePackSerializationOptions> options)
	{
		ArgumentNullException.ThrowIfNull(options);
		_options = options.Value.MessagePackSerializerOptions;
	}

	/// <inheritdoc />
	public string SerializerName => "MessagePack";

	/// <inheritdoc />
	public string SerializerVersion => "1.0.0";

	/// <inheritdoc />
	[RequiresUnreferencedCode("Serialization may require unreferenced code for type-specific handling.")]
	[RequiresDynamicCode("Serialization may require dynamic code generation for type-specific handling.")]
	public byte[] Serialize<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T>(T message)
	{
		ArgumentNullException.ThrowIfNull(message);
		return MessagePackSerializer.Serialize(message, _options);
	}

	/// <inheritdoc />
	[RequiresUnreferencedCode("Deserialization may require unreferenced code for type-specific handling.")]
	[RequiresDynamicCode("Deserialization may require dynamic code generation for type-specific handling.")]
	public T Deserialize<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T>(byte[] data)
	{
		ArgumentNullException.ThrowIfNull(data);
		var result = MessagePackSerializer.Deserialize<T>(data, _options);
		return result ?? throw new InvalidOperationException(
				ErrorMessages.DeserializedMessageCannotBeNull);
	}
}
