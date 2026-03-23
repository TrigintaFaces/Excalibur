// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Abstractions.Transport;

/// <summary>
/// Builder for configuring transport-specific mappings for a message type.
/// </summary>
/// <typeparam name="TMessage">The message type being configured.</typeparam>
/// <remarks>
/// The core interface provides <see cref="ToTransport"/> for custom/generic transport
/// configuration. Transport-specific methods (ToRabbitMq, ToKafka, etc.) are provided
/// as extension methods.
/// </remarks>
public interface IMessageTypeMappingBuilder<TMessage>
	where TMessage : class
{
	/// <summary>
	/// Configures a custom transport mapping for this message type.
	/// </summary>
	/// <param name="transportName">The name of the custom transport.</param>
	/// <param name="configure">Action to configure the transport context.</param>
	/// <returns>This builder for fluent configuration.</returns>
	IMessageTypeMappingBuilder<TMessage> ToTransport(string transportName, Action<ITransportMessageContext> configure);

	/// <summary>
	/// Returns to the parent builder to configure another message type.
	/// </summary>
	/// <returns>The parent message mapping builder.</returns>
	IMessageMappingBuilder And();
}
