// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Buffers;
using System.Diagnostics.CodeAnalysis;

using Mp = MessagePack.MessagePackSerializer;
using MpOptions = MessagePack.MessagePackSerializerOptions;

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
[RequiresUnreferencedCode(
	"MessagePack-CSharp uses runtime code generation for formatter resolution (MakeGenericType in StandardResolver).")]
[RequiresDynamicCode(
	"MessagePack-CSharp uses runtime code generation for formatter resolution. Pre-generated formatters via mpc tool do not eliminate all internal reflection.")]
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
	/// <param name="options">
	/// Custom MessagePack serializer options. If <see langword="null"/>, defaults to
	/// <see cref="MpOptions.Standard"/> hardened with
	/// <see cref="global::MessagePack.MessagePackSecurity.UntrustedData"/> (see remarks on the
	/// trust boundary). A caller that supplies explicit options owns its own security profile.
	/// </param>
	/// <remarks>
	/// <b>Trust boundary:</b> transport/inbox payloads routinely originate off-process, so the
	/// no-options default applies <c>MessagePackSecurity.UntrustedData</c> — MessagePack-CSharp's
	/// guidance for untrusted input. Without it, <see cref="MpOptions.Standard"/> leaves the
	/// deserializer open to hash-collision attacks on dictionary/extension types and unbounded
	/// recursion/allocation (stack overflow / OOM) from a hostile payload. Callers that supply their
	/// own <paramref name="options"/> are responsible for selecting an appropriate security profile.
	/// </remarks>
	public MessagePackSerializer(MpOptions? options)
	{
		_options = options ?? MpOptions.Standard.WithSecurity(global::MessagePack.MessagePackSecurity.UntrustedData);
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
		ArgumentNullException.ThrowIfNull(value);
		ArgumentNullException.ThrowIfNull(bufferWriter);

		try
		{
			Mp.Serialize(bufferWriter, value, _options);
		}
		catch (SerializationException)
		{
			throw;
		}
		catch (Exception ex)
		{
			throw SerializationException.Wrap<T>("serialize", ex);
		}
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
