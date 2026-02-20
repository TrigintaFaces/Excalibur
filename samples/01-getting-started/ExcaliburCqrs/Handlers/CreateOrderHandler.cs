// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions.Delivery;

using Excalibur.EventSourcing.Abstractions;

using ExcaliburCqrs.Domain.Aggregates;
using ExcaliburCqrs.Messages;

namespace ExcaliburCqrs.Handlers;

/// <summary>
/// Handles CreateOrderCommand by creating a new order aggregate.
/// </summary>
public sealed class CreateOrderHandler : IActionHandler<CreateOrderCommand, Guid>
{
	private readonly IEventSourcedRepository<OrderAggregate, Guid> _repository;

	public CreateOrderHandler(IEventSourcedRepository<OrderAggregate, Guid> repository)
	{
		_repository = repository ?? throw new ArgumentNullException(nameof(repository));
	}

	public async Task<Guid> HandleAsync(CreateOrderCommand action, CancellationToken cancellationToken)
	{
		var orderId = Guid.NewGuid();

		Console.WriteLine($"[CreateOrderHandler] Creating order {orderId}");
		Console.WriteLine($"  Product: {action.ProductId}, Quantity: {action.Quantity}");

		// Create the aggregate (raises OrderCreated event)
		var order = OrderAggregate.Create(orderId, action.ProductId, action.Quantity);

		// Save to event store
		await _repository.SaveAsync(order, cancellationToken).ConfigureAwait(false);

		Console.WriteLine($"  --> Order created and saved. Version: {order.Version}");
		Console.WriteLine($"  --> Uncommitted events saved: {order.GetUncommittedEvents().Count}");

		return orderId;
	}
}

/// <summary>
/// Handles AddOrderItemCommand by adding an item to an existing order.
/// </summary>
public sealed class AddOrderItemHandler : IActionHandler<AddOrderItemCommand>
{
	private readonly IEventSourcedRepository<OrderAggregate, Guid> _repository;

	public AddOrderItemHandler(IEventSourcedRepository<OrderAggregate, Guid> repository)
	{
		_repository = repository ?? throw new ArgumentNullException(nameof(repository));
	}

	public async Task HandleAsync(AddOrderItemCommand action, CancellationToken cancellationToken)
	{
		Console.WriteLine($"[AddOrderItemHandler] Adding item to order {action.OrderId}");
		Console.WriteLine($"  Product: {action.ProductId}, Quantity: {action.Quantity}");

		// Load aggregate from event store
		var order = await _repository.GetByIdAsync(action.OrderId, cancellationToken).ConfigureAwait(false);

		if (order is null)
		{
			throw new InvalidOperationException($"Order {action.OrderId} not found.");
		}

		// Execute domain logic (raises OrderItemAdded event)
		order.AddItem(action.ProductId, action.Quantity);

		// Save new events
		await _repository.SaveAsync(order, cancellationToken).ConfigureAwait(false);

		Console.WriteLine($"  --> Item added. Total items: {order.Items.Count}, Total quantity: {order.TotalQuantity}");
	}
}

/// <summary>
/// Handles ConfirmOrderCommand by confirming an order.
/// </summary>
public sealed class ConfirmOrderHandler : IActionHandler<ConfirmOrderCommand>
{
	private readonly IEventSourcedRepository<OrderAggregate, Guid> _repository;

	public ConfirmOrderHandler(IEventSourcedRepository<OrderAggregate, Guid> repository)
	{
		_repository = repository ?? throw new ArgumentNullException(nameof(repository));
	}

	public async Task HandleAsync(ConfirmOrderCommand action, CancellationToken cancellationToken)
	{
		Console.WriteLine($"[ConfirmOrderHandler] Confirming order {action.OrderId}");

		var order = await _repository.GetByIdAsync(action.OrderId, cancellationToken).ConfigureAwait(false);

		if (order is null)
		{
			throw new InvalidOperationException($"Order {action.OrderId} not found.");
		}

		// Execute domain logic (raises OrderConfirmed event)
		order.Confirm();

		await _repository.SaveAsync(order, cancellationToken).ConfigureAwait(false);

		Console.WriteLine($"  --> Order confirmed at {order.ConfirmedAt}");
	}
}

/// <summary>
/// Handles ShipOrderCommand by shipping an order.
/// </summary>
public sealed class ShipOrderHandler : IActionHandler<ShipOrderCommand>
{
	private readonly IEventSourcedRepository<OrderAggregate, Guid> _repository;

	public ShipOrderHandler(IEventSourcedRepository<OrderAggregate, Guid> repository)
	{
		_repository = repository ?? throw new ArgumentNullException(nameof(repository));
	}

	public async Task HandleAsync(ShipOrderCommand action, CancellationToken cancellationToken)
	{
		Console.WriteLine($"[ShipOrderHandler] Shipping order {action.OrderId}");
		Console.WriteLine($"  Tracking: {action.TrackingNumber}");

		var order = await _repository.GetByIdAsync(action.OrderId, cancellationToken).ConfigureAwait(false);

		if (order is null)
		{
			throw new InvalidOperationException($"Order {action.OrderId} not found.");
		}

		// Execute domain logic (raises OrderShipped event)
		order.Ship(action.TrackingNumber);

		await _repository.SaveAsync(order, cancellationToken).ConfigureAwait(false);

		Console.WriteLine($"  --> Order shipped at {order.ShippedAt}");
	}
}

/// <summary>
/// Handles GetOrderQuery by loading and displaying order details.
/// </summary>
public sealed class GetOrderHandler : IDocumentHandler<GetOrderQuery>
{
	private readonly IEventSourcedRepository<OrderAggregate, Guid> _repository;

	public GetOrderHandler(IEventSourcedRepository<OrderAggregate, Guid> repository)
	{
		_repository = repository ?? throw new ArgumentNullException(nameof(repository));
	}

	public async Task HandleAsync(GetOrderQuery document, CancellationToken cancellationToken)
	{
		Console.WriteLine($"[GetOrderHandler] Loading order {document.OrderId}");

		var order = await _repository.GetByIdAsync(document.OrderId, cancellationToken).ConfigureAwait(false);

		if (order is null)
		{
			Console.WriteLine("  --> Order not found.");
			return;
		}

		Console.WriteLine($"  Order ID: {order.Id}");
		Console.WriteLine($"  Status: {order.Status}");
		Console.WriteLine($"  Version: {order.Version}");
		Console.WriteLine($"  Items ({order.Items.Count}):");

		foreach (var item in order.Items)
		{
			Console.WriteLine($"    - {item.ProductId} x{item.Quantity}");
		}

		Console.WriteLine($"  Total Quantity: {order.TotalQuantity}");

		if (order.ConfirmedAt.HasValue)
		{
			Console.WriteLine($"  Confirmed At: {order.ConfirmedAt}");
		}

		if (order.ShippedAt.HasValue)
		{
			Console.WriteLine($"  Shipped At: {order.ShippedAt}");
			Console.WriteLine($"  Tracking: {order.TrackingNumber}");
		}
	}
}
