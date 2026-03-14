// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: Apache-2.0

using Excalibur.Dispatch.Abstractions;
using Excalibur.Domain.Model;

using EnterpriseOrderProcessing.Domain.Events;

namespace EnterpriseOrderProcessing.Domain;

public enum OrderStatus
{
	Draft,
	Submitted,
	Shipped,
	Cancelled
}

public sealed class OrderLine
{
	public required string ProductId { get; init; }
	public required int Quantity { get; init; }
	public required decimal UnitPrice { get; init; }
	public decimal Total => Quantity * UnitPrice;
}

public sealed class OrderAggregate : AggregateRoot<Guid>
{
	private readonly List<OrderLine> _lines = [];

	public override string AggregateType => "Order";

	public Guid CustomerId { get; private set; }
	public string CustomerName { get; private set; } = string.Empty;
	public OrderStatus Status { get; private set; }
	public IReadOnlyList<OrderLine> Lines => _lines;
	public string? TrackingNumber { get; private set; }
	public string? CancellationReason { get; private set; }
	public decimal Total => _lines.Sum(l => l.Total);

	public void Create(Guid orderId, Guid customerId, string customerName)
	{
		if (Status != OrderStatus.Draft || Version > 0)
			throw new InvalidOperationException("Order already created.");

		RaiseEvent(new OrderCreated(orderId, customerId, customerName));
	}

	public void AddLine(string productId, int quantity, decimal unitPrice)
	{
		if (Status != OrderStatus.Draft)
			throw new InvalidOperationException("Cannot add lines to a non-draft order.");

		ArgumentException.ThrowIfNullOrWhiteSpace(productId);
		ArgumentOutOfRangeException.ThrowIfNegativeOrZero(quantity);
		ArgumentOutOfRangeException.ThrowIfNegativeOrZero(unitPrice);

		RaiseEvent(new OrderLineAdded(Id, productId, quantity, unitPrice));
	}

	public void Submit()
	{
		if (Status != OrderStatus.Draft)
			throw new InvalidOperationException("Only draft orders can be submitted.");
		if (_lines.Count == 0)
			throw new InvalidOperationException("Cannot submit an empty order.");

		RaiseEvent(new OrderSubmitted(Id));
	}

	public void Ship(string trackingNumber)
	{
		if (Status != OrderStatus.Submitted)
			throw new InvalidOperationException("Only submitted orders can be shipped.");

		ArgumentException.ThrowIfNullOrWhiteSpace(trackingNumber);

		RaiseEvent(new OrderShipped(Id, trackingNumber));
	}

	public void Cancel(string reason)
	{
		if (Status is OrderStatus.Shipped or OrderStatus.Cancelled)
			throw new InvalidOperationException($"Cannot cancel an order in {Status} status.");

		ArgumentException.ThrowIfNullOrWhiteSpace(reason);

		RaiseEvent(new OrderCancelled(Id, reason));
	}

	protected override void ApplyEventInternal(IDomainEvent @event) => _ = @event switch
	{
		OrderCreated e => Apply(e),
		OrderLineAdded e => Apply(e),
		OrderSubmitted => ApplySubmitted(),
		OrderShipped e => Apply(e),
		OrderCancelled e => Apply(e),
		_ => throw new InvalidOperationException($"Unknown event type: {@event.GetType().Name}")
	};

	private bool Apply(OrderCreated e)
	{
		Id = e.OrderId;
		CustomerId = e.CustomerId;
		CustomerName = e.CustomerName;
		Status = OrderStatus.Draft;
		return true;
	}

	private bool Apply(OrderLineAdded e)
	{
		_lines.Add(new OrderLine
		{
			ProductId = e.ProductId,
			Quantity = e.Quantity,
			UnitPrice = e.UnitPrice
		});
		return true;
	}

	private bool ApplySubmitted()
	{
		Status = OrderStatus.Submitted;
		return true;
	}

	private bool Apply(OrderShipped e)
	{
		Status = OrderStatus.Shipped;
		TrackingNumber = e.TrackingNumber;
		return true;
	}

	private bool Apply(OrderCancelled e)
	{
		Status = OrderStatus.Cancelled;
		CancellationReason = e.Reason;
		return true;
	}
}
