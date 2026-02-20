// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

// ============================================================================
// Order Processing Sample - Real-World Integration Patterns
// ============================================================================
// This comprehensive sample demonstrates how multiple Dispatch and Excalibur
// patterns work together in a realistic order processing workflow:
//
// Patterns demonstrated:
// - Event Sourcing (OrderAggregate)
// - CQRS Commands with FluentValidation
// - Saga Pattern (multi-step workflow orchestration)
// - Retry with exponential backoff (payment processing)
// - Compensation on failure (saga rollback)
// - External service integration
//
// Workflow: Create → Validate → Reserve → Pay → Ship → Complete
// ============================================================================

#pragma warning disable CA1303 // Sample code uses literal strings
#pragma warning disable CA1506 // Sample has high coupling by design

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Configuration;
using Excalibur.Dispatch.Messaging;
using Excalibur.Dispatch.Validation;

using FluentValidation;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using OrderProcessingSample.Domain.Aggregates;
using OrderProcessingSample.Domain.Commands;
using OrderProcessingSample.Domain.Events;
using OrderProcessingSample.ExternalServices;
using OrderProcessingSample.Handlers;

Console.WriteLine("=================================================");
Console.WriteLine("  Order Processing - Real-World Sample");
Console.WriteLine("=================================================");
Console.WriteLine();
Console.WriteLine("This sample demonstrates a complete order processing");
Console.WriteLine("workflow using multiple Dispatch/Excalibur patterns.");
Console.WriteLine();

// ============================================================================
// Step 1: Configure Services
// ============================================================================

var services = new ServiceCollection();

// Add logging
services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Warning));

// Add Dispatch with handlers and validation middleware
services.AddDispatch(dispatch =>
{
	_ = dispatch.AddHandlersFromAssembly(typeof(Program).Assembly);
	_ = dispatch.AddDispatchValidation()
		.WithFluentValidation();
});

// Register validators
services.AddValidatorsFromAssemblyContaining<CreateOrderCommandValidator>();

// Register in-memory order store
services.AddSingleton<InMemoryOrderStore>();

// Register external services (mocks)
services.AddSingleton<MockPaymentService>();
services.AddSingleton<IPaymentService>(sp => sp.GetRequiredService<MockPaymentService>());
services.AddSingleton<IShippingService, MockShippingService>();
services.AddSingleton<MockInventoryService>();
services.AddSingleton<IInventoryService>(sp => sp.GetRequiredService<MockInventoryService>());

var provider = services.BuildServiceProvider();

// Get services
var dispatcher = provider.GetRequiredService<IDispatcher>();
var context = DispatchContextInitializer.CreateDefaultContext(provider);
var orderStore = provider.GetRequiredService<InMemoryOrderStore>();
var paymentService = provider.GetRequiredService<MockPaymentService>();
var inventoryService = provider.GetRequiredService<MockInventoryService>();

// ============================================================================
// Demo 1: Successful Order Processing
// ============================================================================
Console.WriteLine("╔════════════════════════════════════════════════╗");
Console.WriteLine("║  Demo 1: Successful Order Processing           ║");
Console.WriteLine("╚════════════════════════════════════════════════╝");
Console.WriteLine();
Console.WriteLine("Creating and processing a valid order through the");
Console.WriteLine("complete workflow: Create → Validate → Pay → Ship");
Console.WriteLine();

// Create order
var customerId = Guid.NewGuid();
var items = new List<OrderLineItem>
{
	new(Guid.NewGuid(), "Gaming Laptop", 1, 1299.99m),
	new(Guid.NewGuid(), "Wireless Mouse", 2, 49.99m),
	new(Guid.NewGuid(), "USB-C Hub", 1, 79.99m)
};

var createCommand = new CreateOrderCommand(
	customerId,
	items,
	"123 Main Street, Anytown, ST 12345, USA");

Console.WriteLine("1. Creating order...");
var result = await dispatcher.DispatchAsync(createCommand, context, CancellationToken.None);
PrintResult(result);

// Get the created order
var order = orderStore.GetMostRecent();
if (order == null)
{
	Console.WriteLine("  [ERROR] Order not created!");
}
else
{
	Console.WriteLine();
	Console.WriteLine($"Order {order.Id.ToString()[..8]} created:");
	Console.WriteLine($"  Customer: {order.CustomerId.ToString()[..8]}");
	Console.WriteLine($"  Items: {order.Items.Count}");
	Console.WriteLine($"  Total: {order.TotalAmount:C}");
	Console.WriteLine($"  Status: {order.Status}");

	// Process the order (saga)
	Console.WriteLine();
	Console.WriteLine("2. Processing order (saga workflow)...");

	// Disable transient failures for first demo
	paymentService.SimulateTransientFailures = false;

	var processCommand = new ProcessOrderCommand(order.Id);
	result = await dispatcher.DispatchAsync(processCommand, context, CancellationToken.None);
	PrintResult(result);

	// Show final state
	Console.WriteLine();
	Console.WriteLine($"Final order state:");
	Console.WriteLine($"  Status: {order.Status}");
	Console.WriteLine($"  Transaction: {order.TransactionId}");
	Console.WriteLine($"  Tracking: {order.TrackingNumber} ({order.Carrier})");
}

// ============================================================================
// Demo 2: Order with Retry (Transient Payment Failures)
// ============================================================================
Console.WriteLine();
Console.WriteLine("╔════════════════════════════════════════════════╗");
Console.WriteLine("║  Demo 2: Retry Pattern (Transient Failures)    ║");
Console.WriteLine("╚════════════════════════════════════════════════╝");
Console.WriteLine();
Console.WriteLine("This demo shows retry with exponential backoff when");
Console.WriteLine("the payment service experiences transient failures.");
Console.WriteLine();

// Reset payment service
paymentService.Reset();
paymentService.SimulateTransientFailures = true;
paymentService.TransientFailureProbability = 0.5;

// Create another order
var items2 = new List<OrderLineItem> { new(Guid.NewGuid(), "Smartphone Pro", 1, 999.99m) };

var createCommand2 = new CreateOrderCommand(
	Guid.NewGuid(),
	items2,
	"456 Oak Avenue, Somewhere, ST 67890, USA");

Console.WriteLine("1. Creating order...");
result = await dispatcher.DispatchAsync(createCommand2, context, CancellationToken.None);
PrintResult(result);

var order2 = orderStore.GetMostRecent();
if (order2 != null)
{
	Console.WriteLine();
	Console.WriteLine("2. Processing order (with simulated transient failures)...");

	var processCommand2 = new ProcessOrderCommand(order2.Id);
	result = await dispatcher.DispatchAsync(processCommand2, context, CancellationToken.None);
	PrintResult(result);

	Console.WriteLine();
	Console.WriteLine($"Final order state:");
	Console.WriteLine($"  Status: {order2.Status}");
	if (order2.Status == OrderStatus.Shipped)
	{
		Console.WriteLine($"  Transaction: {order2.TransactionId}");
		Console.WriteLine($"  Tracking: {order2.TrackingNumber}");
	}
}

// ============================================================================
// Demo 3: Validation Failure
// ============================================================================
Console.WriteLine();
Console.WriteLine("╔════════════════════════════════════════════════╗");
Console.WriteLine("║  Demo 3: Validation Failure                    ║");
Console.WriteLine("╚════════════════════════════════════════════════╝");
Console.WriteLine();
Console.WriteLine("Demonstrating command validation with FluentValidation.");
Console.WriteLine("Orders with invalid data are rejected before processing.");
Console.WriteLine();

// Invalid order - empty items
var invalidCommand = new CreateOrderCommand(
	Guid.NewGuid(),
	[], // Empty items - will fail validation
	"Short"); // Too short - will fail validation

Console.WriteLine("Attempting to create order with invalid data...");
result = await dispatcher.DispatchAsync(invalidCommand, context, CancellationToken.None);
PrintValidationResult(result);

// ============================================================================
// Demo 4: Inventory Validation Failure (Saga Compensation)
// ============================================================================
Console.WriteLine();
Console.WriteLine("╔════════════════════════════════════════════════╗");
Console.WriteLine("║  Demo 4: Saga Compensation (Inventory Failure) ║");
Console.WriteLine("╚════════════════════════════════════════════════╝");
Console.WriteLine();
Console.WriteLine("When inventory validation fails, the saga executes");
Console.WriteLine("compensating actions to rollback partial state.");
Console.WriteLine();

// Mark a product as unavailable
var unavailableProductId = Guid.NewGuid();
inventoryService.UnavailableProducts.Add(unavailableProductId);

// Disable transient failures for this demo
paymentService.SimulateTransientFailures = false;

var items3 = new List<OrderLineItem>
{
	new(unavailableProductId, "Out of Stock Item", 1, 199.99m), new(Guid.NewGuid(), "Available Item", 2, 29.99m)
};

var createCommand3 = new CreateOrderCommand(
	Guid.NewGuid(),
	items3,
	"789 Pine Road, Elsewhere, ST 11111, USA");

Console.WriteLine("1. Creating order with unavailable product...");
result = await dispatcher.DispatchAsync(createCommand3, context, CancellationToken.None);
PrintResult(result);

var order3 = orderStore.GetMostRecent();
if (order3 != null)
{
	Console.WriteLine();
	Console.WriteLine("2. Processing order (will fail inventory validation)...");

	var processCommand3 = new ProcessOrderCommand(order3.Id);
	result = await dispatcher.DispatchAsync(processCommand3, context, CancellationToken.None);
	PrintResult(result);

	Console.WriteLine();
	Console.WriteLine($"Order state after saga failure:");
	Console.WriteLine($"  Status: {order3.Status}");
	Console.WriteLine($"  Failure Reason: {order3.FailureReason}");
}

// Clean up
inventoryService.UnavailableProducts.Clear();

// ============================================================================
// Demo 5: Order Cancellation
// ============================================================================
Console.WriteLine();
Console.WriteLine("╔════════════════════════════════════════════════╗");
Console.WriteLine("║  Demo 5: Order Cancellation                    ║");
Console.WriteLine("╚════════════════════════════════════════════════╝");
Console.WriteLine();
Console.WriteLine("Demonstrating order cancellation before processing.");
Console.WriteLine();

var items4 = new List<OrderLineItem> { new(Guid.NewGuid(), "Test Product", 1, 99.99m) };

var createCommand4 = new CreateOrderCommand(
	Guid.NewGuid(),
	items4,
	"Test Address, Test City, ST 00000, USA");

Console.WriteLine("1. Creating order...");
result = await dispatcher.DispatchAsync(createCommand4, context, CancellationToken.None);
PrintResult(result);

var order4 = orderStore.GetMostRecent();
if (order4 != null)
{
	Console.WriteLine();
	Console.WriteLine("2. Cancelling order...");

	var cancelCommand = new CancelOrderCommand(order4.Id, "Customer changed mind");
	result = await dispatcher.DispatchAsync(cancelCommand, context, CancellationToken.None);
	PrintResult(result);

	Console.WriteLine();
	Console.WriteLine($"Order state after cancellation:");
	Console.WriteLine($"  Status: {order4.Status}");
	Console.WriteLine($"  Reason: {order4.FailureReason}");
}

// ============================================================================
// Demo 6: Delivery Confirmation
// ============================================================================
Console.WriteLine();
Console.WriteLine("╔════════════════════════════════════════════════╗");
Console.WriteLine("║  Demo 6: Delivery Confirmation                 ║");
Console.WriteLine("╚════════════════════════════════════════════════╝");
Console.WriteLine();
Console.WriteLine("Completing the order lifecycle with delivery confirmation.");
Console.WriteLine();

// Use the first successful order
if (order?.Status == OrderStatus.Shipped)
{
	Console.WriteLine($"Confirming delivery for order {order.Id.ToString()[..8]}...");

	var confirmCommand = new ConfirmDeliveryCommand(order.Id);
	result = await dispatcher.DispatchAsync(confirmCommand, context, CancellationToken.None);
	PrintResult(result);

	Console.WriteLine();
	Console.WriteLine($"Final order state:");
	Console.WriteLine($"  Status: {order.Status}");
	Console.WriteLine($"  Completed At: {order.CompletedAt}");
}

// ============================================================================
// Summary
// ============================================================================
Console.WriteLine();
Console.WriteLine("=================================================");
Console.WriteLine("  Sample Complete!");
Console.WriteLine("=================================================");
Console.WriteLine();
Console.WriteLine("Patterns demonstrated:");
Console.WriteLine("  ✓ Event Sourcing (OrderAggregate with domain events)");
Console.WriteLine("  ✓ CQRS Commands (CreateOrder, ProcessOrder, Cancel)");
Console.WriteLine("  ✓ FluentValidation (command validation middleware)");
Console.WriteLine("  ✓ Saga Pattern (OrderProcessingSaga orchestration)");
Console.WriteLine("  ✓ Retry with Backoff (transient payment failures)");
Console.WriteLine("  ✓ Compensation (saga rollback on failure)");
Console.WriteLine("  ✓ External Service Integration (Payment, Shipping, Inventory)");
Console.WriteLine();
Console.WriteLine("Production considerations:");
Console.WriteLine("  - Use IEventSourcedRepository for aggregate persistence");
Console.WriteLine("  - Use Excalibur.Saga for persistent saga state");
Console.WriteLine("  - Use Excalibur.Dispatch.Resilience.Polly for resilience policies");
Console.WriteLine("  - Use the Outbox pattern for reliable messaging");
Console.WriteLine("  - Add projections for order read models (dashboards, queries)");
Console.WriteLine();

// ============================================================================
// Helper Methods
// ============================================================================

static void PrintResult(IMessageResult result)
{
	if (result.Succeeded)
	{
		Console.WriteLine("  [OK] Command executed successfully");
	}
	else
	{
		Console.WriteLine($"  [ERROR] {result.ErrorMessage}");
	}
}

static void PrintValidationResult(IMessageResult result)
{
	if (result.Succeeded)
	{
		Console.WriteLine("  [OK] Validation passed");
	}
	else
	{
		Console.WriteLine("  [VALIDATION FAILED]");
		if (result.ProblemDetails?.Extensions.TryGetValue("errors", out var errorsObj) == true
			&& errorsObj is IEnumerable<object> errors)
		{
			foreach (var error in errors)
			{
				if (error is Excalibur.Dispatch.Abstractions.Validation.ValidationError ve)
				{
					var prop = string.IsNullOrEmpty(ve.PropertyName) ? "(General)" : ve.PropertyName;
					Console.WriteLine($"    - [{prop}]: {ve.Message}");
				}
				else
				{
					Console.WriteLine($"    - {error}");
				}
			}
		}
	}
}

#pragma warning restore CA1506
#pragma warning restore CA1303
