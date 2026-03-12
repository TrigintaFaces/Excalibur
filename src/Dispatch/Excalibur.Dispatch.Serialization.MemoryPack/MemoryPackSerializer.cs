// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Buffers;
using System.Diagnostics.CodeAnalysis;

using Excalibur.Dispatch.Abstractions.Serialization;

using Mp = global::MemoryPack.MemoryPackSerializer;

namespace Excalibur.Dispatch.Serialization.MemoryPack;

/// <summary>
/// Consolidated MemoryPack serializer implementing <see cref="ISerializer"/>.
/// </summary>
/// <remarks>
/// <para>
/// This is the default high-performance serializer for the Excalibur framework.
/// It consolidates the previously separate MemoryPackPluggableSerializer and
/// MemoryPackInternalSerializer into a single class.
/// </para>
/// <para>
/// <b>Serializer ID:</b> <see cref="SerializerIds.MemoryPack"/> (1)
/// </para>
/// </remarks>
[RequiresUnreferencedCode("MemoryPack serialization may require unreferenced code for type-specific handling.")]
[RequiresDynamicCode("MemoryPack serialization may require dynamic code generation for type-specific handling.")]
public sealed class MemoryPackSerializer : ISerializer
{
	/// <inheritdoc />
	public string Name => "MemoryPack";

	/// <inheritdoc />
	public string Version => typeof(global::MemoryPack.MemoryPackSerializer).Assembly
		.GetName().Version?.ToString() ?? "Unknown";

	/// <inheritdoc />
	public string ContentType => "application/x-memorypack";

	/// <inheritdoc />
	public void Serialize<T>(T value, IBufferWriter<byte> bufferWriter)
	{
		ArgumentNullException.ThrowIfNull(bufferWriter);
		Mp.Serialize(bufferWriter, value);
	}

#pragma warning disable IL2091 // MemoryPack uses source generators for AOT compatibility
	/// <inheritdoc cref="ISerializer.Deserialize{T}"/>
	public T Deserialize<T>(ReadOnlySpan<byte> data)
	{
		try
		{
			return Mp.Deserialize<T>(data)
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
#pragma warning restore IL2091

	/// <inheritdoc />
	public byte[] SerializeObject(object value, Type type)
	{
		ArgumentNullException.ThrowIfNull(value);
		ArgumentNullException.ThrowIfNull(type);

		try
		{
			return Mp.Serialize(type, value);
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
			return Mp.Deserialize(type, data)
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
