// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;

namespace Excalibur.EventSourcing.Abstractions;

/// <summary>
/// Handles a domain event notification raised during <c>EventSourcedRepository.SaveAsync</c>.
/// </summary>
/// <typeparam name="TEvent">
/// The domain event type. Contravariant to allow a handler for a base event type
/// to handle derived event types.
/// </typeparam>
/// <remarks>
/// <para>
/// This handler is distinct from <c>IEventHandler&lt;T&gt;</c> in the Dispatch layer.
/// Dispatch handlers are transport-aware and participate in the messaging pipeline.
/// This handler is EventSourcing-level, in-process only, and invoked during <c>SaveAsync</c>
/// after inline projections have completed.
/// </para>
/// <para>
/// Notification handlers run sequentially after ALL inline projections have finished (R27.8).
/// They are resolved from DI and invoked in registration order.
/// </para>
/// </remarks>
public interface IEventNotificationHandler<in TEvent>
	where TEvent : IDomainEvent
{
	/// <summary>
	/// Handles the domain event notification.
	/// </summary>
	/// <param name="event">The domain event that was committed.</param>
	/// <param name="context">Context about the aggregate that produced the event.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>A task representing the asynchronous operation.</returns>
	Task HandleAsync(
		TEvent @event,
		EventNotificationContext context,
		CancellationToken cancellationToken);
}
