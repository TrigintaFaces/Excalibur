// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using DispatchMinimal.Messages;

using Excalibur.Dispatch.Abstractions.Delivery;

namespace DispatchMinimal.Handlers;

/// <summary>
/// Handles CreateOrderCommand - creates a new order and returns the order ID.
/// </summary>
public class CreateOrderHandler : IActionHandler<CreateOrderCommand, Guid>
{
	public Task<Guid> HandleAsync(CreateOrderCommand action, CancellationToken cancellationToken)
	{
		// In a real app, this would persist to a database
		var orderId = Guid.NewGuid();

		Console.WriteLine($"[CreateOrderHandler] Created order {orderId}");
		Console.WriteLine($"  Product: {action.ProductId}");
		Console.WriteLine($"  Quantity: {action.Quantity}");

		return Task.FromResult(orderId);
	}
}
