// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR
// AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0
using System.Diagnostics.CodeAnalysis;

using Excalibur.Dispatch.Abstractions.Serialization;

using MemoryPack;

namespace Excalibur.Dispatch.Serialization.MemoryPack;

/// <summary>
/// MemoryPack implementation of <see cref="IPluggableSerializer" /> for pluggable serialization.
/// </summary>
/// <remarks>
/// <para>
/// This is the default high-performance serializer for internal persistence in the Dispatch framework. It wraps MemoryPack's serialization
/// capabilities and implements the pluggable serializer interface for use with <see cref="IPayloadSerializer" />.
/// </para>
/// <para> <b> Key Features: </b> </para>
/// <list type="bullet">
/// <item> Highest performance among built-in serializers </item>
/// <item> Full AOT/trimming compatibility via source generators </item>
/// <item> Zero-allocation deserialization via ReadOnlySpan </item>
/// <item> Support for schema evolution via [MemoryPackOrder] attributes </item>
/// </list>
/// <para> <b> Serializer ID: </b><see cref="SerializerIds.MemoryPack" /> (1) </para>
/// <para> See the pluggable serialization architecture documentation. </para>
/// </remarks>
public sealed class MemoryPackPluggableSerializer : IPluggableSerializer
{
	/// <inheritdoc />
	/// <value> Returns "MemoryPack". </value>
	public string Name => "MemoryPack";

	/// <inheritdoc />
	/// <value> Returns the version of the MemoryPack library assembly. </value>
	public string Version => typeof(MemoryPackSerializer).Assembly
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
			return MemoryPackSerializer.Serialize(value);
		}
		catch (Exception ex) when (ex is not ArgumentNullException)
		{
			throw SerializationException.Wrap<T>("serialize", ex);
		}
	}

	/// <inheritdoc />
	/// <exception cref="SerializationException"> Thrown when deserialization fails or returns null. </exception>
	/// <remarks>
	/// IL2091 is suppressed because MemoryPack uses source generators that provide full AOT/trimming compatibility
	/// without requiring runtime reflection. The serialization type info is generated at compile time via
	/// [MemoryPackable] attribute on types.
	/// </remarks>
#pragma warning disable IL2091 // Target generic argument does not satisfy 'DynamicallyAccessedMembersAttribute' - MemoryPack uses source generators
	[RequiresUnreferencedCode("Deserialization may require unreferenced code for type-specific handling.")]
	[RequiresDynamicCode("Deserialization may require dynamic code generation for type-specific handling.")]
	public T Deserialize<T>(ReadOnlySpan<byte> data)
	{
		try
		{
			// Use fully qualified name to avoid ambiguity with class name
			return MemoryPackSerializer.Deserialize<T>(data)
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
			// MemoryPack requires concrete types for serialization. Use the non-generic Serialize overload that takes a Type parameter.
			return MemoryPackSerializer.Serialize(type, value);
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
			// MemoryPack requires concrete types for deserialization. Use the non-generic Deserialize overload that takes a Type parameter.
			return MemoryPackSerializer.Deserialize(type, data)
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
