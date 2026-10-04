// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using DispatchMinimal.Messages;

using Excalibur.Dispatch.Delivery;

namespace DispatchMinimal.Handlers;

/// <summary>
/// Handles OrderCreatedEvent - logs when an order is created.
/// Events can have multiple handlers - this is just one example.
/// </summary>
public sealed class OrderCreatedHandler(OrderStore store) : IEventHandler<OrderCreatedEvent>
{
	public Task HandleAsync(OrderCreatedEvent eventMessage, CancellationToken cancellationToken)
	{
		Console.WriteLine($"[OrderCreatedHandler] Order created event received!");
		Console.WriteLine($"  Order ID: {eventMessage.OrderId}");
		Console.WriteLine($"  Product: {eventMessage.ProductId}");
		Console.WriteLine($"  Quantity: {eventMessage.Quantity}");

		store.ReadModel[eventMessage.OrderId] = new OrderDto(
			eventMessage.OrderId, eventMessage.ProductId, eventMessage.Quantity, "Confirmed");
		return Task.CompletedTask;
	}
}

/// <summary>
/// A second handler for the same event - demonstrates multi-handler support.
/// </summary>
public sealed class OrderCreatedNotificationHandler(OrderStore store) : IEventHandler<OrderCreatedEvent>
{
	public Task HandleAsync(OrderCreatedEvent eventMessage, CancellationToken cancellationToken)
	{
		Console.WriteLine($"[OrderCreatedNotificationHandler] Recording notification locally for order {eventMessage.OrderId}...");

		// Record the notification locally; this sample does not contact an external service.
		store.Notifications[eventMessage.OrderId] = 0;
		return Task.CompletedTask;
	}
}
