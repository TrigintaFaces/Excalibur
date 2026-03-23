// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Abstractions.Transport;

/// <summary>
/// Provides the conventions context for configuring message mappings.
/// </summary>
/// <remarks>
/// This type is the target of <see cref="IMessageMappingBuilder.Add"/> callbacks.
/// Extension methods on <see cref="IMessageMappingBuilder"/> delegate to this interface
/// to perform their work.
/// </remarks>
public interface IMessageMappingConventions
{
	/// <summary>
	/// Begins configuration for mapping a specific message type.
	/// </summary>
	/// <typeparam name="TMessage">The message type to configure mapping for.</typeparam>
	/// <returns>A builder for configuring transport-specific mappings.</returns>
	IMessageTypeMappingBuilder<TMessage> MapMessage<TMessage>()
		where TMessage : class;

	/// <summary>
	/// Registers a custom message mapper.
	/// </summary>
	/// <param name="mapper">The mapper to register.</param>
	void RegisterMapper(IMessageMapper mapper);

	/// <summary>
	/// Registers a custom message mapper with a factory.
	/// </summary>
	/// <typeparam name="TMapper">The type of mapper to register.</typeparam>
	void RegisterMapper<TMapper>()
		where TMapper : class, IMessageMapper;

	/// <summary>
	/// Registers the default set of mappers for common transport combinations.
	/// </summary>
	void UseDefaultMappers();

	/// <summary>
	/// Configures a global default mapping that applies to all message types
	/// when no specific mapping is defined.
	/// </summary>
	/// <param name="configure">Action to configure the default mapping.</param>
	void ConfigureDefaults(Action<IDefaultMappingBuilder> configure);
}
