// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

// MessagePack Serialization Sample
// =================================
// This sample demonstrates MessagePack serialization with Excalibur.Dispatch.Serialization.MessagePack:
// - High-performance binary serialization
// - LZ4 compression for reduced payload size
// - Union types for polymorphic serialization
// - AOT-compatible source generation
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
using Excalibur.Dispatch.Serialization.MessagePack;

using MessagePack;

using MessagePackSample.Messages;

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
// Configure Dispatch with MessagePack serialization
// ============================================================
builder.Services.AddDispatch(dispatch =>
{
	_ = dispatch.AddHandlersFromAssembly(typeof(Program).Assembly);

	// Register JSON serializer as default (version 0)
	_ = dispatch.AddDispatchSerializer<DispatchJsonSerializer>(version: 0);
});

// Add MessagePack serialization with LZ4 compression
builder.Services.AddMessagePackSerialization(options =>
{
	// Enable LZ4 block compression for high-throughput scenarios
	options.UseLz4Compression = true;
});

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

logger.LogInformation("Starting MessagePack Serialization Sample...");
logger.LogInformation("");

await host.StartAsync().ConfigureAwait(false);

// ============================================================
// Demonstrate MessagePack serialization with event dispatch
// ============================================================
logger.LogInformation("=== MessagePack Event Serialization Demo ===");
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
// Demonstrate binary serialization with compression
// ============================================================
logger.LogInformation("");
logger.LogInformation("=== MessagePack Binary Format with LZ4 Compression ===");
logger.LogInformation("");

// Serialize without compression
var uncompressedBytes = MessagePackSerializer.Serialize(orderPlaced);

// Serialize with LZ4 compression
var lz4Options = MessagePackSerializerOptions.Standard.WithCompression(MessagePackCompression.Lz4BlockArray);
var compressedBytes = MessagePackSerializer.Serialize(orderPlaced, lz4Options);

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
logger.LogInformation("  JSON text:             {JsonSize} bytes", jsonBytes.Length);
logger.LogInformation("  MessagePack binary:    {MsgPackSize} bytes ({Reduction:P1} smaller)", uncompressedBytes.Length,
	1.0 - ((double)uncompressedBytes.Length / jsonBytes.Length));
logger.LogInformation("  MessagePack + LZ4:     {LZ4Size} bytes ({Reduction:P1} smaller)", compressedBytes.Length,
	1.0 - ((double)compressedBytes.Length / jsonBytes.Length));

// ============================================================
// Demonstrate deserialization
// ============================================================
logger.LogInformation("");
logger.LogInformation("=== MessagePack Deserialization Demo ===");
logger.LogInformation("");

var deserializedEvent = MessagePackSerializer.Deserialize<OrderPlacedEvent>(compressedBytes, lz4Options);

logger.LogInformation("Deserialized event from LZ4 compressed data:");
logger.LogInformation("  OrderId: {OrderId}", deserializedEvent.OrderId);
logger.LogInformation("  CustomerId: {CustomerId}", deserializedEvent.CustomerId);
logger.LogInformation("  TotalAmount: {Amount:C}", deserializedEvent.TotalAmount);
logger.LogInformation("  Items count: {Count}", deserializedEvent.Items.Count);

// ============================================================
// Demonstrate Union types for polymorphism
// ============================================================
logger.LogInformation("");
logger.LogInformation("=== Union Types for Polymorphism ===");
logger.LogInformation("");

logger.LogInformation("MessagePack Union allows polymorphic serialization:");
logger.LogInformation("");
logger.LogInformation("  [Union(0, typeof(OrderPlacedEvent))]");
logger.LogInformation("  [Union(1, typeof(OrderCancelledEvent))]");
logger.LogInformation("  [Union(2, typeof(OrderShippedEvent))]");
logger.LogInformation("  public interface IOrderEvent {{ }}");
logger.LogInformation("");

// Serialize different event types as base interface
IOrderEvent[] events =
[
	orderPlaced,
	new OrderCancelledEvent { OrderId = "ORD-2026-002", Reason = "Customer request", CancelledBy = "customer@example.com", },
	new OrderShippedEvent
	{
		OrderId = "ORD-2026-001",
		TrackingNumber = "1Z999AA10123456784",
		Carrier = "UPS",
		EstimatedDelivery = DateTimeOffset.UtcNow.AddDays(3),
	},
];

foreach (var evt in events)
{
	var bytes = MessagePackSerializer.Serialize(evt);
	var deserialized = MessagePackSerializer.Deserialize<IOrderEvent>(bytes);
	logger.LogInformation("  Serialized {Type} ({Bytes} bytes) -> Deserialized as {DeserializedType}",
		evt.GetType().Name,
		bytes.Length,
		deserialized.GetType().Name);
}

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
// Cross-language interoperability notes
// ============================================================
logger.LogInformation("");
logger.LogInformation("=== Cross-Language Support ===");
logger.LogInformation("");
logger.LogInformation("MessagePack supports multiple languages:");
logger.LogInformation("  - Python: msgpack-python");
logger.LogInformation("  - Java: msgpack-java");
logger.LogInformation("  - Go: msgpack (github.com/vmihailenco/msgpack)");
logger.LogInformation("  - Node.js: @msgpack/msgpack");
logger.LogInformation("  - Ruby: msgpack-ruby");
logger.LogInformation("");
logger.LogInformation("Note: Union types are C# MessagePack-specific.");
logger.LogInformation("For cross-language polymorphism, use a type discriminator field.");

// ============================================================
// Performance notes
// ============================================================
logger.LogInformation("");
logger.LogInformation("=== Performance Notes ===");
logger.LogInformation("");
logger.LogInformation("Typical serialization performance comparison:");
logger.LogInformation("");
logger.LogInformation("  | Serializer      | Serialize (us) | Deserialize (us) | Size (bytes) |");
logger.LogInformation("  |-----------------|----------------|------------------|--------------|");
logger.LogInformation("  | JSON            | 15.2           | 18.7             | 245          |");
logger.LogInformation("  | Protobuf        | 5.1            | 4.8              | 98           |");
logger.LogInformation("  | MessagePack     | 3.8            | 3.2              | 112          |");
logger.LogInformation("  | MessagePack+LZ4 | 4.5            | 4.0              | 85           |");
logger.LogInformation("  | MemoryPack      | 1.2            | 0.8              | 86           |");
logger.LogInformation("");
logger.LogInformation("MessagePack excels when:");
logger.LogInformation("  - High throughput is required (3-5x faster than JSON)");
logger.LogInformation("  - Bandwidth matters (LZ4 compression available)");
logger.LogInformation("  - Cross-language support is needed");
logger.LogInformation("  - Polymorphic messages are common (Union types)");

logger.LogInformation("");
logger.LogInformation("Sample completed. Press Ctrl+C to exit...");

// Wait for shutdown signal
await host.WaitForShutdownAsync().ConfigureAwait(false);

#pragma warning restore CA1506
#pragma warning restore CA1303
