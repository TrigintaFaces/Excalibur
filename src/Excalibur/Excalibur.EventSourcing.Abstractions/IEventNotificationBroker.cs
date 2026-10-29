// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch;

namespace Excalibur.EventSourcing;

/// <summary>
/// Orchestrates in-process event notification after events have been committed to the event store.
/// </summary>
/// <remarks>
/// <para>
/// The broker is invoked by <c>EventSourcedRepository.SaveAsync</c> immediately after
/// <c>IEventStore.AppendAsync</c> succeeds. It invokes the registered
/// <c>IEventNotificationHandler&lt;TEvent&gt;</c> instances sequentially, one event at a time in
/// commit order. Inline completion then drives the ordered projection processor,
/// which applies committed events in global-stream order.
/// </para>
/// <para>
/// This interface is resolved from DI as a nullable service. When no broker is registered,
/// <c>SaveAsync</c> behavior is identical to the pre-notification era (zero overhead).
/// </para>
/// <para>
/// <b>Important:</b> Because events are already committed when the broker runs,
/// callers must NOT retry <c>SaveAsync</c> if notification fails. The save succeeded; only a
/// handler did not.
/// </para>
/// </remarks>
public interface IEventNotificationBroker
{
	/// <summary>
	/// Notifies the registered event notification handlers about committed events.
	/// </summary>
	/// <param name="events">The domain events that were committed, in order.</param>
	/// <param name="context">Context about the aggregate that produced the events.</param>
	/// <returns>
	/// Whether this save's <c>Inline</c> projections reached the context's global position:
	/// <see cref="ProjectionFreshness.NotRequested"/> when no projection is registered <c>Inline</c>,
	/// <see cref="ProjectionFreshness.Attested"/> when they were observed to have folded the prefix
	/// through it against the roster as it then stood, and
	/// <see cref="ProjectionFreshness.BoundExpired"/> when the bounded wait ended first. The value is
	/// RETURNED rather than thrown because the events are already committed: an exception here reports a
	/// failed save that did not fail, and every retry policy in the ecosystem would repeat the command.
	/// </returns>
	/// <exception cref="System.Exception">
	/// A failing handler's own exception is rethrown with its stack trace preserved, when
	/// <see cref="EventNotificationOptions.FailurePolicy"/> is
	/// <see cref="NotificationFailurePolicy.Propagate"/>.
	/// </exception>
	/// <remarks>
	/// <para>
	/// <b>This method takes no cancellation token, and the omission is the contract.</b> It runs after
	/// the events are durable, so a caller that gives up cannot un-commit them -- the only thing its
	/// cancellation could still achieve is leaving the read model unapplied while the caller is told a
	/// durable save failed. Accepting a token would make that outcome expressible at every call site;
	/// not accepting one makes it expressible at none.
	/// </para>
	/// <para>
	/// Notification delivery and Inline completion each use <see cref="EventNotificationOptions.PostCommitTimeout"/>, on their
	/// own clock, so an unresponsive store still cannot hold a save open forever.
	/// </para>
	/// </remarks>
	Task<ProjectionCompletion> NotifyAsync(
		IReadOnlyList<IDomainEvent> events,
		EventNotificationContext context);
}
