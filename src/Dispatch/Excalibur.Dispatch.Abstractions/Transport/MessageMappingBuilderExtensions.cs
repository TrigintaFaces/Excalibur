// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Abstractions.Transport;

/// <summary>
/// Extension methods for <see cref="IMessageMappingBuilder"/> providing fluent message mapping configuration.
/// </summary>
/// <remarks>
/// These extensions delegate to <see cref="IMessageMappingConventions"/> via the
/// <see cref="IMessageMappingBuilder.Add"/> method. This follows the
/// <c>IEndpointConventionBuilder</c> pattern where the interface has one core method
/// and all fluent API is provided via extensions.
/// </remarks>
public static class MessageMappingBuilderExtensions
{
	/// <summary>
	/// Begins configuration for mapping a specific message type.
	/// </summary>
	/// <typeparam name="TMessage">The message type to configure mapping for.</typeparam>
	/// <param name="builder">The message mapping builder.</param>
	/// <returns>A builder for configuring transport-specific mappings.</returns>
	public static IMessageTypeMappingBuilder<TMessage> MapMessage<TMessage>(this IMessageMappingBuilder builder)
		where TMessage : class
	{
		ArgumentNullException.ThrowIfNull(builder);

		IMessageTypeMappingBuilder<TMessage>? result = null;
		builder.Add(c => result = c.MapMessage<TMessage>());
		return result!;
	}

	/// <summary>
	/// Registers a custom message mapper.
	/// </summary>
	/// <param name="builder">The message mapping builder.</param>
	/// <param name="mapper">The mapper to register.</param>
	/// <returns>This builder for fluent configuration.</returns>
	public static IMessageMappingBuilder RegisterMapper(this IMessageMappingBuilder builder, IMessageMapper mapper)
	{
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentNullException.ThrowIfNull(mapper);

		builder.Add(c => c.RegisterMapper(mapper));
		return builder;
	}

	/// <summary>
	/// Registers a custom message mapper with a factory.
	/// </summary>
	/// <typeparam name="TMapper">The type of mapper to register.</typeparam>
	/// <param name="builder">The message mapping builder.</param>
	/// <returns>This builder for fluent configuration.</returns>
	public static IMessageMappingBuilder RegisterMapper<TMapper>(this IMessageMappingBuilder builder)
		where TMapper : class, IMessageMapper
	{
		ArgumentNullException.ThrowIfNull(builder);

		builder.Add(c => c.RegisterMapper<TMapper>());
		return builder;
	}

	/// <summary>
	/// Registers the default set of mappers for common transport combinations.
	/// </summary>
	/// <param name="builder">The message mapping builder.</param>
	/// <returns>This builder for fluent configuration.</returns>
	public static IMessageMappingBuilder UseDefaultMappers(this IMessageMappingBuilder builder)
	{
		ArgumentNullException.ThrowIfNull(builder);

		builder.Add(c => c.UseDefaultMappers());
		return builder;
	}

	/// <summary>
	/// Configures a global default mapping that applies to all message types
	/// when no specific mapping is defined.
	/// </summary>
	/// <param name="builder">The message mapping builder.</param>
	/// <param name="configure">Action to configure the default mapping.</param>
	/// <returns>This builder for fluent configuration.</returns>
	public static IMessageMappingBuilder ConfigureDefaults(this IMessageMappingBuilder builder, Action<IDefaultMappingBuilder> configure)
	{
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentNullException.ThrowIfNull(configure);

		builder.Add(c => c.ConfigureDefaults(configure));
		return builder;
	}
}
