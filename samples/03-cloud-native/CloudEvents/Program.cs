// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

// ============================================================================
// CloudEvents Sample
// ============================================================================
// Demonstrates how Excalibur.Dispatch integrates with the CNCF CloudEvents
// specification (https://cloudevents.io/).
//
// Key concepts shown:
// - Adding UseCloudEvents() middleware to the Dispatch pipeline
// - Configuring CloudEvent options (mode, source URI)
// - Dispatching events that get enriched with CloudEvents metadata
// ============================================================================

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Delivery;
using Excalibur.Dispatch.Abstractions.Transport;
using Excalibur.Dispatch.CloudEvents;
using Excalibur.Dispatch.Configuration;
using Excalibur.Dispatch.Messaging;
using Excalibur.Dispatch.Options.CloudEvents;

using Excalibur.Dispatch.Samples.CloudEvents;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

Console.WriteLine("=================================================");
Console.WriteLine("  CloudEvents Sample - Excalibur.Dispatch");
Console.WriteLine("=================================================");
Console.WriteLine();

// ------------------------------------------------------------------
// Step 1: Configure services with CloudEvents middleware
// ------------------------------------------------------------------
var services = new ServiceCollection();
services.AddLogging(b => b.SetMinimumLevel(LogLevel.Warning));

// Configure CloudEvent options
services.Configure<CloudEventOptions>(options =>
{
	options.Mode = CloudEventMode.Structured;
	options.DefaultSource = new Uri("urn:sample:cloud-events-demo");
});

// Register Dispatch with CloudEvents in the pipeline
services.AddDispatch(dispatch =>
{
	_ = dispatch.AddHandlersFromAssembly(typeof(Program).Assembly);

	// UseCloudEvents() adds the CloudEventMiddleware to the pipeline.
	// It enriches outgoing messages with CloudEvents metadata (source, type, subject).
	_ = dispatch.UseCloudEvents();
});

var provider = services.BuildServiceProvider();

// ------------------------------------------------------------------
// Step 2: Dispatch an event through the CloudEvents pipeline
// ------------------------------------------------------------------
var dispatcher = provider.GetRequiredService<IDispatcher>();
_ = provider.GetRequiredKeyedService<IMessageBus>("Local");
var context = DispatchContextInitializer.CreateDefaultContext(provider);

var orderEvent = new OrderPlacedEvent(
	OrderId: Guid.NewGuid(),
	CustomerId: "CUST-42",
	TotalAmount: 199.99m);

Console.WriteLine($"Dispatching OrderPlacedEvent (OrderId: {orderEvent.OrderId})...");
var result = await dispatcher.DispatchAsync(orderEvent, context, CancellationToken.None)
	.ConfigureAwait(false);

Console.WriteLine($"Dispatch result: Succeeded={result.Succeeded}");
Console.WriteLine();
Console.WriteLine("=================================================");
Console.WriteLine("  Sample Complete!");
Console.WriteLine("=================================================");

// ------------------------------------------------------------------
// Handler that processes the event
// ------------------------------------------------------------------
namespace Excalibur.Dispatch.Samples.CloudEvents
{
	/// <summary>
	/// A domain event representing an order being placed.
	/// </summary>
	public sealed record OrderPlacedEvent(
		Guid OrderId,
		string CustomerId,
		decimal TotalAmount) : IDispatchEvent;

	/// <summary>
	/// Handler that processes the order placed event.
	/// In a real app, this might update projections, send notifications, etc.
	/// </summary>
	public sealed class OrderPlacedEventHandler : IEventHandler<OrderPlacedEvent>
	{
		public Task HandleAsync(OrderPlacedEvent @event, CancellationToken cancellationToken)
		{
			Console.WriteLine($"[OrderPlacedEventHandler] Order {@event.OrderId} placed by {@event.CustomerId} for ${@event.TotalAmount}");
			return Task.CompletedTask;
		}
	}
}
