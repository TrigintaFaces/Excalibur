// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR
// AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.CodeAnalysis;

using Excalibur.Dispatch.Abstractions.Serialization;

using MessagePack;

namespace Excalibur.Dispatch.Serialization.MessagePack;

/// <summary>
/// MessagePack implementation of <see cref="IPluggableSerializer" /> for pluggable serialization.
/// </summary>
/// <remarks>
/// <para>
/// This serializer provides compact binary output with cross-language compatibility. It is ideal for polyglot environments where non-.NET
/// consumers need to read the data.
/// </para>
/// <para> <b> Key Features: </b> </para>
/// <list type="bullet">
/// <item> Compact binary format </item>
/// <item> Cross-language compatibility (Python, Go, Java, etc.) </item>
/// <item> Good performance (faster than JSON, close to MemoryPack) </item>
/// <item> Configurable serialization options </item>
/// </list>
/// <para> <b> Serializer ID: </b><see cref="SerializerIds.MessagePack" /> (3) </para>
/// <para>
/// <b> Naming Note: </b> This class is named <c> MessagePackPluggableSerializer </c> to avoid conflict with <c>
/// MessagePack.MessagePackSerializer </c> from the MessagePack library.
/// </para>
/// <para> See the pluggable serialization architecture documentation. </para>
/// </remarks>
public sealed class MessagePackPluggableSerializer : IPluggableSerializer
{
	private readonly MessagePackSerializerOptions _options;

	/// <summary>
	/// Initializes a new instance of the <see cref="MessagePackPluggableSerializer" /> class with default options (standard MessagePack).
	/// </summary>
	public MessagePackPluggableSerializer()
		: this(null)
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="MessagePackPluggableSerializer" /> class with custom options.
	/// </summary>
	/// <param name="options">
	/// Custom MessagePack serializer options. If null, <see cref="MessagePackSerializerOptions.Standard" /> is used.
	/// </param>
	public MessagePackPluggableSerializer(MessagePackSerializerOptions? options)
	{
		_options = options ?? MessagePackSerializerOptions.Standard;
	}

	/// <inheritdoc />
	/// <value> Returns "MessagePack". </value>
	public string Name => "MessagePack";

	/// <inheritdoc />
	/// <value> Returns the version of the MessagePack library assembly. </value>
	public string Version => typeof(MessagePackSerializerOptions).Assembly
		.GetName().Version?.ToString() ?? "Unknown";

	/// <inheritdoc />
	/// <exception cref="ArgumentNullException"> Thrown when <paramref name="value" /> is null. </exception>
	/// <exception cref="SerializationException"> Thrown when serialization fails. </exception>
	[RequiresUnreferencedCode("Serialization may require unreferenced code for type-specific handling.")]
	[RequiresDynamicCode("Serialization may require dynamic code generation for type-specific handling.")]
	public byte[] Serialize<T>(T value)
	{
		ArgumentNullException.ThrowIfNull(value);

		try
		{
			// Use fully qualified name to avoid ambiguity with class name
			return MessagePackSerializer.Serialize(value, _options);
		}
		catch (Exception ex) when (ex is not ArgumentNullException)
		{
			throw SerializationException.Wrap<T>("serialize", ex);
		}
	}

	/// <inheritdoc />
	/// <exception cref="SerializationException"> Thrown when deserialization fails or returns null. </exception>
	[RequiresUnreferencedCode("Deserialization may require unreferenced code for type-specific handling.")]
	[RequiresDynamicCode("Deserialization may require dynamic code generation for type-specific handling.")]
	public T Deserialize<T>(ReadOnlySpan<byte> data)
	{
		try
		{
			// MessagePackSerializer.Deserialize doesn't have a ReadOnlySpan overload, so we need to convert to Memory<byte> via array
			var array = data.ToArray();
			return MessagePackSerializer.Deserialize<T>(array, _options)
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
	/// <exception cref="ArgumentNullException"> Thrown when <paramref name="value" /> or <paramref name="type" /> is null. </exception>
	/// <exception cref="SerializationException"> Thrown when serialization fails. </exception>
	[RequiresUnreferencedCode("Serialization may require unreferenced code for runtime type handling.")]
	[RequiresDynamicCode("Serialization may require dynamic code generation for runtime type handling.")]
	public byte[] SerializeObject(object value, Type type)
	{
		ArgumentNullException.ThrowIfNull(value);
		ArgumentNullException.ThrowIfNull(type);

		try
		{
			// MessagePack requires concrete types for serialization. Use the non-generic Serialize overload that takes a Type parameter.
			return MessagePackSerializer.Serialize(type, value, _options);
		}
		catch (Exception ex) when (ex is not ArgumentNullException)
		{
			throw SerializationException.WrapObject(type, "serialize", ex);
		}
	}

	/// <inheritdoc />
	/// <exception cref="ArgumentNullException"> Thrown when <paramref name="type" /> is null. </exception>
	/// <exception cref="SerializationException"> Thrown when deserialization fails or returns null. </exception>
	[RequiresUnreferencedCode("Deserialization may require unreferenced code for runtime type handling.")]
	[RequiresDynamicCode("Deserialization may require dynamic code generation for runtime type handling.")]
	public object DeserializeObject(ReadOnlySpan<byte> data, Type type)
	{
		ArgumentNullException.ThrowIfNull(type);

		try
		{
			// MessagePackSerializer.Deserialize doesn't have a ReadOnlySpan overload, so we need to convert to Memory<byte> via array
			var array = data.ToArray();
			return MessagePackSerializer.Deserialize(type, array, _options)
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
