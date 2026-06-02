// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: Apache-2.0

using Excalibur.Dispatch;

using Excalibur.Domain.Model;

namespace FullStackAddExcalibur.Domain;

/// <summary>
/// Order aggregate used by the AddExcalibur full-stack sample.
/// </summary>
public sealed class OrderAggregate : AggregateRoot<Guid>
{
	private readonly List<OrderLineItem> _lineItems = [];

	/// <summary>
	/// Initializes a new instance for rehydration from events.
	/// </summary>
	public OrderAggregate()
	{
	}

	/// <summary>
	/// Initializes a new instance with an identifier.
	/// </summary>
	public OrderAggregate(Guid id) : base(id)
	{
	}

	/// <summary>Gets the external order ID (e.g., from a legacy CDC source).</summary>
	public string ExternalOrderId { get; private set; } = string.Empty;

	/// <summary>Gets the customer ID.</summary>
	public Guid CustomerId { get; private set; }

	/// <summary>Gets the customer's external ID (e.g., from a legacy CDC source).</summary>
	public string CustomerExternalId { get; private set; } = string.Empty;

	/// <summary>Gets the current order status.</summary>
	public OrderStatus Status { get; private set; }

	/// <summary>Gets the order total (sum of line items).</summary>
	public decimal TotalAmount { get; private set; }

	/// <summary>Gets the line items in this order.</summary>
	public IReadOnlyList<OrderLineItem> LineItems => _lineItems.AsReadOnly();

	/// <summary>
	/// Creates a new order.
	/// </summary>
	public static OrderAggregate Create(
		Guid id,
		string externalOrderId,
		Guid customerId,
		string customerExternalId,
		DateTime orderDate)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(externalOrderId);
		ArgumentException.ThrowIfNullOrWhiteSpace(customerExternalId);

		var order = new OrderAggregate(id);
		order.RaiseEvent(new OrderCreated(
			id,
			externalOrderId,
			customerId,
			customerExternalId,
			orderDate));
		return order;
	}

	/// <summary>
	/// Adds a line item to this order.
	/// </summary>
	public void AddLineItem(Guid itemId, string productName, int quantity, decimal unitPrice)
	{
		EnsureNotCancelled();
		ArgumentException.ThrowIfNullOrWhiteSpace(productName);
		ArgumentOutOfRangeException.ThrowIfNegativeOrZero(quantity);
		ArgumentOutOfRangeException.ThrowIfNegative(unitPrice);

		RaiseEvent(new OrderLineItemAdded(Id, itemId, productName, quantity, unitPrice));
	}

	/// <summary>
	/// Marks the order as shipped.
	/// </summary>
	public void Ship(DateTime shippedDate)
	{
		EnsureNotCancelled();
		if (Status == OrderStatus.Shipped)
		{
			return;
		}

		RaiseEvent(new OrderShipped(Id, shippedDate));
	}

	/// <summary>
	/// Cancels the order.
	/// </summary>
	public void Cancel(string reason)
	{
		if (Status == OrderStatus.Cancelled)
		{
			return;
		}

		ArgumentException.ThrowIfNullOrWhiteSpace(reason);
		RaiseEvent(new OrderCancelled(Id, reason));
	}

	/// <inheritdoc />
	protected override void ApplyEventInternal(IDomainEvent @event)
	{
		switch (@event)
		{
			case OrderCreated e: Apply(e); break;
			case OrderLineItemAdded e: Apply(e); break;
			case OrderShipped: Status = OrderStatus.Shipped; break;
			case OrderCancelled: Status = OrderStatus.Cancelled; break;
		}
	}

	private void Apply(OrderCreated e)
	{
		Id = e.OrderId;
		ExternalOrderId = e.ExternalOrderId;
		CustomerId = e.CustomerId;
		CustomerExternalId = e.CustomerExternalId;
		Status = OrderStatus.Pending;
	}

	private void Apply(OrderLineItemAdded e)
	{
		_lineItems.Add(new OrderLineItem(e.ItemId, e.ProductName, e.Quantity, e.UnitPrice));
		TotalAmount = _lineItems.Sum(li => li.Quantity * li.UnitPrice);
	}

	private void EnsureNotCancelled()
	{
		if (Status == OrderStatus.Cancelled)
		{
			throw new InvalidOperationException("Cannot modify a cancelled order.");
		}
	}
}

/// <summary>
/// Line item within an order.
/// </summary>
public sealed record OrderLineItem(
	Guid ItemId,
	string ProductName,
	int Quantity,
	decimal UnitPrice);
