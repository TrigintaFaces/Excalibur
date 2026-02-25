// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

// MemoryPack Serialization Sample
// ================================
// This sample demonstrates MemoryPack serialization with Excalibur.Dispatch.Serialization.MemoryPack:
// - Zero-allocation binary serialization
// - Source-generated serializers for maximum performance
// - Immutable type support with [MemoryPackConstructor]
// - NativeAOT/trimming compatibility
//
// Prerequisites:
// 1. Run the sample: dotnet run

#pragma warning disable CA1303 // Sample code uses literal strings
#pragma warning disable CA1506 // Sample has high coupling by design

using Excalibur.Data.InMemory.Inbox;
using Excalibur.Data.InMemory.Outbox;
using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Configuration;
using Excalibur.Dispatch.Messaging;
using Excalibur.Dispatch.Serialization;

using MemoryPack;

using MemoryPackSample.Messages;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

// Build configuration
var builder = new HostApplicationBuilder(args);

// Add appsettings.json configuration
builder.Configuration.AddJsonFile("appsettings.json", optional: true, reloadOnChange: true);

// Configure logging for visibility
builder.Services.AddLogging(logging =>
{
	_ = logging.AddConsole();
	_ = logging.SetMinimumLevel(LogLevel.Information);
});

// ============================================================
// Configure Dispatch with MemoryPack serialization (default)
// ============================================================
builder.Services.AddDispatch(dispatch =>
{
	_ = dispatch.AddHandlersFromAssembly(typeof(Program).Assembly);

	// Register JSON serializer as default (version 0)
	_ = dispatch.AddDispatchSerializer<DispatchJsonSerializer>(version: 0);
});

// Add MemoryPack internal serialization (for Outbox/Inbox persistence)
// MemoryPack is the default and fastest binary serializer
builder.Services.AddMemoryPackInternalSerialization();

// ============================================================
// Configure outbox/inbox for reliable messaging
// ============================================================
builder.Services.AddOutbox<InMemoryOutboxStore>();
builder.Services.AddInbox<InMemoryInboxStore>();
builder.Services.AddOutboxHostedService();
builder.Services.AddInboxHostedService();

// ============================================================
// Build and start the host
// ============================================================
using var host = builder.Build();

var logger = host.Services.GetRequiredService<ILogger<Program>>();
var dispatcher = host.Services.GetRequiredService<IDispatcher>();
var context = DispatchContextInitializer.CreateDefaultContext();

logger.LogInformation("Starting MemoryPack Serialization Sample...");
logger.LogInformation("");

await host.StartAsync().ConfigureAwait(false);

// ============================================================
// Demonstrate MemoryPack serialization with event dispatch
// ============================================================
logger.LogInformation("=== MemoryPack Event Serialization Demo ===");
logger.LogInformation("");

// Create an order placed event with multiple items
var orderPlaced = new OrderPlacedEvent
{
	EventId = Guid.NewGuid(),
	OrderId = "ORD-2026-001",
	CustomerId = "CUST-12345",
	Items =
	[
		new OrderItem
		{
			ProductSku = "WIDGET-001", ProductName = "Premium Widget", Quantity = 2, UnitPrice = 49.99m,
		},
		new OrderItem
		{
			ProductSku = "GADGET-002", ProductName = "Smart Gadget", Quantity = 1, UnitPrice = 199.99m,
		}
	],
	TotalAmount = 299.97m,
	OccurredAt = DateTimeOffset.UtcNow,
};

logger.LogInformation("Dispatching OrderPlacedEvent...");
await dispatcher.DispatchAsync(orderPlaced, context, cancellationToken: default).ConfigureAwait(false);

await Task.Delay(300).ConfigureAwait(false);

// ============================================================
// Demonstrate binary serialization performance
// ============================================================
logger.LogInformation("");
logger.LogInformation("=== MemoryPack Binary Format Performance ===");
logger.LogInformation("");

// Serialize with MemoryPack
var memoryPackBytes = MemoryPackSerializer.Serialize(orderPlaced);

// Compare with JSON
var jsonString = System.Text.Json.JsonSerializer.Serialize(new
{
	orderPlaced.EventId,
	orderPlaced.OrderId,
	orderPlaced.CustomerId,
	orderPlaced.TotalAmount,
	Items = orderPlaced.Items.Select(i => new { i.ProductSku, i.ProductName, i.Quantity, i.UnitPrice }),
});
var jsonBytes = System.Text.Encoding.UTF8.GetBytes(jsonString);

logger.LogInformation("Serialization Size Comparison:");
logger.LogInformation("  JSON text:        {JsonSize} bytes", jsonBytes.Length);
logger.LogInformation("  MemoryPack:       {MemPackSize} bytes ({Reduction:P1} smaller)", memoryPackBytes.Length,
	1.0 - ((double)memoryPackBytes.Length / jsonBytes.Length));

// ============================================================
// Demonstrate zero-allocation deserialization
// ============================================================
logger.LogInformation("");
logger.LogInformation("=== Zero-Allocation Deserialization Demo ===");
logger.LogInformation("");

// MemoryPack supports ReadOnlySpan-based deserialization
ReadOnlySpan<byte> span = memoryPackBytes;
var deserializedEvent = MemoryPackSerializer.Deserialize<OrderPlacedEvent>(span);

logger.LogInformation("Deserialized event from binary data:");
logger.LogInformation("  OrderId: {OrderId}", deserializedEvent.OrderId);
logger.LogInformation("  CustomerId: {CustomerId}", deserializedEvent.CustomerId);
logger.LogInformation("  TotalAmount: {Amount:C}", deserializedEvent.TotalAmount);
logger.LogInformation("  Items count: {Count}", deserializedEvent.Items.Count);

// ============================================================
// Demonstrate immutable type with constructor
// ============================================================
logger.LogInformation("");
logger.LogInformation("=== Immutable Type Serialization ===");
logger.LogInformation("");

var orderCompleted = new OrderCompletedEvent(
	Guid.NewGuid(),
	"ORD-2026-001",
	DateTimeOffset.UtcNow,
	299.97m);

var completedBytes = MemoryPackSerializer.Serialize(orderCompleted);
var deserializedCompleted = MemoryPackSerializer.Deserialize<OrderCompletedEvent>(completedBytes);

logger.LogInformation("Immutable event with [MemoryPackConstructor]:");
logger.LogInformation("  Original OrderId: {OrderId}", orderCompleted.OrderId);
logger.LogInformation("  Deserialized OrderId: {OrderId}", deserializedCompleted.OrderId);
logger.LogInformation("  Values match: {Match}", orderCompleted.OrderId == deserializedCompleted.OrderId);

logger.LogInformation("Dispatching OrderCompletedEvent...");
await dispatcher.DispatchAsync(orderCompleted, context, cancellationToken: default).ConfigureAwait(false);

await Task.Delay(300).ConfigureAwait(false);

// ============================================================
// Dispatch additional events
// ============================================================
logger.LogInformation("");
logger.LogInformation("=== Additional Event Types Demo ===");
logger.LogInformation("");

// Ship the order
var orderShipped = new OrderShippedEvent
{
	EventId = Guid.NewGuid(),
	OrderId = "ORD-2026-001",
	TrackingNumber = "1Z999AA10123456784",
	Carrier = "UPS",
	EstimatedDelivery = DateTimeOffset.UtcNow.AddDays(3),
	OccurredAt = DateTimeOffset.UtcNow,
};

logger.LogInformation("Dispatching OrderShippedEvent...");
await dispatcher.DispatchAsync(orderShipped, context, cancellationToken: default).ConfigureAwait(false);

await Task.Delay(300).ConfigureAwait(false);

// Cancel another order
var orderCancelled = new OrderCancelledEvent
{
	EventId = Guid.NewGuid(),
	OrderId = "ORD-2026-002",
	Reason = "Customer requested cancellation - found better price elsewhere",
	CancelledBy = "customer@example.com",
	OccurredAt = DateTimeOffset.UtcNow,
};

logger.LogInformation("Dispatching OrderCancelledEvent...");
await dispatcher.DispatchAsync(orderCancelled, context, cancellationToken: default).ConfigureAwait(false);

await Task.Delay(300).ConfigureAwait(false);

// ============================================================
// Source generation and AOT notes
// ============================================================
logger.LogInformation("");
logger.LogInformation("=== Source Generation & AOT Support ===");
logger.LogInformation("");
logger.LogInformation("MemoryPack uses source generation for:");
logger.LogInformation("  - Zero-allocation serialization");
logger.LogInformation("  - Full NativeAOT/trimming support");
logger.LogInformation("  - No reflection at runtime");
logger.LogInformation("");
logger.LogInformation("Requirements:");
logger.LogInformation("  - [MemoryPackable] attribute on types");
logger.LogInformation("  - 'partial' keyword on classes");
logger.LogInformation("  - [MemoryPackConstructor] for immutable types");

// ============================================================
// Performance notes
// ============================================================
logger.LogInformation("");
logger.LogInformation("=== Performance Notes ===");
logger.LogInformation("");
logger.LogInformation("Typical serialization performance comparison:");
logger.LogInformation("");
logger.LogInformation("  | Serializer      | Serialize (ns/1KB) | Deserialize (ns/1KB) | Size (bytes) |");
logger.LogInformation("  |-----------------|--------------------|--------------------- |--------------|");
logger.LogInformation("  | JSON            | 15,200             | 18,700               | 245          |");
logger.LogInformation("  | Protobuf        | 5,100              | 4,800                | 98           |");
logger.LogInformation("  | MessagePack     | 3,800              | 3,200                | 112          |");
logger.LogInformation("  | MessagePack+LZ4 | 4,500              | 4,000                | 85           |");
logger.LogInformation("  | MemoryPack      | 150                | 120                  | 86           |");
logger.LogInformation("");
logger.LogInformation("MemoryPack excels when:");
logger.LogInformation("  - Maximum throughput is required (10-100x faster than JSON)");
logger.LogInformation("  - Memory pressure must be minimized");
logger.LogInformation("  - .NET-to-.NET communication only");
logger.LogInformation("  - NativeAOT deployment is needed");

// ============================================================
// When to use MemoryPack
// ============================================================
logger.LogInformation("");
logger.LogInformation("=== When to Use MemoryPack ===");
logger.LogInformation("");
logger.LogInformation("Best for:");
logger.LogInformation("  - Internal .NET-to-.NET communication");
logger.LogInformation("  - Event sourcing and Outbox persistence");
logger.LogInformation("  - Maximum performance requirements");
logger.LogInformation("  - AOT/NativeAOT deployments");
logger.LogInformation("");
logger.LogInformation("Consider alternatives when:");
logger.LogInformation("  - Cross-language consumers (use MessagePack or Protobuf)");
logger.LogInformation("  - Human-readable debugging (use JSON)");
logger.LogInformation("  - Schema-based contracts (use Protobuf)");

logger.LogInformation("");
logger.LogInformation("Sample completed. Press Ctrl+C to exit...");

// Wait for shutdown signal
await host.WaitForShutdownAsync().ConfigureAwait(false);

#pragma warning restore CA1506
#pragma warning restore CA1303
