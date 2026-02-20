// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: Apache-2.0

using Excalibur.Dispatch.Abstractions;

using Excalibur.Domain.Model;

using OrderProcessingSample.Domain.Events;

namespace OrderProcessingSample.Domain.Aggregates;

/// <summary>
/// Order aggregate demonstrating event sourcing with full lifecycle management.
/// </summary>
/// <remarks>
/// The order follows a state machine:
/// Created → Validated → PaymentProcessed → Shipped → Completed
/// Any step can transition to: Failed or Cancelled
/// </remarks>
public class OrderAggregate : AggregateRoot<Guid>
{
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

	/// <summary>Gets the customer identifier.</summary>
	public Guid CustomerId { get; private set; }

	/// <summary>Gets the order items.</summary>
	public IReadOnlyList<OrderLineItem> Items { get; private set; } = [];

	/// <summary>Gets the shipping address.</summary>
	public string ShippingAddress { get; private set; } = string.Empty;

	/// <summary>Gets the total order amount.</summary>
	public decimal TotalAmount { get; private set; }

	/// <summary>Gets the current order status.</summary>
	public OrderStatus Status { get; private set; } = OrderStatus.Draft;

	/// <summary>Gets the payment transaction ID (if paid).</summary>
	public string? TransactionId { get; private set; }

	/// <summary>Gets the shipping tracking number (if shipped).</summary>
	public string? TrackingNumber { get; private set; }

	/// <summary>Gets the shipping carrier (if shipped).</summary>
	public string? Carrier { get; private set; }

	/// <summary>Gets the failure/cancellation reason (if applicable).</summary>
	public string? FailureReason { get; private set; }

	/// <summary>Gets when the order was created.</summary>
	public DateTimeOffset? CreatedAt { get; private set; }

	/// <summary>Gets when the order was completed (if completed).</summary>
	public DateTimeOffset? CompletedAt { get; private set; }

	/// <summary>
	/// Creates a new order.
	/// </summary>
	public static OrderAggregate Create(
		Guid orderId,
		Guid customerId,
		IReadOnlyList<OrderLineItem> items,
		string shippingAddress)
	{
		ArgumentNullException.ThrowIfNull(items);
		ArgumentException.ThrowIfNullOrWhiteSpace(shippingAddress);

		if (items.Count == 0)
		{
			throw new ArgumentException("Order must have at least one item", nameof(items));
		}

		var order = new OrderAggregate(orderId);
		order.RaiseEvent(new OrderCreated(orderId, customerId, items, shippingAddress, order.Version));
		return order;
	}

	/// <summary>
	/// Marks the order as validated (inventory and eligibility checks passed).
	/// </summary>
	public void MarkValidated()
	{
		EnsureStatus(OrderStatus.Created, "validate");
		RaiseEvent(new OrderValidated(Id, Version));
	}

	/// <summary>
	/// Marks the order validation as failed.
	/// </summary>
	public void MarkValidationFailed(string reason)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(reason);
		EnsureStatus(OrderStatus.Created, "fail validation");
		RaiseEvent(new OrderValidationFailed(Id, reason, Version));
	}

	/// <summary>
	/// Records successful payment processing.
	/// </summary>
	public void RecordPayment(string transactionId, decimal amount)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(transactionId);
		EnsureStatus(OrderStatus.Validated, "process payment");

		if (amount != TotalAmount)
		{
			throw new InvalidOperationException(
				$"Payment amount {amount:C} does not match order total {TotalAmount:C}");
		}

		RaiseEvent(new PaymentProcessed(Id, transactionId, amount, Version));
	}

	/// <summary>
	/// Records payment failure.
	/// </summary>
	public void RecordPaymentFailure(string reason)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(reason);
		EnsureStatus(OrderStatus.Validated, "record payment failure");
		RaiseEvent(new PaymentFailed(Id, reason, Version));
	}

	/// <summary>
	/// Records order shipment.
	/// </summary>
	public void Ship(string trackingNumber, string carrier)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(trackingNumber);
		ArgumentException.ThrowIfNullOrWhiteSpace(carrier);
		EnsureStatus(OrderStatus.PaymentProcessed, "ship");
		RaiseEvent(new OrderShipped(Id, trackingNumber, carrier, Version));
	}

	/// <summary>
	/// Marks the order as completed (delivered).
	/// </summary>
	public void Complete()
	{
		EnsureStatus(OrderStatus.Shipped, "complete");
		RaiseEvent(new OrderCompleted(Id, Version));
	}

	/// <summary>
	/// Cancels the order.
	/// </summary>
	public void Cancel(string reason)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(reason);

		if (Status is OrderStatus.Completed or OrderStatus.Cancelled)
		{
			throw new InvalidOperationException(
				$"Cannot cancel order in status {Status}");
		}

		RaiseEvent(new OrderCancelled(Id, reason, Version));
	}

	/// <inheritdoc/>
	protected override void ApplyEventInternal(IDomainEvent @event) => _ = @event switch
	{
		OrderCreated e => ApplyOrderCreated(e),
		OrderValidated e => ApplyOrderValidated(e),
		OrderValidationFailed e => ApplyOrderValidationFailed(e),
		PaymentProcessed e => ApplyPaymentProcessed(e),
		PaymentFailed e => ApplyPaymentFailed(e),
		OrderShipped e => ApplyOrderShipped(e),
		OrderCompleted e => ApplyOrderCompleted(e),
		OrderCancelled e => ApplyOrderCancelled(e),
		_ => throw new InvalidOperationException($"Unknown event type: {@event.GetType().Name}")
	};

	private void EnsureStatus(OrderStatus expected, string action)
	{
		if (Status != expected)
		{
			throw new InvalidOperationException(
				$"Cannot {action} order in status {Status}. Expected: {expected}");
		}
	}

	private bool ApplyOrderCreated(OrderCreated e)
	{
		Id = e.OrderId;
		CustomerId = e.CustomerId;
		Items = e.Items;
		ShippingAddress = e.ShippingAddress;
		TotalAmount = e.TotalAmount;
		Status = OrderStatus.Created;
		CreatedAt = e.OccurredAt;
		return true;
	}

	private bool ApplyOrderValidated(OrderValidated e)
	{
		Status = OrderStatus.Validated;
		return true;
	}

	private bool ApplyOrderValidationFailed(OrderValidationFailed e)
	{
		Status = OrderStatus.ValidationFailed;
		FailureReason = e.Reason;
		return true;
	}

	private bool ApplyPaymentProcessed(PaymentProcessed e)
	{
		TransactionId = e.TransactionId;
		Status = OrderStatus.PaymentProcessed;
		return true;
	}

	private bool ApplyPaymentFailed(PaymentFailed e)
	{
		Status = OrderStatus.PaymentFailed;
		FailureReason = e.Reason;
		return true;
	}

	private bool ApplyOrderShipped(OrderShipped e)
	{
		TrackingNumber = e.TrackingNumber;
		Carrier = e.Carrier;
		Status = OrderStatus.Shipped;
		return true;
	}

	private bool ApplyOrderCompleted(OrderCompleted e)
	{
		Status = OrderStatus.Completed;
		CompletedAt = e.OccurredAt;
		return true;
	}

	private bool ApplyOrderCancelled(OrderCancelled e)
	{
		Status = OrderStatus.Cancelled;
		FailureReason = e.Reason;
		return true;
	}
}

/// <summary>
/// Represents the status of an order.
/// </summary>
public enum OrderStatus
{
	/// <summary>Order is being created (not yet submitted).</summary>
	Draft,

	/// <summary>Order has been submitted and is awaiting validation.</summary>
	Created,

	/// <summary>Order has been validated (inventory/eligibility checks passed).</summary>
	Validated,

	/// <summary>Order validation failed.</summary>
	ValidationFailed,

	/// <summary>Payment has been successfully processed.</summary>
	PaymentProcessed,

	/// <summary>Payment processing failed.</summary>
	PaymentFailed,

	/// <summary>Order has been shipped.</summary>
	Shipped,

	/// <summary>Order has been delivered and completed.</summary>
	Completed,

	/// <summary>Order has been cancelled.</summary>
	Cancelled
}
