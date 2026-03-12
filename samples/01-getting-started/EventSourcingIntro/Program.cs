// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

// ============================================================================
// ExcaliburCqrs - Full CQRS/ES Sample Application
// ============================================================================
// This sample demonstrates event sourcing patterns using Excalibur:
// - AggregateRoot<T> for event-sourced domain entities
// - RaiseEvent() for raising domain events
// - ApplyEventInternal() with pattern matching for event application
// - IEventSourcedRepository for loading/saving aggregates
// - InMemoryEventStore for development/testing
//
// This is the progression from DispatchMinimal - see samples/MIGRATION.md
// ============================================================================

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Transport;
using Excalibur.Dispatch.Configuration;
using Excalibur.Dispatch.Messaging;

using ExcaliburCqrs.Domain.Aggregates;
using ExcaliburCqrs.Messages;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

Console.WriteLine("=================================================");
Console.WriteLine("  ExcaliburCqrs - Full Event Sourcing Sample");
Console.WriteLine("=================================================");
Console.WriteLine();

// Step 1: Configure services
var services = new ServiceCollection();

// Add logging
services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Warning));

// Add Dispatch (messaging) with handlers from this assembly
services.AddDispatch(dispatch =>
{
	_ = dispatch.AddHandlersFromAssembly(typeof(Program).Assembly);
});

// Add event serializer (required for event sourcing)
services.AddSingleton<IEventSerializer, JsonEventSerializer>();

// Add Excalibur event sourcing with in-memory event store
services.AddExcaliburEventSourcing(builder =>
{
	// Register the OrderAggregate repository with factory
	_ = builder.AddRepository<OrderAggregate, Guid>(id => new OrderAggregate(id));
});

// Add in-memory event store (for development/testing)
services.AddInMemoryEventStore();

var provider = services.BuildServiceProvider();

// Step 2: Get the dispatcher
var dispatcher = provider.GetRequiredService<IDispatcher>();

// Initialize the local message bus
_ = provider.GetRequiredKeyedService<IMessageBus>("Local");

// Create message context
var context = DispatchContextInitializer.CreateDefaultContext(provider);

Console.WriteLine("Step 1: Creating a new order (CreateOrderCommand)");
Console.WriteLine("This creates an OrderAggregate and raises OrderCreated event.");
Console.WriteLine();

// Step 3: Create an order (returns order ID)
var createCommand = new CreateOrderCommand("WIDGET-001", 5);
var createResult = await dispatcher.DispatchAsync<CreateOrderCommand, Guid>(
	createCommand, context, CancellationToken.None);

if (!createResult.Succeeded)
{
	Console.WriteLine($"  --> Failed: {createResult.ErrorMessage}");
	return;
}

var orderId = createResult.ReturnValue;
Console.WriteLine();

Console.WriteLine("Step 2: Adding items to the order (AddOrderItemCommand)");
Console.WriteLine("This loads the aggregate, adds items, and raises OrderItemAdded event.");
Console.WriteLine();

// Step 4: Add more items to the order
var addItem1 = new AddOrderItemCommand(orderId, "GADGET-002", 3);
await dispatcher.DispatchAsync(addItem1, context, CancellationToken.None);

var addItem2 = new AddOrderItemCommand(orderId, "GIZMO-003", 2);
await dispatcher.DispatchAsync(addItem2, context, CancellationToken.None);

Console.WriteLine();

Console.WriteLine("Step 3: Confirming the order (ConfirmOrderCommand)");
Console.WriteLine("This validates business rules and raises OrderConfirmed event.");
Console.WriteLine();

// Step 5: Confirm the order
var confirmCommand = new ConfirmOrderCommand(orderId);
await dispatcher.DispatchAsync(confirmCommand, context, CancellationToken.None);

Console.WriteLine();

Console.WriteLine("Step 4: Shipping the order (ShipOrderCommand)");
Console.WriteLine("This validates order is confirmed and raises OrderShipped event.");
Console.WriteLine();

// Step 6: Ship the order
var shipCommand = new ShipOrderCommand(orderId, "TRACK-12345");
await dispatcher.DispatchAsync(shipCommand, context, CancellationToken.None);

Console.WriteLine();

Console.WriteLine("Step 5: Querying the order (GetOrderQuery)");
Console.WriteLine("This loads the aggregate from event store and displays current state.");
Console.WriteLine();

// Step 7: Query the order state
var query = new GetOrderQuery(orderId);
await dispatcher.DispatchAsync(query, context, CancellationToken.None);

Console.WriteLine();
Console.WriteLine("=================================================");
Console.WriteLine("  Sample Complete!");
Console.WriteLine("=================================================");
Console.WriteLine();
Console.WriteLine("Key patterns demonstrated:");
Console.WriteLine("- AggregateRoot<Guid> with event sourcing");
Console.WriteLine("- RaiseEvent() for domain events");
Console.WriteLine("- ApplyEventInternal() with switch expressions");
Console.WriteLine("- IEventSourcedRepository for persistence");
Console.WriteLine("- Business invariants (order must be Created to add items)");
Console.WriteLine("- Order lifecycle: Created -> Confirmed -> Shipped");
Console.WriteLine();
Console.WriteLine("See samples/MIGRATION.md for upgrade guidance from DispatchMinimal.");
Console.WriteLine();
