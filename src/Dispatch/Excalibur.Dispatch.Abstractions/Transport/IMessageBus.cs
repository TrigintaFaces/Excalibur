// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Transport;

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
	/// <para>
	/// Events are typically published to multiple subscribers using pub-sub patterns. The implementation handles fan-out to all registered
	/// event handlers. For integration events, the context may specify external endpoints.
	/// </para>
	/// <para>
	/// All handlers for a single published event observe the <b>same</b> scoped service instances. One
	/// dependency-injection scope is created per published event — not one per handler — and it is disposed
	/// only after every handler has completed. Two handlers for the same event therefore share the same
	/// <c>IUnitOfWork</c> or <c>DbContext</c>, so one event is handled as one unit of work. Separate
	/// publishes get separate scopes, and no scope is created at all when no handler for the event depends
	/// on a scoped service.
	/// </para>
	/// <para>
	/// Faults are isolated between handlers; state is not. Every handler is started and awaited, so one
	/// handler throwing does not prevent the others from running — but because they share scoped instances,
	/// a handler that fails after leaving a shared service in a broken state hands that state to the
	/// siblings that run after it. If a handler must not be affected by a sibling's failure, resolve its
	/// dependency from a factory rather than the shared scope, or give it its own message.
	/// </para>
	/// <para>
	/// <b>Which exception you catch does not depend on how many handlers are registered.</b> When exactly
	/// one handler fails, that handler's own exception is rethrown with its original stack trace, whether
	/// one handler was subscribed or ten. Only a genuine multi-fault fan-out — two or more handlers failing
	/// for the same published event — throws <see cref="AggregateException"/>, whose
	/// <see cref="AggregateException.InnerExceptions"/> carries every fault. This is the same unwrapping
	/// rule as <c>Task.GetAwaiter().GetResult()</c>.
	/// </para>
	/// <para>
	/// The guarantee matters because a <c>catch</c> block, an exception mapper, and a typed exception handler
	/// all select on the exception's type. Were a sole fault wrapped, subscribing a second handler would
	/// silently stop the consumer's handling of the first one's exception. Consumers that must also handle
	/// the multi-fault case should catch <see cref="AggregateException"/> in addition to their own type.
	/// </para>
	/// </remarks>
	/// <exception cref="ArgumentNullException"> Thrown when evt or context is null. </exception>
	/// <exception cref="InvalidOperationException"> Thrown when the message bus is not properly configured. </exception>
	/// <exception cref="AggregateException">
	/// Thrown when two or more handlers fail while publishing the event; every fault is in
	/// <see cref="AggregateException.InnerExceptions"/>. A single failing handler rethrows its own exception
	/// instead, regardless of how many handlers are subscribed.
	/// </exception>
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
