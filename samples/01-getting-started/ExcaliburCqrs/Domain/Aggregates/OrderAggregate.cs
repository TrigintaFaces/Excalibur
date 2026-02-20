// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;

using Excalibur.Domain.Model;

using ExcaliburCqrs.Domain.Events;

namespace ExcaliburCqrs.Domain.Aggregates;

/// <summary>
/// Represents the order status in its lifecycle.
/// </summary>
public enum OrderStatus
{
	/// <summary>Order has been created but not confirmed.</summary>
	Created,

	/// <summary>Order has been confirmed by the customer.</summary>
	Confirmed,

	/// <summary>Order has been shipped.</summary>
	Shipped
}

/// <summary>
/// Order aggregate root demonstrating event sourcing patterns.
/// </summary>
/// <remarks>
/// <para>
/// This aggregate demonstrates:
/// <list type="bullet">
/// <item>Event sourcing with RaiseEvent for state changes</item>
/// <item>Pattern matching in ApplyEventInternal using switch expressions</item>
/// <item>Business invariant enforcement</item>
/// <item>Static factory method for creation</item>
/// </list>
/// </para>
/// </remarks>
public class OrderAggregate : AggregateRoot<Guid>
{
	private readonly List<OrderItem> _items = [];

	/// <summary>
	/// Initializes a new instance for rehydration from events.
	/// </summary>
	public OrderAggregate()
	{
	}

	/// <summary>
	/// Initializes a new instance with an identifier.
	/// </summary>
	/// <param name="id">The order identifier.</param>
	public OrderAggregate(Guid id) : base(id)
	{
	}

	/// <summary>Gets the current order status.</summary>
	public OrderStatus Status { get; private set; }

	/// <summary>Gets the order items.</summary>
	public IReadOnlyList<OrderItem> Items => _items;

	/// <summary>Gets the total quantity across all items.</summary>
	public int TotalQuantity => _items.Sum(i => i.Quantity);

	/// <summary>Gets when the order was confirmed (if confirmed).</summary>
	public DateTime? ConfirmedAt { get; private set; }

	/// <summary>Gets when the order was shipped (if shipped).</summary>
	public DateTime? ShippedAt { get; private set; }

	/// <summary>Gets the shipping tracking number (if shipped).</summary>
	public string? TrackingNumber { get; private set; }

	/// <summary>
	/// Creates a new order.
	/// </summary>
	/// <param name="id">The order identifier.</param>
	/// <param name="productId">The initial product identifier.</param>
	/// <param name="quantity">The initial quantity.</param>
	/// <returns>A new order aggregate.</returns>
	public static OrderAggregate Create(Guid id, string productId, int quantity)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(productId);
		ArgumentOutOfRangeException.ThrowIfNegativeOrZero(quantity);

		var order = new OrderAggregate(id);
		order.RaiseEvent(new OrderCreated(id, productId, quantity, order.Version));
		return order;
	}

	/// <summary>
	/// Adds an item to the order.
	/// </summary>
	/// <param name="productId">The product identifier to add.</param>
	/// <param name="quantity">The quantity to add.</param>
	/// <exception cref="InvalidOperationException">Thrown if order is not in Created status.</exception>
	public void AddItem(string productId, int quantity)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(productId);
		ArgumentOutOfRangeException.ThrowIfNegativeOrZero(quantity);

		if (Status != OrderStatus.Created)
		{
			throw new InvalidOperationException(
				$"Cannot add items to order in status '{Status}'. Order must be in Created status.");
		}

		RaiseEvent(new OrderItemAdded(Id, productId, quantity, Version));
	}

	/// <summary>
	/// Confirms the order for processing.
	/// </summary>
	/// <exception cref="InvalidOperationException">Thrown if order cannot be confirmed.</exception>
	public void Confirm()
	{
		if (Status != OrderStatus.Created)
		{
			throw new InvalidOperationException(
				$"Cannot confirm order in status '{Status}'. Order must be in Created status.");
		}

		if (_items.Count == 0)
		{
			throw new InvalidOperationException("Cannot confirm an empty order.");
		}

		RaiseEvent(new OrderConfirmed(Id, Version));
	}

	/// <summary>
	/// Ships the order with a tracking number.
	/// </summary>
	/// <param name="trackingNumber">The shipping tracking number.</param>
	/// <exception cref="InvalidOperationException">Thrown if order cannot be shipped.</exception>
	public void Ship(string trackingNumber)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(trackingNumber);

		if (Status != OrderStatus.Confirmed)
		{
			throw new InvalidOperationException(
				$"Cannot ship order in status '{Status}'. Order must be Confirmed first.");
		}

		RaiseEvent(new OrderShipped(Id, trackingNumber, Version));
	}

	/// <inheritdoc/>
	protected override void ApplyEventInternal(IDomainEvent @event) => _ = @event switch
	{
		OrderCreated e => ApplyOrderCreated(e),
		OrderItemAdded e => ApplyOrderItemAdded(e),
		OrderConfirmed e => ApplyOrderConfirmed(e),
		OrderShipped e => ApplyOrderShipped(e),
		_ => throw new InvalidOperationException($"Unknown event type: {@event.GetType().Name}")
	};

	private bool ApplyOrderCreated(OrderCreated e)
	{
		Id = e.OrderId;
		Status = OrderStatus.Created;
		_items.Add(new OrderItem(e.ProductId, e.Quantity));
		return true;
	}

	private bool ApplyOrderItemAdded(OrderItemAdded e)
	{
		_items.Add(new OrderItem(e.ProductId, e.Quantity));
		return true;
	}

	private bool ApplyOrderConfirmed(OrderConfirmed e)
	{
		Status = OrderStatus.Confirmed;
		ConfirmedAt = e.ConfirmedAt;
		return true;
	}

	private bool ApplyOrderShipped(OrderShipped e)
	{
		Status = OrderStatus.Shipped;
		TrackingNumber = e.TrackingNumber;
		ShippedAt = e.ShippedAt;
		return true;
	}
}

/// <summary>
/// Represents an item in an order.
/// </summary>
/// <param name="ProductId">The product identifier.</param>
/// <param name="Quantity">The quantity ordered.</param>
public sealed record OrderItem(string ProductId, int Quantity);
