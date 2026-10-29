// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch;
using Excalibur.Domain.Model;
using Excalibur.EventSourcing;

namespace Excalibur.Examples.TwoProcessProjections;

/// <summary>An order placed by a customer.</summary>
/// <remarks>
/// The stored identity is DECLARED, not derived from the type. A derived name changes when the
/// namespace, assembly or assembly version changes, and every event already stored under the old name
/// becomes unreadable — so the framework refuses to start without this rather than letting a rename
/// silently orphan history. Choose it once; rename the type later with <c>[MessageNameAlias]</c>.
/// </remarks>
[MessageName("Sample.TwoProcessProjections.OrderPlaced")]
public sealed record OrderPlaced : DomainEvent
{
	/// <summary>Gets the order this event belongs to.</summary>
	/// <remarks>
	/// The order id is the event's OWN business data. A domain event carries no aggregate id and no
	/// stream version: the aggregate id is a parameter to the event store, and the version is assigned
	/// by the store at append time and read back on <c>StoredEvent.Version</c>. Deriving
	/// <c>DomainEvent</c> supplies <c>EventId</c>, <c>OccurredAt</c> and <c>Metadata</c>.
	/// </remarks>
	public string OrderId { get; init; } = string.Empty;

	/// <summary>Gets the customer the order belongs to.</summary>
	public string CustomerId { get; init; } = string.Empty;

	/// <summary>Gets the order total.</summary>
	public decimal Amount { get; init; }
}

/// <summary>A line added to an existing order.</summary>
[MessageName("Sample.TwoProcessProjections.OrderLineAdded")]
public sealed record OrderLineAdded : DomainEvent
{
	/// <summary>Gets the order this event belongs to.</summary>
	public string OrderId { get; init; } = string.Empty;

	/// <summary>Gets the product added.</summary>
	public string Sku { get; init; } = string.Empty;

	/// <summary>Gets the line amount.</summary>
	public decimal Amount { get; init; }
}

/// <summary>The write model.</summary>
public sealed class Order : AggregateRoot
{
	/// <summary>Gets the customer the order belongs to.</summary>
	public string CustomerId { get; private set; } = string.Empty;

	/// <summary>Gets the running total.</summary>
	public decimal Total { get; private set; }

	/// <summary>Places a new order.</summary>
	/// <param name="orderId">The order identifier.</param>
	/// <param name="customerId">The customer identifier.</param>
	/// <param name="amount">The opening amount.</param>
	/// <returns>The new order.</returns>
	public static Order Place(string orderId, string customerId, decimal amount)
	{
		var order = new Order();
		order.RaiseEvent(new OrderPlaced { OrderId = orderId, CustomerId = customerId, Amount = amount });
		return order;
	}

	/// <summary>Adds a line.</summary>
	/// <param name="sku">The product.</param>
	/// <param name="amount">The line amount.</param>
	public void AddLine(string sku, decimal amount) =>
		RaiseEvent(new OrderLineAdded { OrderId = Id, Sku = sku, Amount = amount });

	/// <inheritdoc />
	protected override bool ApplyEventInternal(IDomainEvent @event)
	{
		switch (@event)
		{
			case OrderPlaced placed:
				Id = placed.OrderId;
				CustomerId = placed.CustomerId;
				Total = placed.Amount;
				return true;

			case OrderLineAdded line:
				Total += line.Amount;
				return true;

			default:
				return false;
		}
	}
}

/// <summary>
/// The QUERYABLE read model — a list screen, so it has to be stored and cannot be computed per read.
/// </summary>
[Excalibur.EventSourcing.ProjectionVersion(1)]
public sealed class OrderSummary
{
	/// <summary>Gets or sets the order identifier, copied from the event envelope.</summary>
	public string Id { get; set; } = string.Empty;

	/// <summary>Gets or sets the customer.</summary>
	public string CustomerId { get; set; } = string.Empty;

	/// <summary>Gets or sets the total.</summary>
	public decimal Total { get; set; }

	/// <summary>Gets or sets how many events have been folded in.</summary>
	public int EventCount { get; set; }
}

/// <summary>
/// The READ-BY-ID read model, built on demand from the aggregate's own stream.
/// </summary>
/// <remarks>
/// Registered <c>Ephemeral</c>, so nothing stores it and nothing has to apply it: it is the fold of
/// that order's events, computed when it is read. There is no staleness to reason about, no checkpoint,
/// and no host — which is why it is the right shape for a detail screen and the wrong shape for a list.
/// </remarks>
public sealed class OrderDetail
{
	/// <summary>Gets or sets the order identifier.</summary>
	public string Id { get; set; } = string.Empty;

	/// <summary>Gets or sets the total.</summary>
	public decimal Total { get; set; }

	/// <summary>Gets or sets the number of lines.</summary>
	public int Lines { get; set; }
}
