// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;

namespace Excalibur.EventSourcing.Subscriptions;

/// <summary>
/// Represents a subscription to a stream of domain events.
/// </summary>
/// <remarks>
/// <para>
/// Event subscriptions provide a push-based mechanism for receiving new events
/// as they are appended to a stream. Subscriptions automatically track their
/// position and deliver events in order.
/// </para>
/// <para>
/// Call <see cref="SubscribeAsync"/> to start receiving events and
/// <see cref="UnsubscribeAsync"/> to stop.
/// </para>
/// </remarks>
public interface IEventSubscription
{
	/// <summary>
	/// Subscribes to events on the specified stream, delivering batches to the handler.
	/// </summary>
	/// <param name="streamId">The stream identifier to subscribe to.</param>
	/// <param name="handler">
	/// The handler that processes batches of domain events. Events are delivered in order.
	/// </param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>A task representing the subscription setup.</returns>
	Task SubscribeAsync(
		string streamId,
		Func<IReadOnlyList<IDomainEvent>, Task> handler,
		CancellationToken cancellationToken);

	/// <summary>
	/// Unsubscribes from the event stream, stopping event delivery.
	/// </summary>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>A task representing the unsubscribe operation.</returns>
	Task UnsubscribeAsync(CancellationToken cancellationToken);
}
