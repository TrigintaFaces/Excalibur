// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Buffers;

using Excalibur.Dispatch.Abstractions.Serialization;

using MemoryPack;

namespace Excalibur.Dispatch.Serialization.MemoryPack;

/// <summary>
/// MemoryPack-based implementation of <see cref="IInternalSerializer"/>.
/// </summary>
/// <remarks>
/// <para>
/// This is the default high-performance internal serializer for Excalibur framework wire formats.
/// It provides:
/// </para>
/// <list type="bullet">
///   <item><description>Excellent performance (comparable to hand-rolled solutions)</description></item>
///   <item><description>Full AOT/trimming compatibility via source generators</description></item>
///   <item><description>Zero-copy serialization when using <see cref="IBufferWriter{T}"/></description></item>
///   <item><description>Support for schema evolution via [MemoryPackOrder] attributes</description></item>
/// </list>
/// <para>
/// See the design documentation for the full design rationale.
/// </para>
/// </remarks>
public sealed class MemoryPackInternalSerializer : IInternalSerializer
{
	/// <inheritdoc />
	public void Serialize<T>(T value, IBufferWriter<byte> bufferWriter)
	{
		MemoryPackSerializer.Serialize(bufferWriter, value);
	}

	/// <inheritdoc />
	public byte[] Serialize<T>(T value)
	{
		return MemoryPackSerializer.Serialize(value);
	}

	/// <inheritdoc />
	/// <remarks>
	/// IL2091 is suppressed because MemoryPack uses source generators that provide full AOT/trimming
	/// compatibility without requiring runtime reflection. The serialization type info is generated
	/// at compile time via [MemoryPackable] attribute on types.
	/// </remarks>
#pragma warning disable IL2091 // Target generic argument does not satisfy 'DynamicallyAccessedMembersAttribute' - MemoryPack uses source generators
	public T Deserialize<T>(ReadOnlySequence<byte> buffer)
	{
		return MemoryPackSerializer.Deserialize<T>(buffer)
			?? throw new System.Runtime.Serialization.SerializationException($"Failed to deserialize {typeof(T).Name}: result was null");
	}
#pragma warning restore IL2091

	/// <inheritdoc />
	/// <remarks>
	/// IL2091 is suppressed because MemoryPack uses source generators that provide full AOT/trimming
	/// compatibility without requiring runtime reflection. The serialization type info is generated
	/// at compile time via [MemoryPackable] attribute on types.
	/// </remarks>
#pragma warning disable IL2091 // Target generic argument does not satisfy 'DynamicallyAccessedMembersAttribute' - MemoryPack uses source generators
	public T Deserialize<T>(ReadOnlySpan<byte> buffer)
	{
		return MemoryPackSerializer.Deserialize<T>(buffer)
			?? throw new System.Runtime.Serialization.SerializationException($"Failed to deserialize {typeof(T).Name}: result was null");
	}
#pragma warning restore IL2091
}
