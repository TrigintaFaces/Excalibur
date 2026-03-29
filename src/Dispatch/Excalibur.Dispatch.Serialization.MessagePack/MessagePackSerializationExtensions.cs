// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Dispatch.Abstractions.Serialization;
using Excalibur.Dispatch.Serialization;
using Excalibur.Dispatch.Serialization.MessagePack;

using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection;

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

		// Register the consolidated MessagePack serializer
		services.TryAddSingleton<MessagePackSerializer>();
		services.TryAddSingleton<ISerializer>(sp => sp.GetRequiredService<MessagePackSerializer>());

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
	public static ISerializer GetPluggableSerializer() => new MessagePackSerializer();

	/// <summary>
	/// Gets the MessagePack pluggable serializer with custom options for use with <see cref="ISerializerRegistry"/>.
	/// </summary>
	/// <param name="options">Custom MessagePack serializer options.</param>
	/// <returns>The MessagePack pluggable serializer instance with custom options.</returns>
	public static ISerializer GetPluggableSerializer(global::MessagePack.MessagePackSerializerOptions options)
	{
		ArgumentNullException.ThrowIfNull(options);
		return new MessagePackSerializer(options);
	}

	/// <summary>
	/// Registers the MessagePack serializer with the serialization builder (framework-assigned ID: 3).
	/// </summary>
	/// <param name="builder">The serialization builder.</param>
	/// <returns>The builder for method chaining.</returns>
	/// <remarks>
	/// <para>
	/// This extension enables the builder pattern:
	/// </para>
	/// <code>
	/// services.AddDispatch()
	///     .WithSerialization(config =>
	///     {
	///         config.RegisterMessagePack();
	///         config.UseMessagePack();
	///     });
	/// </code>
	/// </remarks>
	public static ISerializationBuilder RegisterMessagePack(this ISerializationBuilder builder)
	{
		ArgumentNullException.ThrowIfNull(builder);
		return builder.Register(new MessagePackSerializer(), SerializerIds.MessagePack);
	}

	/// <summary>
	/// Registers the MessagePack serializer with configuration (framework-assigned ID: 3).
	/// </summary>
	/// <param name="builder">The serialization builder.</param>
	/// <param name="configure">Configuration delegate for MessagePack serialization options.</param>
	/// <returns>The builder for method chaining.</returns>
	public static ISerializationBuilder RegisterMessagePack(
		this ISerializationBuilder builder,
		Action<MessagePackSerializationOptions> configure)
	{
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentNullException.ThrowIfNull(configure);

		var options = new MessagePackSerializationOptions();
		configure(options);

		return builder.Register(
			new MessagePackSerializer(options.MessagePackSerializerOptions),
			SerializerIds.MessagePack);
	}

	/// <summary>
	/// Registers the MessagePack serializer with custom native options (framework-assigned ID: 3).
	/// </summary>
	/// <param name="builder">The serialization builder.</param>
	/// <param name="options">Custom MessagePack serializer options.</param>
	/// <returns>The builder for method chaining.</returns>
	public static ISerializationBuilder RegisterMessagePack(
		this ISerializationBuilder builder,
		global::MessagePack.MessagePackSerializerOptions options)
	{
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentNullException.ThrowIfNull(options);
		return builder.Register(new MessagePackSerializer(options), SerializerIds.MessagePack);
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
				new MessagePackSerializer(),
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
				new MessagePackSerializer(options),
				setAsCurrent);
	}
}
