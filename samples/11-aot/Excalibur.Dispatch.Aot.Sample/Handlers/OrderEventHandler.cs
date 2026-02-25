// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions.Delivery;
using Excalibur.Dispatch.Aot.Sample.Messages;

namespace Excalibur.Dispatch.Aot.Sample.Handlers;

/// <summary>
/// Handles order-related domain events.
/// </summary>
/// <remarks>
/// Demonstrates:
/// - Event handlers (vs command/query handlers)
/// - Multiple handlers can subscribe to the same event
/// - All discovered at compile time by source generators
/// </remarks>
public sealed class OrderEventHandler : IEventHandler<OrderCreatedEvent>
{
	/// <inheritdoc />
	public Task HandleAsync(OrderCreatedEvent evt, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(evt);

		Console.WriteLine("[OrderEventHandler] Processing OrderCreatedEvent");
		Console.WriteLine($"[OrderEventHandler] Order {evt.OrderId} created at {evt.OccurredAt}");
		Console.WriteLine($"[OrderEventHandler] Customer: {evt.CustomerId}, Amount: ${evt.TotalAmount:F2}");

		// In a real application, this could:
		// - Update read models
		// - Send notifications
		// - Trigger downstream processes
		// - Update analytics

		return Task.CompletedTask;
	}
}

/// <summary>
/// Additional event handler for order analytics.
/// Demonstrates multiple handlers for the same event.
/// </summary>
public sealed class OrderAnalyticsHandler : IEventHandler<OrderCreatedEvent>
{
	/// <inheritdoc />
	public Task HandleAsync(OrderCreatedEvent evt, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(evt);

		Console.WriteLine($"[OrderAnalyticsHandler] Recording analytics for order {evt.OrderId}");

		// In a real application, this could:
		// - Update metrics
		// - Record to analytics store
		// - Calculate running totals

		return Task.CompletedTask;
	}
}
