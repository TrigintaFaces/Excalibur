// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions.Delivery;

using OrderProcessingSample.Domain.Aggregates;
using OrderProcessingSample.Domain.Commands;

namespace OrderProcessingSample.Handlers;

// ============================================================================
// Order Command Handlers
// ============================================================================
// These handlers process commands and update the aggregate state.
// They use the in-memory store for simplicity (production would use repository).

/// <summary>
/// Handles CreateOrderCommand.
/// </summary>
public sealed class CreateOrderHandler : IActionHandler<CreateOrderCommand>
{
	private readonly InMemoryOrderStore _store;

	public CreateOrderHandler(InMemoryOrderStore store)
	{
		_store = store;
	}

	public Task HandleAsync(CreateOrderCommand action, CancellationToken cancellationToken)
	{
		var orderId = Guid.NewGuid();
		Console.WriteLine($"  [CreateOrderHandler] Creating order {orderId.ToString()[..8]}");

		var order = OrderAggregate.Create(
			orderId,
			action.CustomerId,
			action.Items,
			action.ShippingAddress);

		_store.Save(order);
		order.MarkEventsAsCommitted();

		Console.WriteLine($"  [CreateOrderHandler] Order created with {action.Items.Count} items");
		Console.WriteLine($"  [CreateOrderHandler] Total: {order.TotalAmount:C}");

		return Task.CompletedTask;
	}
}

/// <summary>
/// Handles CancelOrderCommand.
/// </summary>
public sealed class CancelOrderHandler : IActionHandler<CancelOrderCommand>
{
	private readonly InMemoryOrderStore _store;

	public CancelOrderHandler(InMemoryOrderStore store)
	{
		_store = store;
	}

	public Task HandleAsync(CancelOrderCommand action, CancellationToken cancellationToken)
	{
		Console.WriteLine($"  [CancelOrderHandler] Cancelling order {action.OrderId.ToString()[..8]}");

		var order = _store.GetById(action.OrderId)
					?? throw new InvalidOperationException($"Order {action.OrderId} not found");

		order.Cancel(action.Reason);
		_store.Save(order);
		order.MarkEventsAsCommitted();

		Console.WriteLine($"  [CancelOrderHandler] Order cancelled. Reason: {action.Reason}");

		return Task.CompletedTask;
	}
}

/// <summary>
/// Handles ConfirmDeliveryCommand.
/// </summary>
public sealed class ConfirmDeliveryHandler : IActionHandler<ConfirmDeliveryCommand>
{
	private readonly InMemoryOrderStore _store;

	public ConfirmDeliveryHandler(InMemoryOrderStore store)
	{
		_store = store;
	}

	public Task HandleAsync(ConfirmDeliveryCommand action, CancellationToken cancellationToken)
	{
		Console.WriteLine($"  [ConfirmDeliveryHandler] Confirming delivery for order {action.OrderId.ToString()[..8]}");

		var order = _store.GetById(action.OrderId)
					?? throw new InvalidOperationException($"Order {action.OrderId} not found");

		order.Complete();
		_store.Save(order);
		order.MarkEventsAsCommitted();

		Console.WriteLine($"  [ConfirmDeliveryHandler] Order marked as completed");

		return Task.CompletedTask;
	}
}

// ============================================================================
// In-Memory Order Store
// ============================================================================
// Simple in-memory store for demonstration. In production, use IEventSourcedRepository.

/// <summary>
/// In-memory store for order aggregates (demonstration purposes only).
/// </summary>
public sealed class InMemoryOrderStore
{
	private readonly Dictionary<Guid, OrderAggregate> _orders = [];
	private readonly object _lock = new();

	/// <summary>
	/// Gets an order by ID.
	/// </summary>
	public OrderAggregate? GetById(Guid id)
	{
		lock (_lock)
		{
			return _orders.TryGetValue(id, out var order) ? order : null;
		}
	}

	/// <summary>
	/// Saves an order.
	/// </summary>
	public void Save(OrderAggregate order)
	{
		lock (_lock)
		{
			_orders[order.Id] = order;
		}
	}

	/// <summary>
	/// Gets all orders.
	/// </summary>
	public IReadOnlyList<OrderAggregate> GetAll()
	{
		lock (_lock)
		{
			return _orders.Values.ToList();
		}
	}

	/// <summary>
	/// Gets the most recently created order.
	/// </summary>
	public OrderAggregate? GetMostRecent()
	{
		lock (_lock)
		{
			return _orders.Values
				.OrderByDescending(o => o.CreatedAt)
				.FirstOrDefault();
		}
	}
}
