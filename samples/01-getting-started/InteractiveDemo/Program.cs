// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

// ============================================================================
// QuickDemo - Interactive Event Dispatch Demo
// ============================================================================
// Demonstrates event dispatching with multiple handlers in an interactive loop.
// Press any key to simulate order events, ESC to exit.
// ============================================================================

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Delivery;
using Excalibur.Dispatch.Configuration;
using Excalibur.Dispatch.Messaging;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using QuickDemo;

Console.WriteLine("================================================");
Console.WriteLine("  QuickDemo - Interactive Event Dispatch");
Console.WriteLine("================================================");
Console.WriteLine();

var host = Host.CreateDefaultBuilder(args)
	.ConfigureServices(static (context, services) =>
	{
		_ = services.AddDispatch(dispatch =>
		{
			_ = dispatch.AddHandlersFromAssembly(typeof(Program).Assembly);
		});
	})
	.Build();

await host.StartAsync().ConfigureAwait(false);

var dispatcher = host.Services.GetRequiredService<IDispatcher>();

Console.WriteLine("Press any key to simulate orders (ESC to exit):");
Console.WriteLine();

// Demonstration loop - generate sample order amounts
var orderCount = 0;
while (Console.ReadKey(true).Key != ConsoleKey.Escape)
{
	orderCount++;
	var orderId = Guid.NewGuid();
	// Simple deterministic amount for demonstration purposes
	var amount = 100m + (orderCount * 50m);

	var context = DispatchContextInitializer.CreateDefaultContext(host.Services);

	_ = await dispatcher.DispatchAsync(
		new OrderPlacedEvent(orderId, amount, DateTimeOffset.UtcNow),
		context, cancellationToken: default).ConfigureAwait(false);

	await Task.Delay(100).ConfigureAwait(false);

	_ = await dispatcher.DispatchAsync(
		new OrderShippedEvent(orderId, DateTimeOffset.UtcNow),
		context, cancellationToken: default).ConfigureAwait(false);
}

Console.WriteLine();
Console.WriteLine("Shutting down...");
await host.StopAsync().ConfigureAwait(false);

namespace QuickDemo
{
	/// <summary>
	/// Event indicating an order was placed.
	/// </summary>
	public record OrderPlacedEvent(Guid OrderId, decimal Amount, DateTimeOffset EventTimestamp) : IDispatchEvent;

	/// <summary>
	/// Event indicating an order was shipped.
	/// </summary>
	public record OrderShippedEvent(Guid OrderId, DateTimeOffset ShippedAt) : IDispatchEvent;

	/// <summary>
	/// Handles order placement events.
	/// </summary>
	public class OrderHandler : IEventHandler<OrderPlacedEvent>
	{
		/// <inheritdoc />
		public Task HandleAsync(OrderPlacedEvent eventMessage, CancellationToken cancellationToken)
		{
			ArgumentNullException.ThrowIfNull(eventMessage);
			Console.WriteLine($"[OrderHandler] Order placed: {eventMessage.OrderId:N} for ${eventMessage.Amount}");
			return Task.CompletedTask;
		}
	}

	/// <summary>
	/// Handles shipping events.
	/// </summary>
	public class ShippingHandler : IEventHandler<OrderShippedEvent>
	{
		/// <inheritdoc />
		public Task HandleAsync(OrderShippedEvent eventMessage, CancellationToken cancellationToken)
		{
			ArgumentNullException.ThrowIfNull(eventMessage);
			Console.WriteLine($"[ShippingHandler] Order shipped: {eventMessage.OrderId:N} at {eventMessage.ShippedAt:HH:mm:ss}");
			return Task.CompletedTask;
		}
	}
}
