// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Abstractions.Routing;

/// <summary>
/// Service responsible for selecting the appropriate transport (message bus) for a message.
/// </summary>
/// <remarks>
/// <para>
/// The transport selector determines which message bus should handle a given message.
/// Common transports include "local" (in-process), "rabbitmq", "kafka", "azureservicebus", etc.
/// </para>
/// <para>
/// This interface replaces the former <c>IRouterService</c> with clearer naming that
/// distinguishes transport selection from endpoint routing.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// services.AddDispatch(dispatch =>
/// {
///     dispatch.UseRouting(routing =>
///     {
///         routing.Transport
///             .Route&lt;OrderCreated&gt;().To("rabbitmq")
///             .Route&lt;PaymentProcessed&gt;().To("kafka")
///             .Default("local");
///     });
/// });
/// </code>
/// </example>
public interface ITransportSelector
{
	/// <summary>
	/// Selects the transport for a message based on configured routing rules.
	/// </summary>
	/// <param name="message">The message to route.</param>
	/// <param name="context">The message context containing metadata and state.</param>
	/// <param name="cancellationToken">Token to cancel the operation.</param>
	/// <returns>
	/// The name of the selected transport (e.g., "local", "rabbitmq", "kafka").
	/// </returns>
	/// <exception cref="ArgumentNullException">
	/// Thrown when <paramref name="message"/> or <paramref name="context"/> is null.
	/// </exception>
	ValueTask<string> SelectTransportAsync(
		IDispatchMessage message,
		IMessageContext context,
		CancellationToken cancellationToken);

	/// <summary>
	/// Gets all available transports that can handle the specified message type.
	/// </summary>
	/// <param name="messageType">The type of message to check.</param>
	/// <returns>
	/// A collection of transport names that have rules configured for the message type,
	/// plus the default transport.
	/// </returns>
	/// <exception cref="ArgumentNullException">
	/// Thrown when <paramref name="messageType"/> is null.
	/// </exception>
	IEnumerable<string> GetAvailableTransports(Type messageType);
}
