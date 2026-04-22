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
using Excalibur.Dispatch.Delivery;
using Excalibur.Dispatch.Aot.Sample.EventSourcing;
using Excalibur.Dispatch.Aot.Sample.Handlers;
using Excalibur.Dispatch.Aot.Sample.Messages;
using Excalibur.Dispatch.Aot.Sample.Serialization;
using Excalibur.Dispatch.Configuration;
using Excalibur.Dispatch.Transport;
using Excalibur.Dispatch.Messaging;
using Excalibur.EventSourcing.Abstractions;

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

// Configure Dispatch with source-generator-discovered handlers (AOT-safe, zero reflection)
services.AddDispatch(dispatch => dispatch.AddDiscoveredHandlers());

// Register InMemory transport (zero native dependencies, AOT-safe)
services.AddInMemoryTransport("demo");

// S2: Register Event Sourcing with InMemory store (AOT-safe)
services.AddExcalibur(excalibur => excalibur.AddEventSourcing());
services.AddInMemoryEventStore();

// Configure source-generated JSON serializer options
services.AddSingleton(AppJsonSerializerContext.Default.Options);

var provider = services.BuildServiceProvider();

// Initialize the local message bus
_ = provider.GetRequiredKeyedService<IMessageBus>("Local");

// Get services
var dispatcher = provider.GetRequiredService<IDispatcher>();
var contextFactory = provider.GetService<IMessageContextFactory>();

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

// Dispatch the command via the pipeline.
// TResponse (Guid) is inferred from CreateOrderCommand : IDispatchAction<Guid> — no explicit type args needed.
// Context is auto-created from the ambient context or IMessageContextFactory.
var result = await dispatcher.DispatchAsync(createOrder, cancellationToken: default)
	.ConfigureAwait(false);
var orderId = result.ReturnValue;
Console.WriteLine($"Order created: {orderId}");
Console.WriteLine();

// ============================================================================
// Demo 2: Event Dispatch (Multiple Handlers)
// ============================================================================
Console.WriteLine("--- Demo 2: Event with Multiple Handlers ---");
Console.WriteLine("(OrderCreatedEvent was dispatched by CreateOrderHandler)");
Console.WriteLine("Both OrderCreatedHandler and OrderAnalyticsHandler processed it.");
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
var queryContext = contextFactory?.CreateContext() ?? new MessageContext();
// With-context dispatch requires explicit type parameters (C# overload resolution picks
// the generic TMessage overload over IDispatchAction<TResponse> when both have 3 params).
var queryResult = await dispatcher.DispatchAsync<GetOrderQuery, OrderDto>(query, queryContext, cancellationToken: default)
	.ConfigureAwait(false);
var order = queryResult.ReturnValue;
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
	var missingContext = contextFactory?.CreateContext() ?? new MessageContext();
	_ = await dispatcher.DispatchAsync<GetOrderQuery, OrderDto>(missingQuery, missingContext, cancellationToken: default)
		.ConfigureAwait(false);
	Console.WriteLine("Unexpected: found order");
}
catch (InvalidOperationException ex)
{
	Console.WriteLine($"Order not found (as expected): {ex.Message}");
}

Console.WriteLine();

// ============================================================================
// Demo 5: Serialization Round-Trip (AOT Source-Gen Verification)
// ============================================================================
Console.WriteLine("--- Demo 5: Serialization Round-Trip ---");

var originalCommand = new CreateOrderCommand { CustomerId = "CUST-RT", Items = [new OrderItem("ROUND-TRIP", 1, 9.99m)] };

var serialized = JsonSerializer.Serialize(originalCommand, AppJsonSerializerContext.Default.CreateOrderCommand);
Console.WriteLine($"Serialized:   {serialized}");

var deserialized = JsonSerializer.Deserialize(serialized, AppJsonSerializerContext.Default.CreateOrderCommand);
Console.WriteLine($"Deserialized: CustomerId={deserialized?.CustomerId}, Items={deserialized?.Items.Count}");

var match = deserialized is not null
			&& deserialized.CustomerId == originalCommand.CustomerId
			&& deserialized.Items.Count == originalCommand.Items.Count
			&& deserialized.Items[0].Sku == originalCommand.Items[0].Sku;
Console.WriteLine($"Round-trip match: {match}");
Console.WriteLine();

// ============================================================================
// Demo 6: Transport Registration Verification
// ============================================================================
Console.WriteLine("--- Demo 6: InMemory Transport Registration ---");

var transport = provider.GetRequiredKeyedService<InMemoryTransportAdapter>("demo");
Console.WriteLine($"Transport registered: Name={transport.Name}, Type={transport.TransportType}");
Console.WriteLine($"Transport running: {transport.IsRunning}");
Console.WriteLine();

// ============================================================================
// S2: Event Sourcing Scenarios (AOT-Safe)
// ============================================================================

// ============================================================================
// Demo 7: Aggregate with Pattern-Matching Event Application
// ============================================================================
Console.WriteLine("--- Demo 7: Event-Sourced Aggregate (AOT Pattern Matching) ---");

var account = new BankAccountAggregate();
account.Open("ACC-001", "Alice Johnson", 1000m);
account.Deposit(500m);
account.Withdraw(200m);

Console.WriteLine($"Account: {account.Id}");
Console.WriteLine($"Holder:  {account.HolderName}");
Console.WriteLine($"Balance: {account.Balance:C}");
Console.WriteLine($"Version: {account.Version}");
Console.WriteLine($"Events:  {account.GetUncommittedEvents().Count}");
Console.WriteLine($"AOT:     Pattern-matching apply (zero reflection)");
Console.WriteLine();

// ============================================================================
// Demo 8: Event Store Append + Load Round-Trip
// ============================================================================
Console.WriteLine("--- Demo 8: InMemory Event Store Round-Trip ---");

var eventStore = provider.GetRequiredKeyedService<IEventStore>("default");
var uncommittedEvents = account.GetUncommittedEvents();

#pragma warning disable IL2026, IL3050 // InMemoryEventStore serialization uses reflection internally
var appendResult = await eventStore.AppendAsync(
	account.Id,
	account.AggregateType,
	uncommittedEvents,
	-1, // expected version: -1 means new aggregate (no prior events)
	CancellationToken.None).ConfigureAwait(false);
#pragma warning restore IL2026, IL3050

Console.WriteLine($"Append result: Success={appendResult.Success}, NextExpectedVersion={appendResult.NextExpectedVersion}");

// Load events back from the store
var storedEvents = await eventStore.LoadAsync(
	account.Id,
	account.AggregateType,
	CancellationToken.None).ConfigureAwait(false);

Console.WriteLine($"Stored events loaded: {storedEvents.Count}");
foreach (var stored in storedEvents)
{
	Console.WriteLine($"  [{stored.Version}] {stored.EventType} @ {stored.Timestamp:HH:mm:ss}");
}

Console.WriteLine();

// ============================================================================
// Demo 9: Aggregate Reconstruction from Event History
// ============================================================================
Console.WriteLine("--- Demo 9: Aggregate Reconstruction from History ---");

// Create a fresh aggregate and replay events (simulating load from store)
var reconstructed = new BankAccountAggregate();
reconstructed.LoadFromHistory(uncommittedEvents);

Console.WriteLine($"Reconstructed account: {reconstructed.Id}");
Console.WriteLine($"Holder:  {reconstructed.HolderName}");
Console.WriteLine($"Balance: {reconstructed.Balance:C}");
Console.WriteLine($"Version: {reconstructed.Version}");
var stateMatch = reconstructed.Balance == account.Balance
	&& reconstructed.HolderName == account.HolderName
	&& reconstructed.Id == account.Id;
Console.WriteLine($"State matches original: {stateMatch}");
Console.WriteLine($"Uncommitted after replay: {reconstructed.GetUncommittedEvents().Count} (expected: 0)");
Console.WriteLine();

// ============================================================================
// S3: Transport Publish/Subscribe Scenarios (AOT-Safe)
// ============================================================================

// ============================================================================
// Demo 10: InMemory Transport Publish + Subscribe End-to-End
// ============================================================================
Console.WriteLine("--- Demo 10: Transport Publish/Subscribe (AOT) ---");

// Start the transport adapter
await transport.StartAsync(CancellationToken.None).ConfigureAwait(false);
Console.WriteLine($"Transport started: IsRunning={transport.IsRunning}");

// Send a message through the transport (simulates external message arrival)
var inventoryEvent = new InventoryUpdatedEvent
{
	Sku = "WIDGET-A",
	NewQuantity = 42,
	Warehouse = "WH-EAST",
	UpdatedAt = DateTimeOffset.UtcNow
};

// AOT-safe JSON serialization via source-generated context
var transportJson = JsonSerializer.Serialize(inventoryEvent, AppJsonSerializerContext.Default.InventoryUpdatedEvent);
Console.WriteLine($"Transport payload (source-gen JSON): {transportJson}");

// Deserialize (simulates transport receive side)
var received = JsonSerializer.Deserialize(transportJson, AppJsonSerializerContext.Default.InventoryUpdatedEvent);
Console.WriteLine($"Deserialized: SKU={received?.Sku}, Qty={received?.NewQuantity}");

// Send through transport adapter (verifies SendAsync works in AOT)
var transportContext = contextFactory?.CreateContext() ?? new MessageContext();
await transport.SendAsync(inventoryEvent, "inventory-updates", transportContext, CancellationToken.None)
	.ConfigureAwait(false);

Console.WriteLine($"Messages sent through transport: {transport.SentMessages.Count}");
var sentOk = transport.SentMessages.Values.Any(m => m is InventoryUpdatedEvent);
Console.WriteLine($"InventoryUpdatedEvent in transport: {sentOk}");

// Dispatch the event through the pipeline (verifies handler discovery in AOT)
var dispatchContext = contextFactory?.CreateContext() ?? new MessageContext();
await dispatcher.DispatchAsync(inventoryEvent, dispatchContext, cancellationToken: default)
	.ConfigureAwait(false);

var handlerReceived = InventoryUpdatedHandler.LastReceived is not null;
Console.WriteLine($"Handler received event: {handlerReceived}");

// Transport health check (verifies health API works in AOT)
var healthContext = new TransportHealthCheckContext(TransportHealthCheckCategory.Connectivity);
var health = await ((ITransportHealthChecker)transport).CheckHealthAsync(healthContext, CancellationToken.None)
	.ConfigureAwait(false);
Console.WriteLine($"Transport health: {health.Status}");

// Clean stop
await transport.StopAsync(CancellationToken.None).ConfigureAwait(false);
Console.WriteLine($"Transport stopped: IsRunning={transport.IsRunning}");
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
Console.WriteLine("S1 - Core Dispatch:");
Console.WriteLine("  - HandlerRegistrySourceGenerator (handler discovery + AOT factory)");
Console.WriteLine("  - HandlerActivationGenerator (DI-free instantiation)");
Console.WriteLine("  - HandlerInvocationGenerator (direct invocation)");
Console.WriteLine("  - MessageResultExtractorGenerator (AOT result factory)");
Console.WriteLine("  - JsonSerializationSourceGenerator (type metadata + registry)");
Console.WriteLine("  - ServiceRegistrationSourceGenerator ([AutoRegister] DI)");
Console.WriteLine("  - MessageTypeSourceGenerator (type metadata)");
Console.WriteLine();
Console.WriteLine("S2 - Event Sourcing:");
Console.WriteLine("  - AggregateRoot pattern-matching (zero reflection event apply)");
Console.WriteLine("  - InMemory event store (append + load round-trip)");
Console.WriteLine("  - Aggregate reconstruction from event history");
Console.WriteLine();
Console.WriteLine("S3 - Transport:");
Console.WriteLine("  - InMemory transport lifecycle (start/stop)");
Console.WriteLine("  - Transport SendAsync (message tracking)");
Console.WriteLine("  - Source-generated JSON serialization for transport payloads");
Console.WriteLine("  - Event dispatch through pipeline to handler");
Console.WriteLine("  - Transport health check API");
Console.WriteLine("================================================");
