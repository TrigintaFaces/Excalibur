// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics.CodeAnalysis;

using Excalibur.Dispatch.Abstractions.Configuration;
using Excalibur.Dispatch.Abstractions.Transport;
using Excalibur.Dispatch.Transport;

using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for configuring message mapping via dependency injection.
/// </summary>
public static class MessageMappingServiceCollectionExtensions
{
	/// <summary>
	/// Adds message mapping services to the service collection.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <returns>The service collection for chaining.</returns>
	public static IServiceCollection AddMessageMapping(this IServiceCollection services)
	{
		ArgumentNullException.ThrowIfNull(services);

		services.TryAddSingleton<MessageMapperRegistry>();
		services.TryAddSingleton<IMessageMapperRegistry>(sp => sp.GetRequiredService<MessageMapperRegistry>());

		return services;
	}

	/// <summary>
	/// Adds message mapping services with configuration to the service collection.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="configure">Action to configure message mapping.</param>
	/// <returns>The service collection for chaining.</returns>
	public static IServiceCollection AddMessageMapping(this IServiceCollection services, Action<IMessageMappingBuilder> configure)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configure);

		_ = services.AddMessageMapping();

		// Create and configure the builder
		var registry = new MessageMapperRegistry();
		var builder = new MessageMappingBuilder(services, registry);
		configure(builder);

		// Register the configured mapper
		var configuredMapper = builder.Build();
		_ = services.AddSingleton(configuredMapper);
		_ = services.AddSingleton(configuredMapper);

		// Replace the registry with the configured one
		_ = services.Replace(ServiceDescriptor.Singleton(registry));
		_ = services.Replace(ServiceDescriptor.Singleton<IMessageMapperRegistry>(registry));

		return services;
	}

	/// <summary>
	/// Configures message mapping for the dispatch builder.
	/// </summary>
	/// <param name="builder">The dispatch builder.</param>
	/// <param name="configure">Action to configure message mapping.</param>
	/// <returns>The dispatch builder for fluent configuration.</returns>
	public static IDispatchBuilder WithMessageMapping(this IDispatchBuilder builder, Action<IMessageMappingBuilder> configure)
	{
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentNullException.ThrowIfNull(configure);

		_ = builder.Services.AddMessageMapping(configure);
		return builder;
	}

	/// <summary>
	/// Adds default message mappers (RabbitMQ↔Kafka, etc.) to the dispatch builder.
	/// </summary>
	/// <param name="builder">The dispatch builder.</param>
	/// <returns>The dispatch builder for fluent configuration.</returns>
	public static IDispatchBuilder UseDefaultMessageMappers(this IDispatchBuilder builder)
	{
		ArgumentNullException.ThrowIfNull(builder);

		_ = builder.Services.AddMessageMapping(mapping => mapping.UseDefaultMappers());
		return builder;
	}

	/// <summary>
	/// Registers a custom message mapper with the dispatch builder.
	/// </summary>
	/// <typeparam name="TMapper">The type of mapper to register.</typeparam>
	/// <param name="builder">The dispatch builder.</param>
	/// <returns>The dispatch builder for fluent configuration.</returns>
	public static IDispatchBuilder AddMessageMapper<
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
	TMapper>(this IDispatchBuilder builder)
		where TMapper : class, IMessageMapper
	{
		ArgumentNullException.ThrowIfNull(builder);

		_ = builder.Services.AddMessageMapping();
		builder.Services.TryAddSingleton<TMapper>();
		_ = builder.Services.AddSingleton<IMessageMapper, TMapper>();

		return builder;
	}

	/// <summary>
	/// Registers a custom message mapper instance with the dispatch builder.
	/// </summary>
	/// <param name="builder">The dispatch builder.</param>
	/// <param name="mapper">The mapper instance to register.</param>
	/// <returns>The dispatch builder for fluent configuration.</returns>
	public static IDispatchBuilder AddMessageMapper(this IDispatchBuilder builder, IMessageMapper mapper)
	{
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentNullException.ThrowIfNull(mapper);

		_ = builder.Services.AddMessageMapping();
		_ = builder.Services.AddSingleton(mapper);
		_ = builder.Services.AddSingleton(sp => mapper);

		// Also register with the registry
		_ = builder.Services.AddSingleton(sp =>
		{
			var registry = sp.GetRequiredService<MessageMapperRegistry>();
			registry.Register(mapper);
			return mapper;
		});

		return builder;
	}
}
