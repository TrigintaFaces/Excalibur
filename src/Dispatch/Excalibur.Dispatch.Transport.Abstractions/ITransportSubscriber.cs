// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

namespace Excalibur.Dispatch.Transport;

/// <summary>
/// Push-based transport subscription interface.
/// <para>
/// Unlike <see cref="ITransportReceiver"/> (pull-based, returns <see cref="TransportReceivedMessage"/>),
/// this interface pushes messages to a callback handler. Transports with native push semantics
/// (Kafka consumer groups, RabbitMQ BasicConsume, Azure Event Hubs, Google PubSub streaming pull)
/// implement this interface directly.
/// </para>
/// <para>
/// Follows the same minimal-interface pattern as <see cref="ITransportSender"/>
/// and <see cref="ITransportReceiver"/>.
/// </para>
/// </summary>
public interface ITransportSubscriber : IAsyncDisposable
{
	/// <summary>
	/// Gets the source (topic, queue, subscription) this subscriber reads from.
	/// </summary>
	string Source { get; }

	/// <summary>
	/// Subscribes to receive messages via the provided handler callback.
	/// The handler is invoked for each received message. The subscription runs until
	/// the <paramref name="cancellationToken"/> is cancelled or <see cref="IAsyncDisposable.DisposeAsync"/> is called.
	/// </summary>
	/// <param name="handler">
	/// Callback invoked for each received message. Return <see cref="MessageAction.Acknowledge"/>
	/// to acknowledge, <see cref="MessageAction.Reject"/> to reject, or
	/// <see cref="MessageAction.Requeue"/> to requeue the message.
	/// </param>
	/// <param name="cancellationToken">Cancellation token to stop the subscription.</param>
	/// <returns>A task that completes when the subscription ends.</returns>
	Task SubscribeAsync(
		Func<TransportReceivedMessage, CancellationToken, Task<MessageAction>> handler,
		CancellationToken cancellationToken);

	/// <summary>
	/// Gets the underlying transport service (e.g., IConsumer, IChannel, SubscriberClient).
	/// Follows the IChatClient.GetService() pattern from Microsoft.Extensions.AI.
	/// </summary>
	/// <remarks>
	/// The default implementation answers for any service this instance itself implements, so a transport
	/// that provides one directly need not override it. A transport overrides it to hand out its native
	/// SDK handle, and a decorator overrides it to answer for itself before deferring to the transport it
	/// wraps.
	/// </remarks>
	/// <exception cref="ArgumentNullException"> Thrown when <paramref name="serviceType"/> is null. </exception>
	object? GetService(Type serviceType)
	{
		ArgumentNullException.ThrowIfNull(serviceType);

		return serviceType.IsInstanceOfType(this) ? this : null;
	}
}
