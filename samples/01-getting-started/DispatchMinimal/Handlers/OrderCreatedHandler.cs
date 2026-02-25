// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using DispatchMinimal.Messages;

using Excalibur.Dispatch.Abstractions.Delivery;

namespace DispatchMinimal.Handlers;

/// <summary>
/// Handles OrderCreatedEvent - logs when an order is created.
/// Events can have multiple handlers - this is just one example.
/// </summary>
public class OrderCreatedHandler : IEventHandler<OrderCreatedEvent>
{
	public Task HandleAsync(OrderCreatedEvent eventMessage, CancellationToken cancellationToken)
	{
		Console.WriteLine($"[OrderCreatedHandler] Order created event received!");
		Console.WriteLine($"  Order ID: {eventMessage.OrderId}");
		Console.WriteLine($"  Product: {eventMessage.ProductId}");
		Console.WriteLine($"  Quantity: {eventMessage.Quantity}");

		// In a real app, this could update a read model, send notifications, etc.
		return Task.CompletedTask;
	}
}

/// <summary>
/// A second handler for the same event - demonstrates multi-handler support.
/// </summary>
public class OrderCreatedNotificationHandler : IEventHandler<OrderCreatedEvent>
{
	public Task HandleAsync(OrderCreatedEvent eventMessage, CancellationToken cancellationToken)
	{
		Console.WriteLine($"[OrderCreatedNotificationHandler] Sending notification for order {eventMessage.OrderId}...");

		// In a real app, this would send an email, SMS, push notification, etc.
		return Task.CompletedTask;
	}
}
