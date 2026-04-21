// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions.Delivery;
using Excalibur.EventSourcing.Abstractions;

using OrderProcessingSample.Domain.Aggregates;
using OrderProcessingSample.Domain.Commands;

namespace OrderProcessingSample.Handlers;

// ============================================================================
// Order Command Handlers
// ============================================================================
// These handlers process commands and update aggregate state through
// IEventSourcedRepository, the correct production pattern for event sourcing.

/// <summary>
/// Handles CreateOrderCommand.
/// </summary>
public sealed class CreateOrderHandler : IActionHandler<CreateOrderCommand>
{
	private readonly IEventSourcedRepository<OrderAggregate, Guid> _repository;

	public CreateOrderHandler(IEventSourcedRepository<OrderAggregate, Guid> repository)
	{
		_repository = repository ?? throw new ArgumentNullException(nameof(repository));
	}

	public async Task HandleAsync(CreateOrderCommand action, CancellationToken cancellationToken)
	{
		Console.WriteLine($"  [CreateOrderHandler] Creating order {action.OrderId.ToString()[..8]}");

		var order = OrderAggregate.Create(
			action.OrderId,
			action.CustomerId,
			action.Items,
			action.ShippingAddress);

		await _repository.SaveAsync(order, cancellationToken).ConfigureAwait(false);

		Console.WriteLine($"  [CreateOrderHandler] Order created with {action.Items.Count} items");
		Console.WriteLine($"  [CreateOrderHandler] Total: {order.TotalAmount:C}");
	}
}

/// <summary>
/// Handles CancelOrderCommand.
/// </summary>
public sealed class CancelOrderHandler : IActionHandler<CancelOrderCommand>
{
	private readonly IEventSourcedRepository<OrderAggregate, Guid> _repository;

	public CancelOrderHandler(IEventSourcedRepository<OrderAggregate, Guid> repository)
	{
		_repository = repository ?? throw new ArgumentNullException(nameof(repository));
	}

	public async Task HandleAsync(CancelOrderCommand action, CancellationToken cancellationToken)
	{
		Console.WriteLine($"  [CancelOrderHandler] Cancelling order {action.OrderId.ToString()[..8]}");

		var order = await _repository.GetByIdAsync(action.OrderId, cancellationToken).ConfigureAwait(false)
					?? throw new InvalidOperationException($"Order {action.OrderId} not found");

		order.Cancel(action.Reason);
		await _repository.SaveAsync(order, cancellationToken).ConfigureAwait(false);

		Console.WriteLine($"  [CancelOrderHandler] Order cancelled. Reason: {action.Reason}");
	}
}

/// <summary>
/// Handles ConfirmDeliveryCommand.
/// </summary>
public sealed class ConfirmDeliveryHandler : IActionHandler<ConfirmDeliveryCommand>
{
	private readonly IEventSourcedRepository<OrderAggregate, Guid> _repository;

	public ConfirmDeliveryHandler(IEventSourcedRepository<OrderAggregate, Guid> repository)
	{
		_repository = repository ?? throw new ArgumentNullException(nameof(repository));
	}

	public async Task HandleAsync(ConfirmDeliveryCommand action, CancellationToken cancellationToken)
	{
		Console.WriteLine($"  [ConfirmDeliveryHandler] Confirming delivery for order {action.OrderId.ToString()[..8]}");

		var order = await _repository.GetByIdAsync(action.OrderId, cancellationToken).ConfigureAwait(false)
					?? throw new InvalidOperationException($"Order {action.OrderId} not found");

		order.Complete();
		await _repository.SaveAsync(order, cancellationToken).ConfigureAwait(false);

		Console.WriteLine($"  [ConfirmDeliveryHandler] Order marked as completed");
	}
}
