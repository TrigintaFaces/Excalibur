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
	/// This method provides access to the <see cref="MemoryPackPluggableSerializer"/> for manual registration
	/// with the serializer registry. Use this when you need to register the serializer directly.
	/// </para>
	/// <para>
	/// <b>Serializer ID:</b> <see cref="SerializerIds.MemoryPack"/> (1)
	/// </para>
	/// <para>
	/// <b>Usage:</b>
	/// </para>
	/// <code>
	/// // Register via the builder pattern (preferred)
	/// services.AddDispatch()
	///     .ConfigurePluggableSerialization(config =>
	///     {
	///         config.RegisterMemoryPack();  // Auto-registers from this package
	///     });
	///
	/// // Or register manually via ISerializerRegistry
	/// var registry = services.GetRequiredService&lt;ISerializerRegistry&gt;();
	/// registry.Register(SerializerIds.MemoryPack, MemoryPackSerializationServiceCollectionExtensions.GetPluggableSerializer());
	/// </code>
	/// <para>
	/// <b>Note:</b> MemoryPack is the default serializer. When using <c>AddDispatch()</c> or
	/// <c>AddPluggableSerialization()</c>, MemoryPack is automatically registered and set as current.
	/// There is no separate <c>AddMemoryPackPluggableSerialization()</c> method because that would
	/// create a circular dependency (Dispatch references MemoryPack for default serialization).
	/// </para>
	/// <para>
	/// See the pluggable serialization architecture documentation.
	/// </para>
	/// </remarks>
	public static ISerializer GetPluggableSerializer() => new MemoryPackSerializer();
}
