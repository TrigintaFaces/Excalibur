// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Configuration;
using Excalibur.Dispatch.Abstractions.Serialization;
using Excalibur.Dispatch.Serialization;

using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for configuring pluggable serialization services.
/// </summary>
public static class PluggableSerializationServiceCollectionExtensions
{
	/// <summary>
	/// Adds pluggable serialization services to the service collection.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <returns>The service collection for method chaining.</returns>
	/// <remarks>
	/// <para>
	/// This method registers:
	/// </para>
	/// <list type="bullet">
	///   <item><see cref="ISerializerRegistry"/> - Singleton registry for serializers</item>
	///   <item><see cref="IPayloadSerializer"/> - Singleton facade for internal serialization</item>
	/// </list>
	/// <para>
	/// By default, MemoryPack is auto-registered and set as the current serializer.
	/// Use <see cref="WithSerialization"/> to customize this behavior.
	/// </para>
	/// </remarks>
	public static IServiceCollection AddPluggableSerialization(this IServiceCollection services)
	{
		ArgumentNullException.ThrowIfNull(services);

		// Register options infrastructure
		_ = services.AddOptions<PluggableSerializationOptions>()
			.ValidateDataAnnotations()
			.ValidateOnStart();

		// Register the registry with deferred configuration
		services.TryAddSingleton<ISerializerRegistry>(sp =>
		{
			var options = sp.GetRequiredService<IOptions<PluggableSerializationOptions>>().Value;
			var registry = new SerializerRegistry();

			// Auto-register MemoryPack if enabled (default: true)
			if (options.AutoRegisterMemoryPack && !registry.IsRegistered(SerializerIds.MemoryPack))
			{
				registry.Register(
					SerializerIds.MemoryPack,
					MemoryPackSerializationServiceCollectionExtensions.GetPluggableSerializer());
			}

			// Execute all registration actions
			foreach (var action in options.RegistrationActions)
			{
				action(registry);
			}

			// Set current serializer if specified, otherwise default to MemoryPack if auto-registered
			if (!string.IsNullOrEmpty(options.CurrentSerializerName))
			{
				registry.SetCurrent(options.CurrentSerializerName);
			}
			else if (options.AutoRegisterMemoryPack)
			{
				registry.SetCurrent("MemoryPack");
			}

			return registry;
		});

		// Register the payload serializer
		services.TryAddSingleton<IPayloadSerializer>(sp =>
		{
			var registry = sp.GetRequiredService<ISerializerRegistry>();
			var logger = sp.GetRequiredService<ILogger<PayloadSerializer>>();
			return new PayloadSerializer(registry, logger);
		});

		return services;
	}

	/// <summary>
	/// Configures serialization for internal persistence using <see cref="ISerializationBuilder"/>.
	/// </summary>
	/// <param name="builder">The dispatch builder.</param>
	/// <param name="configure">Configuration action for the serialization builder.</param>
	/// <returns>The dispatch builder for method chaining.</returns>
	/// <remarks>
	/// <para>
	/// The <see cref="ISerializationBuilder"/> has 3 core methods; format-specific convenience
	/// methods are available as extension methods in <see cref="SerializationBuilderExtensions"/>.
	/// </para>
	/// </remarks>
	public static IDispatchBuilder WithSerialization(
		this IDispatchBuilder builder,
		Action<ISerializationBuilder> configure)
	{
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentNullException.ThrowIfNull(configure);

		// Ensure pluggable serialization services are registered
		_ = builder.Services.AddPluggableSerialization();

		// Configure options using the builder pattern
		_ = builder.Services.Configure<PluggableSerializationOptions>(options =>
		{
			var serializationBuilder = new PluggableSerializationBuilder(options);
			configure(serializationBuilder);
		});

		return builder;
	}

	/// <summary>
	/// Registers a pluggable serializer with the service collection.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="id">The serializer ID.</param>
	/// <param name="serializer">The serializer implementation.</param>
	/// <returns>The service collection for method chaining.</returns>
	/// <remarks>
	/// <para>
	/// This method is typically used by serializer packages (e.g., Excalibur.Dispatch.Serialization.MemoryPack)
	/// to register their implementations during service configuration.
	/// </para>
	/// <para>
	/// The serializer will be registered when the <see cref="ISerializerRegistry"/> is first resolved.
	/// </para>
	/// </remarks>
	public static IServiceCollection AddPluggableSerializer(
		this IServiceCollection services,
		byte id,
		ISerializer serializer)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(serializer);

		// Ensure base services are registered
		_ = services.AddPluggableSerialization();

		// Add registration action
		_ = services.Configure<PluggableSerializationOptions>(options =>
		{
			options.AddRegistration(registry => registry.Register(id, serializer));
		});

		return services;
	}

	/// <summary>
	/// Registers a pluggable serializer with the service collection and sets it as current.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="id">The serializer ID.</param>
	/// <param name="serializer">The serializer implementation.</param>
	/// <param name="setAsCurrent">Whether to set this serializer as the current serializer.</param>
	/// <returns>The service collection for method chaining.</returns>
	public static IServiceCollection AddPluggableSerializer(
		this IServiceCollection services,
		byte id,
		ISerializer serializer,
		bool setAsCurrent)
	{
		_ = services.AddPluggableSerializer(id, serializer);

		if (setAsCurrent)
		{
			_ = services.Configure<PluggableSerializationOptions>(options =>
			{
				options.CurrentSerializerName = serializer.Name;
			});
		}

		return services;
	}

	/// <summary>
	/// Adds the event serializer to the service collection.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <returns>The service collection for method chaining.</returns>
	/// <remarks>
	/// Registers <see cref="SpanEventSerializer"/> as <see cref="IEventSerializer"/>.
	/// Requires <see cref="AddPluggableSerialization"/> to be called first.
	/// </remarks>
	public static IServiceCollection AddEventSerializer(this IServiceCollection services)
	{
		ArgumentNullException.ThrowIfNull(services);

		// Ensure pluggable serialization is registered (provides ISerializerRegistry)
		_ = services.AddPluggableSerialization();

		// Register SpanEventSerializer as IEventSerializer
		services.TryAddSingleton<IEventSerializer>(sp =>
		{
			var registry = sp.GetRequiredService<ISerializerRegistry>();
			return new SpanEventSerializer(registry);
		});

		return services;
	}
}
