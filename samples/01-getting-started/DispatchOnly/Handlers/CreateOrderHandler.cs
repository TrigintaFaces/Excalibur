// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using DispatchMinimal.Messages;

using Excalibur.Dispatch.Delivery;

namespace DispatchMinimal.Handlers;

/// <summary>
/// Handles CreateOrderCommand - creates a new order and returns the order ID.
/// </summary>
public sealed class CreateOrderHandler(OrderStore store) : IActionHandler<CreateOrderCommand, Guid>
{
	public Task<Guid> HandleAsync(CreateOrderCommand action, CancellationToken cancellationToken)
	{
		var orderId = Guid.NewGuid();
		store.Orders[orderId] = new OrderDto(orderId, action.ProductId, action.Quantity, "Created");

		Console.WriteLine($"[CreateOrderHandler] Created order {orderId}");
		Console.WriteLine($"  Product: {action.ProductId}");
		Console.WriteLine($"  Quantity: {action.Quantity}");

		return Task.FromResult(orderId);
	}
}
