// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics.CodeAnalysis;

namespace Excalibur.Dispatch.Abstractions.Delivery;

/// <summary>
/// Defines a handler for processing events in the messaging pipeline.
/// </summary>
/// <typeparam name="TEvent"> The type of event to handle. </typeparam>
/// <remarks>
/// Implement this interface to react to events that have occurred in the system. Unlike action handlers, multiple event handlers can
/// process the same event, supporting the publish-subscribe pattern. Common use cases include:
/// <list type="bullet">
/// <item> Updating read models and projections </item>
/// <item> Triggering workflows and side effects </item>
/// <item> Sending notifications and alerts </item>
/// <item> Maintaining audit logs and analytics </item>
/// <item> Cache invalidation and synchronization </item>
/// </list>
/// Event handlers should be idempotent as events may be redelivered. The contravariant in modifier allows handlers to process base event types.
/// </remarks>
[SuppressMessage("Naming", "CA1711:Identifiers should not have incorrect suffix",
	Justification = "Event handler pattern intentionally uses the Handler suffix.")]
public interface IEventHandler<in TEvent>
	where TEvent : IDispatchEvent
{
	/// <summary>
	/// Handles the specified event asynchronously.
	/// </summary>
	/// <param name="eventMessage"> The event to handle. </param>
	/// <param name="cancellationToken"> The cancellation token to observe. </param>
	/// <returns> A task representing the asynchronous operation. </returns>
	/// <remarks>
	/// Implementations should be idempotent to handle potential redelivery. Event handlers execute independently; one handler's failure
	/// doesn't affect others. Consider the eventual consistency implications when designing event handlers.
	/// </remarks>
	Task HandleAsync(TEvent eventMessage, CancellationToken cancellationToken);
}
