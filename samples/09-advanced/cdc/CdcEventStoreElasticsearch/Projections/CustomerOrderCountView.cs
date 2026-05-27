// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json.Serialization;

using CdcEventStoreElasticsearch.Domain;

using Excalibur.Dispatch;
using Excalibur.EventSourcing;

namespace CdcEventStoreElasticsearch.Projections;

// ============================================================================
// bd-treh2p: IMaterializedViewBuilder<T> example
// ============================================================================
//
// This file demonstrates the materialized-view pattern introduced for CDC
// projections. Compared to inline projections (CustomerProjectionHandler,
// OrderProjectionHandler), materialized-view builders:
//
//   * Own a stable ViewName used for position-tracking in the store
//   * Declare HandledEventTypes so the engine only feeds relevant events
//   * Implement GetViewId(event) to route events to the right view document
//   * Implement Apply(view, event) to evolve state immutably
//
// The framework wires a catch-up loop that:
//   1. Resumes from the last saved position for this ViewName
//   2. Dispatches each matching event to GetViewId + Apply
//   3. Upserts the updated view into the configured IMaterializedViewStore
//      (SQL Server, Elasticsearch, or a consumer-supplied implementation)
//
// ============================================================================

/// <summary>
/// Read-model that tracks each customer's total order count and lifetime spend.
/// </summary>
public sealed class CustomerOrderCountView
{
	/// <summary>Gets or sets the document identifier (equals CustomerId).</summary>
	[JsonPropertyName("id")]
	public string Id { get; set; } = string.Empty;

	/// <summary>Gets or sets the customer identifier.</summary>
	[JsonPropertyName("customerId")]
	public Guid CustomerId { get; set; }

	/// <summary>Gets or sets the total number of orders for this customer.</summary>
	[JsonPropertyName("orderCount")]
	public int OrderCount { get; set; }

	/// <summary>Gets or sets the total spend across all orders.</summary>
	[JsonPropertyName("totalSpend")]
	public decimal TotalSpend { get; set; }

	/// <summary>Gets or sets when the view was last updated.</summary>
	[JsonPropertyName("lastUpdatedAt")]
	public DateTimeOffset LastUpdatedAt { get; set; }
}

/// <summary>
/// IMaterializedViewBuilder{CustomerOrderCountView} implementation showing
/// the view-id + position-tracking pattern established for CDC projections.
/// </summary>
public sealed class CustomerOrderCountViewBuilder
	: IMaterializedViewBuilder<CustomerOrderCountView>
{
	/// <inheritdoc />
	public string ViewName => "customer-order-count";

	/// <inheritdoc />
	public IReadOnlyList<Type> HandledEventTypes { get; } =
		[typeof(OrderCreated), typeof(OrderLineItemAdded), typeof(OrderCancelled)];

	/// <inheritdoc />
	public string? GetViewId(IDomainEvent @event) => @event switch
	{
		// Route all three events by the CustomerId so they roll up into the
		// same materialized document. OrderLineItemAdded + OrderCancelled come
		// from the Order stream, not the Customer stream, so we look up the
		// parent order via the event's AggregateId (a real implementation
		// would cache the Order -> Customer map from OrderCreated events).
		OrderCreated created => created.CustomerId.ToString("D"),
		OrderLineItemAdded line => line.OrderId.ToString("D"),
		OrderCancelled cancelled => cancelled.OrderId.ToString("D"),
		_ => null
	};

	/// <inheritdoc />
	public CustomerOrderCountView Apply(CustomerOrderCountView view, IDomainEvent @event)
	{
		ArgumentNullException.ThrowIfNull(view);
		ArgumentNullException.ThrowIfNull(@event);

		switch (@event)
		{
			case OrderCreated created:
				view.Id = created.CustomerId.ToString("D");
				view.CustomerId = created.CustomerId;
				view.OrderCount += 1;
				break;

			case OrderLineItemAdded line:
				view.TotalSpend += line.Quantity * line.UnitPrice;
				break;

			case OrderCancelled:
				// Cancelled orders still count toward lifetime-but-cancelled metrics.
				// Subclass or extend the view if you want separate counters.
				break;
		}

		view.LastUpdatedAt = @event.OccurredAt;
		return view;
	}
}
