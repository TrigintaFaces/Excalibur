// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;

using Excalibur.Domain.Model;

using MultiProviderQueueProcessor.Events;

namespace MultiProviderQueueProcessor.Domain;

/// <summary>
/// Order status enumeration.
/// </summary>
public enum OrderStatus
{
	Created,
	Submitted,
	Shipped,
	Cancelled,
}

/// <summary>
/// Order aggregate root demonstrating event sourcing patterns.
/// </summary>
public sealed class Order : AggregateRoot
{
	private readonly List<OrderItem> _items = [];

	public string CustomerId { get; private set; } = string.Empty;
	public decimal TotalAmount { get; private set; }
	public string Currency { get; private set; } = "USD";
	public OrderStatus Status { get; private set; }
	public IReadOnlyList<OrderItem> Items => _items.AsReadOnly();
	public string? TrackingNumber { get; private set; }
	public string? Carrier { get; private set; }
	public string? CancellationReason { get; private set; }

	/// <summary>
	/// Creates a new order.
	/// </summary>
	public static Order Create(string orderId, string customerId, decimal totalAmount, string currency = "USD")
	{
		var order = new Order();
		order.RaiseEvent(new OrderCreatedEvent
		{
			AggregateId = orderId,
			CustomerId = customerId,
			TotalAmount = totalAmount,
			Currency = currency,
		});
		return order;
	}

	/// <summary>
	/// Adds an item to the order.
	/// </summary>
	public void AddItem(string productId, string productName, int quantity, decimal unitPrice)
	{
		if (Status != OrderStatus.Created)
		{
			throw new InvalidOperationException($"Cannot add items to order in {Status} status");
		}

		RaiseEvent(new OrderItemAddedEvent
		{
			AggregateId = Id,
			ProductId = productId,
			ProductName = productName,
			Quantity = quantity,
			UnitPrice = unitPrice,
		});
	}

	/// <summary>
	/// Submits the order for processing.
	/// </summary>
	public void Submit()
	{
		if (Status != OrderStatus.Created)
		{
			throw new InvalidOperationException($"Cannot submit order in {Status} status");
		}

		if (_items.Count == 0)
		{
			throw new InvalidOperationException("Cannot submit an empty order");
		}

		RaiseEvent(new OrderSubmittedEvent { AggregateId = Id, SubmittedAt = DateTime.UtcNow, });
	}

	/// <summary>
	/// Marks the order as shipped.
	/// </summary>
	public void Ship(string trackingNumber, string carrier)
	{
		if (Status != OrderStatus.Submitted)
		{
			throw new InvalidOperationException($"Cannot ship order in {Status} status");
		}

		RaiseEvent(new OrderShippedEvent
		{
			AggregateId = Id,
			TrackingNumber = trackingNumber,
			Carrier = carrier,
			ShippedAt = DateTime.UtcNow,
		});
	}

	/// <summary>
	/// Cancels the order.
	/// </summary>
	public void Cancel(string reason)
	{
		if (Status == OrderStatus.Shipped || Status == OrderStatus.Cancelled)
		{
			throw new InvalidOperationException($"Cannot cancel order in {Status} status");
		}

		RaiseEvent(new OrderCancelledEvent { AggregateId = Id, Reason = reason, CancelledAt = DateTime.UtcNow, });
	}

	/// <inheritdoc />
	protected override void ApplyEventInternal(IDomainEvent @event) => _ = @event switch
	{
		OrderCreatedEvent e => Apply(e),
		OrderItemAddedEvent e => Apply(e),
		OrderSubmittedEvent => ApplySubmitted(),
		OrderShippedEvent e => Apply(e),
		OrderCancelledEvent e => Apply(e),
		_ => throw new InvalidOperationException($"Unknown event type: {@event.GetType().Name}"),
	};

	private bool Apply(OrderCreatedEvent e)
	{
		Id = e.AggregateId;
		CustomerId = e.CustomerId;
		TotalAmount = e.TotalAmount;
		Currency = e.Currency;
		Status = OrderStatus.Created;
		return true;
	}

	private bool Apply(OrderItemAddedEvent e)
	{
		_items.Add(new OrderItem(e.ProductId, e.ProductName, e.Quantity, e.UnitPrice));
		TotalAmount += e.Quantity * e.UnitPrice;
		return true;
	}

	private bool ApplySubmitted()
	{
		Status = OrderStatus.Submitted;
		return true;
	}

	private bool Apply(OrderShippedEvent e)
	{
		TrackingNumber = e.TrackingNumber;
		Carrier = e.Carrier;
		Status = OrderStatus.Shipped;
		return true;
	}

	private bool Apply(OrderCancelledEvent e)
	{
		CancellationReason = e.Reason;
		Status = OrderStatus.Cancelled;
		return true;
	}
}

/// <summary>
/// Order line item.
/// </summary>
public sealed record OrderItem(
	string ProductId,
	string ProductName,
	int Quantity,
	decimal UnitPrice)
{
	public decimal Total => Quantity * UnitPrice;
}
