// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics.CodeAnalysis;

using Excalibur.Dispatch.Abstractions.Serialization;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Excalibur.Dispatch.Serialization.MessagePack;

/// <summary>
/// Extension methods for registering MessagePack serialization support.
/// </summary>
public static class MessagePackSerializationExtensions
{
	/// <summary>
	/// Adds MessagePack serialization support to the Dispatch pipeline.
	/// This is an opt-in package for high-performance binary serialization.
	/// Uses the zero-copy serializer by default for maximum performance.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="configure">Optional configuration delegate.</param>
	/// <returns>The service collection for chaining.</returns>
	public static IServiceCollection AddMessagePackSerialization(
		this IServiceCollection services,
		Action<MessagePackSerializationOptions>? configure = null)
	{
		ArgumentNullException.ThrowIfNull(services);

		if (configure != null)
		{
			_ = services.Configure(configure);
		}

		// Register the fastest MessagePack serializer implementations
		// Zero-copy serializer for pipeline-based serialization
		services.TryAddSingleton<IZeroCopySerializer, MessagePackZeroCopySerializer>();

		// Standard MessagePack serializer for IMessageSerializer consumers
		services.TryAddSingleton<IMessageSerializer, DispatchMessagePackSerializer>();

		return services;
	}

	/// <summary>
	/// Adds MessagePack serialization support with a specific serializer implementation.
	/// </summary>
	/// <typeparam name="TSerializer">The MessagePack serializer implementation type.</typeparam>
	/// <param name="services">The service collection.</param>
	/// <param name="configure">Optional configuration delegate.</param>
	/// <returns>The service collection for chaining.</returns>
	public static IServiceCollection AddMessagePackSerialization<
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TSerializer>(
	this IServiceCollection services,
	Action<MessagePackSerializationOptions>? configure = null)
	where TSerializer : class, IMessageSerializer
	{
		ArgumentNullException.ThrowIfNull(services);

		if (configure != null)
		{
			_ = services.Configure(configure);
		}

		// Register zero-copy serializer if available
		services.TryAddSingleton<IZeroCopySerializer, MessagePackZeroCopySerializer>();

		// Register the specified IMessageSerializer implementation
		services.TryAddSingleton<IMessageSerializer, TSerializer>();

		return services;
	}

	/// <summary>
	/// Gets the MessagePack pluggable serializer singleton instance for use with <see cref="ISerializerRegistry"/>.
	/// </summary>
	/// <returns>The MessagePack pluggable serializer instance with default options.</returns>
	/// <remarks>
	/// <para>
	/// This method provides access to the <see cref="MessagePackPluggableSerializer"/> for manual registration
	/// with the serializer registry. Use this when you need to register the serializer directly.
	/// </para>
	/// <para>
	/// <b>Serializer ID:</b> <see cref="SerializerIds.MessagePack"/> (3)
	/// </para>
	/// <para>
	/// <b>Usage:</b>
	/// </para>
	/// <code>
	/// // Register via the builder pattern (preferred)
	/// services.AddDispatch()
	///     .ConfigurePluggableSerialization(config =>
	///     {
	///         config.RegisterMessagePack();  // Auto-registers from this package
	///     });
	///
	/// // Or register manually via ISerializerRegistry
	/// var registry = services.GetRequiredService&lt;ISerializerRegistry&gt;();
	/// registry.Register(SerializerIds.MessagePack, MessagePackSerializationExtensions.GetPluggableSerializer());
	/// </code>
	/// <para>
	/// See the pluggable serialization architecture documentation.
	/// </para>
	/// </remarks>
	public static IPluggableSerializer GetPluggableSerializer() => new MessagePackPluggableSerializer();

	/// <summary>
	/// Gets the MessagePack pluggable serializer with custom options for use with <see cref="ISerializerRegistry"/>.
	/// </summary>
	/// <param name="options">Custom MessagePack serializer options.</param>
	/// <returns>The MessagePack pluggable serializer instance with custom options.</returns>
	public static IPluggableSerializer GetPluggableSerializer(global::MessagePack.MessagePackSerializerOptions options)
	{
		ArgumentNullException.ThrowIfNull(options);
		return new MessagePackPluggableSerializer(options);
	}

	/// <summary>
	/// Adds the MessagePack serializer to the pluggable serialization system.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="setAsCurrent">
	/// Whether to set MessagePack as the current serializer for new payloads.
	/// Default is <c>false</c> (MemoryPack remains default).
	/// </param>
	/// <returns>The service collection for method chaining.</returns>
	/// <remarks>
	/// <para>
	/// This method registers <see cref="MessagePackPluggableSerializer"/> with
	/// <see cref="SerializerIds.MessagePack"/> (3) in the pluggable serialization registry.
	/// </para>
	/// <para>
	/// <b>Usage:</b>
	/// </para>
	/// <code>
	/// // Register MessagePack alongside default MemoryPack
	/// services.AddMessagePackPluggableSerialization();
	///
	/// // Register MessagePack and set as current serializer
	/// services.AddMessagePackPluggableSerialization(setAsCurrent: true);
	/// </code>
	/// <para>
	/// This method requires the <c>Excalibur.Dispatch.Serialization</c> namespace to be available.
	/// Ensure <c>AddPluggableSerialization()</c> or <c>AddDispatch()</c> has been called.
	/// </para>
	/// <para>
	/// See the pluggable serialization architecture documentation.
	/// </para>
	/// </remarks>
	public static IServiceCollection AddMessagePackPluggableSerialization(
		this IServiceCollection services,
		bool setAsCurrent = false)
	{
		ArgumentNullException.ThrowIfNull(services);

		// Register using the extension from Microsoft.Extensions.DependencyInjection
		return PluggableSerializationServiceCollectionExtensions
			.AddPluggableSerializer(
				services,
				SerializerIds.MessagePack,
				new MessagePackPluggableSerializer(),
				setAsCurrent);
	}

	/// <summary>
	/// Adds the MessagePack serializer with custom options to the pluggable serialization system.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="options">Custom MessagePack serializer options.</param>
	/// <param name="setAsCurrent">
	/// Whether to set MessagePack as the current serializer for new payloads.
	/// Default is <c>false</c> (MemoryPack remains default).
	/// </param>
	/// <returns>The service collection for method chaining.</returns>
	public static IServiceCollection AddMessagePackPluggableSerialization(
		this IServiceCollection services,
		global::MessagePack.MessagePackSerializerOptions options,
		bool setAsCurrent = false)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(options);

		return PluggableSerializationServiceCollectionExtensions
			.AddPluggableSerializer(
				services,
				SerializerIds.MessagePack,
				new MessagePackPluggableSerializer(options),
				setAsCurrent);
	}
}
