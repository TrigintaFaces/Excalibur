// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics.CodeAnalysis;

using Excalibur.Dispatch.Abstractions.Serialization;
using Excalibur.Dispatch.Serialization.MemoryPack;

using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for configuring MemoryPack-based internal serialization.
/// </summary>
public static class MemoryPackSerializationServiceCollectionExtensions
{
	/// <summary>
	/// Adds the MemoryPack-based internal serializer to the service collection.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <returns>The service collection for chaining.</returns>
	/// <remarks>
	/// <para>
	/// This registers <see cref="MemoryPackInternalSerializer"/> as the implementation
	/// of <see cref="IInternalSerializer"/> using TryAddSingleton pattern.
	/// </para>
	/// <para>
	/// If a different <see cref="IInternalSerializer"/> is already registered,
	/// this method will not replace it.
	/// </para>
	/// </remarks>
	/// <example>
	/// <code>
	/// services.AddMemoryPackInternalSerialization();
	/// </code>
	/// </example>
	public static IServiceCollection AddMemoryPackInternalSerialization(this IServiceCollection services)
	{
		ArgumentNullException.ThrowIfNull(services);
		services.TryAddSingleton<IInternalSerializer, MemoryPackInternalSerializer>();
		return services;
	}

	/// <summary>
	/// Adds a custom internal serializer implementation to the service collection.
	/// </summary>
	/// <typeparam name="TSerializer">The serializer implementation type.</typeparam>
	/// <param name="services">The service collection.</param>
	/// <returns>The service collection for chaining.</returns>
	/// <remarks>
	/// This replaces any previously registered <see cref="IInternalSerializer"/>.
	/// </remarks>
	/// <example>
	/// <code>
	/// services.AddInternalSerialization&lt;CustomSerializer&gt;();
	/// </code>
	/// </example>
	public static IServiceCollection AddInternalSerialization<
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TSerializer>(this IServiceCollection services)
		where TSerializer : class, IInternalSerializer
	{
		ArgumentNullException.ThrowIfNull(services);
		_ = services.AddSingleton<IInternalSerializer, TSerializer>();
		return services;
	}

	/// <summary>
	/// Adds a custom internal serializer instance to the service collection.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="serializer">The serializer instance.</param>
	/// <returns>The service collection for chaining.</returns>
	/// <remarks>
	/// This replaces any previously registered <see cref="IInternalSerializer"/>.
	/// </remarks>
	/// <example>
	/// <code>
	/// var customSerializer = new CustomSerializer(options);
	/// services.AddInternalSerialization(customSerializer);
	/// </code>
	/// </example>
	public static IServiceCollection AddInternalSerialization(
		this IServiceCollection services,
		IInternalSerializer serializer)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(serializer);
		_ = services.AddSingleton(serializer);
		return services;
	}

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
	public static IPluggableSerializer GetPluggableSerializer() => new MemoryPackPluggableSerializer();
}
