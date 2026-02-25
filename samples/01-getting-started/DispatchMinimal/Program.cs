// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

// ============================================================================
// DispatchMinimal - Pure Dispatch Sample Application
// ============================================================================
// This sample demonstrates Dispatch messaging patterns WITHOUT any Excalibur
// dependencies. Use this as a starting point for simple command/query/event
// scenarios before graduating to full CQRS/ES patterns.
//
// Key concepts demonstrated:
// - IDispatchAction (commands)
// - IDispatchEvent (events with multiple handlers)
// - IDispatchDocument (queries)
// - IDispatchHandler<T> (message handlers)
// - IDispatchMiddleware (custom pipeline middleware)
// - IDispatcher (message dispatching)
// ============================================================================

using DispatchMinimal.Messages;
using DispatchMinimal.Middleware;

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Transport;
using Excalibur.Dispatch.Configuration;
using Excalibur.Dispatch.Messaging;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

Console.WriteLine("=================================================");
Console.WriteLine("  DispatchMinimal - Pure Dispatch Sample");
Console.WriteLine("=================================================");
Console.WriteLine();

// Step 1: Configure services
var services = new ServiceCollection();

// Add logging (required by Dispatch pipeline)
services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Warning));

// Add Dispatch with handlers from this assembly
services.AddDispatch(dispatch =>
{
	_ = dispatch.AddHandlersFromAssembly(typeof(Program).Assembly);
});

// Add custom middleware
services.AddSingleton<IDispatchMiddleware, LoggingMiddleware>();

// Build the service provider
var provider = services.BuildServiceProvider();

// Step 2: Get the dispatcher
var dispatcher = provider.GetRequiredService<IDispatcher>();

// Initialize the local message bus (keyed registration)
_ = provider.GetRequiredKeyedService<IMessageBus>("Local");

// Create a message context with proper correlation ID
var context = DispatchContextInitializer.CreateDefaultContext(provider);

Console.WriteLine("Step 1: Dispatching a COMMAND (CreateOrderCommand)");
Console.WriteLine("Commands represent intent to change state.");
Console.WriteLine();

// Step 3: Dispatch a command that returns a value (order ID)
var createCommand = new CreateOrderCommand("WIDGET-123", 5);
var createResult = await dispatcher.DispatchAsync<CreateOrderCommand, Guid>(createCommand, context, CancellationToken.None);

if (createResult.Succeeded)
{
	var orderId = createResult.ReturnValue;
	Console.WriteLine();
	Console.WriteLine($"  --> Command succeeded! Order ID: {orderId}");
	Console.WriteLine();

	Console.WriteLine("Step 2: Dispatching an EVENT (OrderCreatedEvent)");
	Console.WriteLine("Events notify multiple handlers that something happened.");
	Console.WriteLine();

	// Step 4: Dispatch an event (can have multiple handlers)
	var orderEvent = new OrderCreatedEvent(orderId, "WIDGET-123", 5);
	var eventResult = await dispatcher.DispatchAsync(orderEvent, context, CancellationToken.None);

	Console.WriteLine();
	Console.WriteLine($"  --> Event dispatched to multiple handlers (Success: {eventResult.Succeeded})");
	Console.WriteLine();

	Console.WriteLine("Step 3: Dispatching a DOCUMENT (GetOrderQuery)");
	Console.WriteLine("Documents represent data that flows through the pipeline.");
	Console.WriteLine();

	// Step 5: Dispatch a document query
	var query = new GetOrderQuery(orderId);
	var queryResult = await dispatcher.DispatchAsync(query, context, CancellationToken.None);

	Console.WriteLine();
	Console.WriteLine($"  --> Document processing completed (Success: {queryResult.Succeeded})");
}
else
{
	Console.WriteLine($"  --> Command failed: {createResult.ErrorMessage}");
}

Console.WriteLine();
Console.WriteLine("=================================================");
Console.WriteLine("  Sample Complete!");
Console.WriteLine("=================================================");
Console.WriteLine();
Console.WriteLine("Next steps:");
Console.WriteLine("- See samples/ExcaliburCqrs for aggregate root patterns");
Console.WriteLine("- See samples/MIGRATION.md for upgrade guidance");
Console.WriteLine();
