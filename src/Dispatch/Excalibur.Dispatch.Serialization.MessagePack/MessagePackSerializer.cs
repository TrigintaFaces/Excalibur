// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Buffers;
using System.Diagnostics.CodeAnalysis;

using Excalibur.Dispatch.Abstractions.Serialization;

using Mp = global::MessagePack.MessagePackSerializer;
using MpOptions = global::MessagePack.MessagePackSerializerOptions;

namespace Excalibur.Dispatch.Serialization.MessagePack;

/// <summary>
/// Consolidated MessagePack serializer implementing <see cref="ISerializer"/>.
/// </summary>
/// <remarks>
/// <para>
/// Replaces MessagePackPluggableSerializer, MessagePackMessageSerializer,
/// DispatchMessagePackSerializer, AotMessagePackSerializer, and MessagePackZeroCopySerializer.
/// </para>
/// <para>
/// <b>Serializer ID:</b> <see cref="SerializerIds.MessagePack"/> (3)
/// </para>
/// </remarks>
[RequiresUnreferencedCode("MessagePack serialization may require unreferenced code for type-specific handling.")]
[RequiresDynamicCode("MessagePack serialization may require dynamic code generation for type-specific handling.")]
public sealed class MessagePackSerializer : ISerializer
{
	private readonly MpOptions _options;

	/// <summary>
	/// Initializes a new instance with default MessagePack options.
	/// </summary>
	public MessagePackSerializer()
		: this(null)
	{
	}

	/// <summary>
	/// Initializes a new instance with custom MessagePack options.
	/// </summary>
	/// <param name="options">Custom MessagePack serializer options. If null, uses Standard.</param>
	public MessagePackSerializer(MpOptions? options)
	{
		_options = options ?? MpOptions.Standard;
	}

	/// <inheritdoc />
	public string Name => "MessagePack";

	/// <inheritdoc />
	public string Version => typeof(MpOptions).Assembly
		.GetName().Version?.ToString() ?? "Unknown";

	/// <inheritdoc />
	public string ContentType => "application/x-msgpack";

	/// <inheritdoc />
	public void Serialize<T>(T value, IBufferWriter<byte> bufferWriter)
	{
		ArgumentNullException.ThrowIfNull(bufferWriter);
		Mp.Serialize(bufferWriter, value, _options);
	}

	/// <inheritdoc cref="ISerializer.Deserialize{T}"/>
	public T Deserialize<T>(ReadOnlySpan<byte> data)
	{
		try
		{
			var array = data.ToArray();
			return Mp.Deserialize<T>(array, _options)
				?? throw SerializationException.NullResult<T>();
		}
		catch (SerializationException)
		{
			throw;
		}
		catch (Exception ex)
		{
			throw SerializationException.Wrap<T>("deserialize", ex);
		}
	}

	/// <inheritdoc />
	public byte[] SerializeObject(object value, Type type)
	{
		ArgumentNullException.ThrowIfNull(value);
		ArgumentNullException.ThrowIfNull(type);

		try
		{
			return Mp.Serialize(type, value, _options);
		}
		catch (Exception ex) when (ex is not ArgumentNullException)
		{
			throw SerializationException.WrapObject(type, "serialize", ex);
		}
	}

	/// <inheritdoc />
	public object DeserializeObject(ReadOnlySpan<byte> data, Type type)
	{
		ArgumentNullException.ThrowIfNull(type);

		try
		{
			var array = data.ToArray();
			return Mp.Deserialize(type, array, _options)
				?? throw SerializationException.NullResultForType(type);
		}
		catch (SerializationException)
		{
			throw;
		}
		catch (Exception ex)
		{
			throw SerializationException.WrapObject(type, "deserialize", ex);
		}
	}

}
