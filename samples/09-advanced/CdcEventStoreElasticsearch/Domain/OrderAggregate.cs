// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: Apache-2.0

using Excalibur.Dispatch.Abstractions;

using Excalibur.Domain.Model;

namespace CdcEventStoreElasticsearch.Domain;

/// <summary>
/// Order aggregate demonstrating event sourcing with SQL Server.
/// This aggregate receives commands translated from CDC events via the Anti-Corruption Layer.
/// </summary>
/// <remarks>
/// <para>
/// Key design decision: OrderItems are NOT separate aggregates. They are part of
/// the OrderAggregate to maintain transactional consistency. When an OrderItem CDC
/// event arrives, we load the parent Order, modify it, and save.
/// </para>
/// </remarks>
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

	/// <summary>Gets the external order ID from the legacy system.</summary>
	public string ExternalOrderId { get; private set; } = string.Empty;

	/// <summary>Gets the customer ID who placed the order.</summary>
	public Guid CustomerId { get; private set; }

	/// <summary>Gets the customer's external ID from the legacy system.</summary>
	public string CustomerExternalId { get; private set; } = string.Empty;

	/// <summary>Gets the order status.</summary>
	public OrderStatus Status { get; private set; }

	/// <summary>Gets the total order amount.</summary>
	public decimal TotalAmount { get; private set; }

	/// <summary>Gets the order line items.</summary>
	public IReadOnlyList<OrderLineItem> LineItems => _lineItems.AsReadOnly();

	/// <summary>Gets the order date.</summary>
	public DateTime OrderDate { get; private set; }

	/// <summary>Gets the shipped date if shipped.</summary>
	public DateTime? ShippedDate { get; private set; }

	/// <summary>Gets the cancellation reason if cancelled.</summary>
	public string? CancellationReason { get; private set; }

	/// <summary>Gets when the order was created.</summary>
	public DateTimeOffset CreatedAt { get; private set; }

	/// <summary>Gets when the order was last updated.</summary>
	public DateTimeOffset? UpdatedAt { get; private set; }

	/// <summary>
	/// Creates a new order from CDC data.
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
			orderDate,
			order.Version));
		return order;
	}

	/// <summary>
	/// Adds a line item to the order.
	/// </summary>
	public void AddLineItem(
		Guid itemId,
		string externalItemId,
		string productName,
		int quantity,
		decimal unitPrice)
	{
		EnsureNotCancelled();
		ArgumentException.ThrowIfNullOrWhiteSpace(productName);
		ArgumentOutOfRangeException.ThrowIfNegativeOrZero(quantity);
		ArgumentOutOfRangeException.ThrowIfNegative(unitPrice);

		// Check for duplicate
		if (_lineItems.Any(li => li.ItemId == itemId || li.ExternalItemId == externalItemId))
		{
			return; // Idempotent - already exists
		}

		RaiseEvent(new OrderLineItemAdded(
			Id,
			itemId,
			externalItemId,
			productName,
			quantity,
			unitPrice,
			Version));
	}

	/// <summary>
	/// Updates an existing line item's quantity.
	/// </summary>
	public void UpdateLineItem(Guid itemId, int newQuantity)
	{
		EnsureNotCancelled();
		ArgumentOutOfRangeException.ThrowIfNegativeOrZero(newQuantity);

		var existing = _lineItems.FirstOrDefault(li => li.ItemId == itemId);
		if (existing is null)
		{
			return; // Item not found, ignore
		}

		if (existing.Quantity == newQuantity)
		{
			return; // No change
		}

		RaiseEvent(new OrderLineItemUpdated(
			Id,
			itemId,
			existing.Quantity,
			newQuantity,
			Version));
	}

	/// <summary>
	/// Removes a line item from the order.
	/// </summary>
	public void RemoveLineItem(Guid itemId)
	{
		EnsureNotCancelled();

		var existing = _lineItems.FirstOrDefault(li => li.ItemId == itemId);
		if (existing is null)
		{
			return; // Item not found, ignore
		}

		RaiseEvent(new OrderLineItemRemoved(
			Id,
			itemId,
			existing.ProductName,
			existing.Quantity,
			existing.UnitPrice,
			Version));
	}

	/// <summary>
	/// Updates the order status.
	/// </summary>
	public void UpdateStatus(OrderStatus newStatus)
	{
		EnsureNotCancelled();

		if (Status == newStatus)
		{
			return; // No change
		}

		RaiseEvent(new OrderStatusUpdated(Id, Status, newStatus, Version));
	}

	/// <summary>
	/// Ships the order.
	/// </summary>
	public void Ship(DateTime shippedDate)
	{
		EnsureNotCancelled();

		if (Status == OrderStatus.Shipped || Status == OrderStatus.Delivered)
		{
			return; // Already shipped
		}

		RaiseEvent(new OrderShipped(Id, shippedDate, Version));
	}

	/// <summary>
	/// Marks the order as delivered.
	/// </summary>
	public void Deliver(DateTime deliveredDate)
	{
		if (Status == OrderStatus.Cancelled)
		{
			throw new InvalidOperationException("Cannot deliver a cancelled order");
		}

		if (Status == OrderStatus.Delivered)
		{
			return; // Already delivered
		}

		RaiseEvent(new OrderDelivered(Id, deliveredDate, Version));
	}

	/// <summary>
	/// Cancels the order.
	/// </summary>
	public void Cancel(string reason)
	{
		if (Status == OrderStatus.Cancelled)
		{
			return; // Already cancelled
		}

		if (Status == OrderStatus.Delivered)
		{
			throw new InvalidOperationException("Cannot cancel a delivered order");
		}

		RaiseEvent(new OrderCancelled(Id, reason, Version));
	}

	/// <inheritdoc/>
	protected override void ApplyEventInternal(IDomainEvent @event) => _ = @event switch
	{
		OrderCreated e => ApplyOrderCreated(e),
		OrderLineItemAdded e => ApplyLineItemAdded(e),
		OrderLineItemUpdated e => ApplyLineItemUpdated(e),
		OrderLineItemRemoved e => ApplyLineItemRemoved(e),
		OrderStatusUpdated e => ApplyStatusUpdated(e),
		OrderShipped e => ApplyShipped(e),
		OrderDelivered e => ApplyDelivered(e),
		OrderCancelled e => ApplyCancelled(e),
		_ => throw new InvalidOperationException($"Unknown event type: {@event.GetType().Name}")
	};

	private void EnsureNotCancelled()
	{
		if (Status == OrderStatus.Cancelled)
		{
			throw new InvalidOperationException("Order is cancelled");
		}
	}

	private void RecalculateTotalAmount()
	{
		TotalAmount = _lineItems.Sum(li => li.Quantity * li.UnitPrice);
	}

	private bool ApplyOrderCreated(OrderCreated e)
	{
		Id = e.OrderId;
		ExternalOrderId = e.ExternalOrderId;
		CustomerId = e.CustomerId;
		CustomerExternalId = e.CustomerExternalId;
		OrderDate = e.OrderDate;
		Status = OrderStatus.Pending;
		CreatedAt = e.OccurredAt;
		return true;
	}

	private bool ApplyLineItemAdded(OrderLineItemAdded e)
	{
		_lineItems.Add(new OrderLineItem(
			e.ItemId,
			e.ExternalItemId,
			e.ProductName,
			e.Quantity,
			e.UnitPrice));
		RecalculateTotalAmount();
		UpdatedAt = e.OccurredAt;
		return true;
	}

	private bool ApplyLineItemUpdated(OrderLineItemUpdated e)
	{
		var item = _lineItems.FirstOrDefault(li => li.ItemId == e.ItemId);
		if (item is not null)
		{
			var index = _lineItems.IndexOf(item);
			_lineItems[index] = item with { Quantity = e.NewQuantity };
			RecalculateTotalAmount();
		}

		UpdatedAt = e.OccurredAt;
		return true;
	}

	private bool ApplyLineItemRemoved(OrderLineItemRemoved e)
	{
		var item = _lineItems.FirstOrDefault(li => li.ItemId == e.ItemId);
		if (item is not null)
		{
			_ = _lineItems.Remove(item);
			RecalculateTotalAmount();
		}

		UpdatedAt = e.OccurredAt;
		return true;
	}

	private bool ApplyStatusUpdated(OrderStatusUpdated e)
	{
		Status = e.NewStatus;
		UpdatedAt = e.OccurredAt;
		return true;
	}

	private bool ApplyShipped(OrderShipped e)
	{
		Status = OrderStatus.Shipped;
		ShippedDate = e.ShippedDate;
		UpdatedAt = e.OccurredAt;
		return true;
	}

	private bool ApplyDelivered(OrderDelivered e)
	{
		Status = OrderStatus.Delivered;
		UpdatedAt = e.OccurredAt;
		return true;
	}

	private bool ApplyCancelled(OrderCancelled e)
	{
		Status = OrderStatus.Cancelled;
		CancellationReason = e.Reason;
		UpdatedAt = e.OccurredAt;
		return true;
	}
}

/// <summary>
/// Order status enumeration.
/// </summary>
public enum OrderStatus
{
	/// <summary>Order is pending confirmation.</summary>
	Pending = 0,

	/// <summary>Order is confirmed.</summary>
	Confirmed = 1,

	/// <summary>Order has been shipped.</summary>
	Shipped = 2,

	/// <summary>Order has been delivered.</summary>
	Delivered = 3,

	/// <summary>Order has been cancelled.</summary>
	Cancelled = 4
}

/// <summary>
/// Represents a line item in an order.
/// </summary>
public sealed record OrderLineItem(
	Guid ItemId,
	string ExternalItemId,
	string ProductName,
	int Quantity,
	decimal UnitPrice)
{
	/// <summary>Gets the line total (Quantity * UnitPrice).</summary>
	public decimal LineTotal => Quantity * UnitPrice;
}
