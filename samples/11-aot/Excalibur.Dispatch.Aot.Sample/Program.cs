// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

// ============================================================================
// Excalibur.Dispatch.Aot.Sample - Native AOT Compatible Dispatch Example
// ============================================================================
// Demonstrates Native AOT compilation with Dispatch source generators:
// - Compile-time handler discovery (no reflection)
// - Source-generated JSON serialization
// - Static pipeline generation
// - Zero runtime code generation
// ============================================================================

using System.Text.Json;

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Transport;
using Excalibur.Dispatch.Aot.Sample.Handlers;
using Excalibur.Dispatch.Aot.Sample.Messages;
using Excalibur.Dispatch.Aot.Sample.Serialization;
using Excalibur.Dispatch.Configuration;
using Excalibur.Dispatch.Messaging;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

Console.WriteLine("================================================");
Console.WriteLine("  Excalibur.Dispatch.Aot.Sample - Native AOT Demo");
Console.WriteLine("================================================");
Console.WriteLine();

// ============================================================================
// AOT-Compatible Service Configuration
// ============================================================================
// Key differences from reflection-based configuration:
// 1. AddHandlersFromAssembly works because source generators pre-discover handlers
// 2. JSON serialization uses AppJsonSerializerContext
// 3. No runtime code generation (all pipelines are static)
// ============================================================================

var services = new ServiceCollection();

// Add logging (minimal setup for demo)
services.AddLogging(builder => builder.SetMinimumLevel(LogLevel.Warning));

// Configure Dispatch with source-generator-discovered handlers
services.AddDispatch(dispatch => dispatch.AddHandlersFromAssembly(typeof(Program).Assembly));

// Configure source-generated JSON serializer options
services.AddSingleton(AppJsonSerializerContext.Default.Options);

var provider = services.BuildServiceProvider();

// Initialize the local message bus
_ = provider.GetRequiredKeyedService<IMessageBus>("Local");

// Get services
var directLocal = provider.GetRequiredService<IDirectLocalDispatcher>();

// ============================================================================
// Demo 1: Command with Response
// ============================================================================
Console.WriteLine("--- Demo 1: Create Order Command ---");

var createOrder = new CreateOrderCommand
{
	CustomerId = "CUST-001",
	Items =
	[
		new OrderItem("WIDGET-A", 2, 29.99m),
		new OrderItem("GADGET-B", 1, 49.99m)
	]
};

// Show AOT-compatible serialization
Console.WriteLine("Serializing command (source-generated):");
var json = JsonSerializer.Serialize(createOrder, AppJsonSerializerContext.Default.CreateOrderCommand);
Console.WriteLine($"  {json}");
Console.WriteLine();

// Dispatch the command
var orderId = await directLocal.DispatchLocalAsync<CreateOrderCommand, Guid>(createOrder, cancellationToken: default)
	.ConfigureAwait(false);
Console.WriteLine($"Order created: {orderId}");
Console.WriteLine();

// ============================================================================
// Demo 2: Event Dispatch (Multiple Handlers)
// ============================================================================
Console.WriteLine("--- Demo 2: Event with Multiple Handlers ---");
Console.WriteLine("(OrderCreatedEvent was dispatched by CreateOrderHandler)");
Console.WriteLine("Both OrderEventHandler and OrderAnalyticsHandler processed it.");
Console.WriteLine();

// ============================================================================
// Demo 3: Query Existing Order
// ============================================================================
Console.WriteLine("--- Demo 3: Query Order ---");

// Store order for retrieval (demo only)
GetOrderHandler.AddOrder(new OrderDto
{
	Id = orderId,
	CustomerId = createOrder.CustomerId,
	Status = "Created",
	TotalAmount = createOrder.Items.Sum(i => i.Quantity * i.UnitPrice),
	Items = createOrder.Items,
	CreatedAt = DateTimeOffset.UtcNow
});

var query = new GetOrderQuery { OrderId = orderId };
var order = await directLocal.DispatchLocalAsync<GetOrderQuery, OrderDto>(query, cancellationToken: default)
	.ConfigureAwait(false);
Console.WriteLine("Order retrieved (source-generated serialization):");
var orderJson = JsonSerializer.Serialize(order, AppJsonSerializerContext.Default.OrderDto);
Console.WriteLine($"  {orderJson}");

Console.WriteLine();

// ============================================================================
// Demo 4: Query for Non-Existent Order
// ============================================================================
Console.WriteLine("--- Demo 4: Query Non-Existent Order ---");

var missingQuery = new GetOrderQuery { OrderId = Guid.NewGuid() };
try
{
	_ = await directLocal.DispatchLocalAsync<GetOrderQuery, OrderDto>(missingQuery, cancellationToken: default)
		.ConfigureAwait(false);
	Console.WriteLine("Unexpected: found order");
}
catch (InvalidOperationException ex)
{
	Console.WriteLine($"Order not found (as expected): {ex.Message}");
}
Console.WriteLine();

// ============================================================================
// AOT Build Verification
// ============================================================================
Console.WriteLine("================================================");
Console.WriteLine("  AOT Verification Summary");
Console.WriteLine("================================================");
Console.WriteLine("To build and verify AOT compatibility:");
Console.WriteLine("  dotnet publish -c Release");
Console.WriteLine();
Console.WriteLine("Expected output location:");
Console.WriteLine("  bin/Release/net10.0/win-x64/publish/Excalibur.Dispatch.Aot.Sample.exe");
Console.WriteLine();
Console.WriteLine("Key source generators active:");
Console.WriteLine("  - HandlerRegistrySourceGenerator (handler discovery + AOT factory)");
Console.WriteLine("  - HandlerActivationGenerator (DI-free instantiation)");
Console.WriteLine("  - HandlerInvocationGenerator (direct invocation)");
Console.WriteLine("  - MessageResultExtractorGenerator (AOT result factory)");
Console.WriteLine("  - JsonSerializationSourceGenerator (type metadata + registry)");
Console.WriteLine("  - ServiceRegistrationSourceGenerator ([AutoRegister] DI)");
Console.WriteLine("  - MessageTypeSourceGenerator (type metadata)");
Console.WriteLine("================================================");
