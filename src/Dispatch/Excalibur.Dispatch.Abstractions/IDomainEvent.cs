// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Abstractions;

/// <summary>
/// Core interface for all domain events in the system.
/// Combines dispatch messaging capabilities with event sourcing metadata.
/// </summary>
/// <remarks>
/// Domain events capture important business occurrences within an aggregate or entity. They should be named using past-tense verbs (e.g.,
/// OrderPlaced, PaymentReceived). Domain events typically remain within the bounded context and are used for:
/// <list type="bullet">
/// <item> Triggering side effects within the same bounded context </item>
/// <item> Maintaining read model projections </item>
/// <item> Supporting event sourcing patterns </item>
/// </list>
/// </remarks>
public interface IDomainEvent : IDispatchEvent
{
	/// <summary>
	/// Gets the unique identifier for this event instance.
	/// </summary>
	/// <value>The unique identifier for this event instance.</value>
	string EventId { get; }

	/// <summary>
	/// Gets the identifier of the aggregate that raised this event.
	/// </summary>
	/// <value>The identifier of the aggregate that raised this event.</value>
	string AggregateId { get; }

	/// <summary>
	/// Gets the version of the aggregate after this event was applied.
	/// Used for optimistic concurrency and event ordering.
	/// </summary>
	/// <value>The version of the aggregate after this event was applied.</value>
	long Version { get; }

	/// <summary>
	/// Gets the UTC timestamp when this event occurred.
	/// </summary>
	/// <value>The UTC timestamp when this event occurred.</value>
	DateTimeOffset OccurredAt { get; }

	/// <summary>
	/// Gets the event type name for serialization and routing.
	/// Defaults to the class name.
	/// </summary>
	/// <value>The event type name for serialization and routing.</value>
	string EventType { get; }

	/// <summary>
	/// Gets optional metadata for cross-cutting concerns.
	/// Examples: UserId, TenantId, CorrelationId, custom tags.
	/// </summary>
	/// <value>Optional metadata for cross-cutting concerns.</value>
	IDictionary<string, object>? Metadata { get; }
}
