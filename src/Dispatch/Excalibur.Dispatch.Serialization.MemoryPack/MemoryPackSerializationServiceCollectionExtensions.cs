// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Dispatch.Abstractions.Serialization;
using Excalibur.Dispatch.Serialization.MemoryPack;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for configuring MemoryPack serialization.
/// </summary>
public static class MemoryPackSerializationServiceCollectionExtensions
{
	/// <summary>
	/// Gets the MemoryPack pluggable serializer singleton instance for use with <see cref="ISerializerRegistry"/>.
	/// </summary>
	/// <returns>The MemoryPack pluggable serializer instance.</returns>
	/// <remarks>
	/// <para>
	/// This method provides access to the <see cref="MemoryPackSerializer"/> for manual registration
	/// with the serializer registry. Use this when you need to register the serializer directly.
	/// </para>
	/// <para>
	/// <b>Serializer ID:</b> <see cref="SerializerIds.MemoryPack"/> (1)
	/// </para>
	/// <para>
	/// <b>Usage:</b>
	/// </para>
	/// <code>
	/// // Register MemoryPack via the builder pattern (opt-in, replaces JSON default)
	/// services.AddDispatch(dispatch =>
	///     dispatch.WithSerialization(config =>
	///     {
	///         config.Register(new MemoryPackSerializer(), SerializerIds.MemoryPack);
	///         config.UseMemoryPack();
	///     }));
	/// </code>
	/// <para>
	/// <b>Note:</b> JSON (System.Text.Json) is the default serializer (ADR-295).
	/// MemoryPack is opt-in for high-performance binary serialization in .NET-only environments.
	/// Register it explicitly via this method when needed.
	/// </para>
	/// <para>
	/// See the pluggable serialization architecture documentation.
	/// </para>
	/// </remarks>
	public static ISerializer GetPluggableSerializer() => new MemoryPackSerializer();
}
