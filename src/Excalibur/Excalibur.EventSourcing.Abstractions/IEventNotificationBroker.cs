// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;

namespace Excalibur.EventSourcing.Abstractions;

/// <summary>
/// Orchestrates in-process event notification after events have been committed to the event store.
/// </summary>
/// <remarks>
/// <para>
/// The broker is invoked by <c>EventSourcedRepository.SaveAsync</c> immediately after
/// <c>IEventStore.AppendAsync</c> succeeds. It runs inline projections first (via
/// <c>Task.WhenAll</c> across projection types), then notification handlers sequentially.
/// Projections and handlers never overlap.
/// </para>
/// <para>
/// This interface is resolved from DI as a nullable service. When no broker is registered,
/// <c>SaveAsync</c> behavior is identical to the pre-notification era (zero overhead).
/// </para>
/// <para>
/// <b>Important:</b> Because events are already committed when the broker runs,
/// callers must NOT retry <c>SaveAsync</c> if notification fails. Instead, use
/// <c>IProjectionRecovery.ReapplyAsync</c> for failed projections.
/// </para>
/// </remarks>
public interface IEventNotificationBroker
{
	/// <summary>
	/// Notifies inline projections and event notification handlers about committed events.
	/// </summary>
	/// <param name="events">The domain events that were committed, in order.</param>
	/// <param name="context">Context about the aggregate that produced the events.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>A task representing the asynchronous notification operation.</returns>
	/// <exception cref="System.AggregateException">
	/// Thrown when one or more inline projections fail and
	/// <see cref="EventNotificationOptions.FailurePolicy"/> is
	/// <see cref="NotificationFailurePolicy.Propagate"/>.
	/// </exception>
	Task NotifyAsync(
		IReadOnlyList<IDomainEvent> events,
		EventNotificationContext context,
		CancellationToken cancellationToken);
}
