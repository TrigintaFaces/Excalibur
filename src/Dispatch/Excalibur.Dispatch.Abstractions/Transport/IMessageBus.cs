// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Abstractions.Transport;

/// <summary>
/// Provides an abstraction for publishing messages to the underlying messaging infrastructure.
/// </summary>
/// <remarks>
/// The message bus is responsible for routing messages to their appropriate destinations, which may be in-process handlers, message queues,
/// or remote services. Implementations handle serialization, transport, and delivery guarantees. Key responsibilities include:
/// <list type="bullet">
/// <item> Message serialization and envelope creation </item>
/// <item> Transport-specific protocol handling </item>
/// <item> Routing based on message type and configuration </item>
/// <item> Delivery guarantees (at-least-once, at-most-once, exactly-once) </item>
/// <item> Integration with various message brokers (RabbitMQ, Kafka, Azure Service Bus, etc.) </item>
/// </list>
/// The message bus is typically accessed through IDispatcher rather than directly. Multiple message bus implementations can be registered
/// for different transports.
/// </remarks>
/// <seealso cref="IMessageBusProvider" />
/// <seealso cref="IDispatcher" />
public interface IMessageBus
{
	/// <summary>
	/// Publishes an action message to the message bus.
	/// </summary>
	/// <param name="action"> The action to publish. </param>
	/// <param name="context"> The message context containing metadata and routing information. </param>
	/// <param name="cancellationToken"> The cancellation token to observe. </param>
	/// <returns> A task representing the asynchronous publish operation. </returns>
	/// <remarks>
	/// Actions are typically routed to a single handler. For request-reply patterns, the context.ReplyTo property indicates where responses
	/// should be sent. The implementation ensures the message is durably queued or delivered.
	/// </remarks>
	/// <exception cref="ArgumentNullException"> Thrown when action or context is null. </exception>
	/// <exception cref="InvalidOperationException"> Thrown when the message bus is not properly configured. </exception>
	Task PublishAsync(IDispatchAction action, IMessageContext context, CancellationToken cancellationToken);

	/// <summary>
	/// Publishes an event message to the message bus.
	/// </summary>
	/// <param name="evt"> The event to publish. </param>
	/// <param name="context"> The message context containing metadata and routing information. </param>
	/// <param name="cancellationToken"> The cancellation token to observe. </param>
	/// <returns> A task representing the asynchronous publish operation. </returns>
	/// <remarks>
	/// Events are typically published to multiple subscribers using pub-sub patterns. The implementation handles fan-out to all registered
	/// event handlers. For integration events, the context may specify external endpoints.
	/// </remarks>
	/// <exception cref="ArgumentNullException"> Thrown when evt or context is null. </exception>
	/// <exception cref="InvalidOperationException"> Thrown when the message bus is not properly configured. </exception>
	Task PublishAsync(IDispatchEvent evt, IMessageContext context, CancellationToken cancellationToken);

	/// <summary>
	/// Publishes a document message to the message bus.
	/// </summary>
	/// <param name="doc"> The document to publish. </param>
	/// <param name="context"> The message context containing metadata and routing information. </param>
	/// <param name="cancellationToken"> The cancellation token to observe. </param>
	/// <returns> A task representing the asynchronous publish operation. </returns>
	/// <remarks>
	/// Documents may be routed to multiple handlers for processing different aspects. Large documents may be chunked or use claim-check
	/// patterns depending on the transport limitations and configuration.
	/// </remarks>
	/// <exception cref="ArgumentNullException"> Thrown when doc or context is null. </exception>
	/// <exception cref="InvalidOperationException"> Thrown when the message bus is not properly configured. </exception>
	Task PublishAsync(IDispatchDocument doc, IMessageContext context, CancellationToken cancellationToken);
}
