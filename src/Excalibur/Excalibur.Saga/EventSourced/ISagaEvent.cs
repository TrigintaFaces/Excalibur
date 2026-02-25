// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Saga.EventSourced;

/// <summary>
/// Represents an event that records a state change in an event-sourced saga.
/// </summary>
/// <remarks>
/// <para>
/// Saga events capture the complete history of state transitions, step completions,
/// and failures for a saga instance. The saga state can be fully reconstructed
/// by replaying these events in order.
/// </para>
/// <para>
/// This follows the event sourcing pattern from <c>Excalibur.EventSourcing</c>,
/// adapted for saga-specific state management.
/// </para>
/// </remarks>
public interface ISagaEvent
{
	/// <summary>
	/// Gets the saga instance identifier this event belongs to.
	/// </summary>
	/// <value>The saga identifier.</value>
	string SagaId { get; }

	/// <summary>
	/// Gets the type of saga event.
	/// </summary>
	/// <value>The event type descriptor.</value>
	string EventType { get; }

	/// <summary>
	/// Gets the timestamp when this event occurred.
	/// </summary>
	/// <value>The event timestamp in UTC.</value>
	DateTimeOffset OccurredAt { get; }
}
