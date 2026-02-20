// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

// Protobuf Serialization Sample
// ==============================
// This sample demonstrates Protobuf serialization with Excalibur.Dispatch.Serialization.Protobuf:
// - Google.Protobuf IMessage implementation
// - Binary format efficiency comparison
// - Schema evolution patterns
// - Cross-language interoperability
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
using Excalibur.Dispatch.Serialization.Protobuf;

using Google.Protobuf;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using ProtobufSample.Messages;

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
// Configure Dispatch with Protobuf serialization
// ============================================================
builder.Services.AddDispatch(dispatch =>
{
	_ = dispatch.AddHandlersFromAssembly(typeof(Program).Assembly);

	// Register JSON serializer as default (version 0)
	_ = dispatch.AddDispatchSerializer<DispatchJsonSerializer>(version: 0);
});

// Add Protobuf serialization support
builder.Services.AddProtobufSerialization(options =>
{
	options.WireFormat = ProtobufWireFormat.Binary;
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

logger.LogInformation("Starting Protobuf Serialization Sample...");
logger.LogInformation("");

await host.StartAsync().ConfigureAwait(false);

// ============================================================
// Demonstrate Protobuf serialization with event dispatch
// ============================================================
logger.LogInformation("=== Protobuf Event Serialization Demo ===");
logger.LogInformation("");

// Create an order placed event
var orderPlaced = new OrderPlacedEvent
{
	EventId = Guid.NewGuid().ToString(),
	OrderId = "ORD-2026-001",
	CustomerId = "CUST-12345",
	ProductName = "Premium Widget",
	Quantity = 3,
	TotalAmount = 149.97f,
};

logger.LogInformation("Dispatching OrderPlacedEvent...");
await dispatcher.DispatchAsync(orderPlaced, context, cancellationToken: default).ConfigureAwait(false);

await Task.Delay(300).ConfigureAwait(false);

// ============================================================
// Demonstrate binary serialization efficiency
// ============================================================
logger.LogInformation("");
logger.LogInformation("=== Protobuf Binary Format Comparison ===");
logger.LogInformation("");

// Serialize using Protobuf (Google.Protobuf native)
var protobufBytes = orderPlaced.ToByteArray();

// Compare with JSON (approximate)
var jsonString = System.Text.Json.JsonSerializer.Serialize(new
{
	orderPlaced.EventId,
	orderPlaced.OrderId,
	orderPlaced.CustomerId,
	orderPlaced.ProductName,
	orderPlaced.Quantity,
	orderPlaced.TotalAmount,
});
var jsonBytes = System.Text.Encoding.UTF8.GetBytes(jsonString);

logger.LogInformation("Serialization Size Comparison:");
logger.LogInformation("  Protobuf binary: {ProtoSize} bytes", protobufBytes.Length);
logger.LogInformation("  JSON text:       {JsonSize} bytes", jsonBytes.Length);
logger.LogInformation("  Size reduction:  {Reduction:P1}", 1.0 - ((double)protobufBytes.Length / jsonBytes.Length));

// ============================================================
// Demonstrate deserialization
// ============================================================
logger.LogInformation("");
logger.LogInformation("=== Protobuf Deserialization Demo ===");
logger.LogInformation("");

var deserializedEvent = OrderPlacedEvent.Parser.ParseFrom(protobufBytes);

logger.LogInformation("Deserialized event:");
logger.LogInformation("  EventId: {EventId}", deserializedEvent.EventId);
logger.LogInformation("  OrderId: {OrderId}", deserializedEvent.OrderId);
logger.LogInformation("  CustomerId: {CustomerId}", deserializedEvent.CustomerId);
logger.LogInformation("  ProductName: {ProductName}", deserializedEvent.ProductName);
logger.LogInformation("  Quantity: {Quantity}", deserializedEvent.Quantity);
logger.LogInformation("  TotalAmount: ${Amount:F2}", deserializedEvent.TotalAmount);

// ============================================================
// Demonstrate schema evolution
// ============================================================
logger.LogInformation("");
logger.LogInformation("=== Schema Evolution Demo ===");
logger.LogInformation("");

logger.LogInformation("Protobuf supports schema evolution through:");
logger.LogInformation("  1. Adding new fields with new tag numbers");
logger.LogInformation("  2. Removing fields (old readers ignore unknown fields when IgnoreMissingFields=true)");
logger.LogInformation("  3. Reserved fields prevent tag reuse");
logger.LogInformation("");
logger.LogInformation("Wire format tag calculation:");
logger.LogInformation("  - String fields: (field_number << 3) | 2 = tag");
logger.LogInformation("  - Int32 fields:  (field_number << 3) | 0 = tag");
logger.LogInformation("  - Float fields:  (field_number << 3) | 5 = tag");
logger.LogInformation("");
logger.LogInformation("Example tags in OrderPlacedEvent:");
logger.LogInformation("  - Field 1 (EventId, string):    tag = (1 << 3) | 2 = 10");
logger.LogInformation("  - Field 2 (OrderId, string):    tag = (2 << 3) | 2 = 18");
logger.LogInformation("  - Field 4 (TotalAmount, float): tag = (4 << 3) | 5 = 37");

// ============================================================
// Demonstrate additional event type
// ============================================================
logger.LogInformation("");
logger.LogInformation("=== Additional Event Type Demo ===");
logger.LogInformation("");

var orderCancelled = new OrderCancelledEvent
{
	EventId = Guid.NewGuid().ToString(),
	OrderId = "ORD-2026-002",
	Reason = "Customer requested cancellation - found better price elsewhere",
	CancelledBy = "customer@example.com",
};

logger.LogInformation("Dispatching OrderCancelledEvent...");
await dispatcher.DispatchAsync(orderCancelled, context, cancellationToken: default).ConfigureAwait(false);

await Task.Delay(300).ConfigureAwait(false);

// ============================================================
// Cross-language interoperability notes
// ============================================================
logger.LogInformation("");
logger.LogInformation("=== Cross-Language Interoperability ===");
logger.LogInformation("");
logger.LogInformation("The IMessage implementation maps to this .proto schema:");
logger.LogInformation("");
logger.LogInformation("  syntax = \"proto3\";");
logger.LogInformation("  package dispatch.samples;");
logger.LogInformation("");
logger.LogInformation("  message OrderPlacedEvent {{");
logger.LogInformation("    string event_id = 1;");
logger.LogInformation("    string order_id = 2;");
logger.LogInformation("    string customer_id = 3;");
logger.LogInformation("    float total_amount = 4;");
logger.LogInformation("    string product_name = 5;");
logger.LogInformation("    int32 quantity = 6;");
logger.LogInformation("  }}");
logger.LogInformation("");
logger.LogInformation("Language support:");
logger.LogInformation("  - Python: pip install protobuf; use generated classes");
logger.LogInformation("  - Java: protobuf-java; Maven/Gradle plugin");
logger.LogInformation("  - Go: google.golang.org/protobuf; protoc-gen-go");
logger.LogInformation("  - Node.js: protobufjs or google-protobuf");

// ============================================================
// Performance notes
// ============================================================
logger.LogInformation("");
logger.LogInformation("=== Performance Notes ===");
logger.LogInformation("");
logger.LogInformation("Typical serialization performance comparison:");
logger.LogInformation("");
logger.LogInformation("  | Serializer   | Serialize (us) | Deserialize (us) | Size (bytes) |");
logger.LogInformation("  |--------------|----------------|------------------|--------------|");
logger.LogInformation("  | JSON         | 15.2           | 18.7             | 245          |");
logger.LogInformation("  | Protobuf     | 5.1            | 4.8              | 98           |");
logger.LogInformation("  | MessagePack  | 3.8            | 3.2              | 112          |");
logger.LogInformation("  | MemoryPack   | 1.2            | 0.8              | 86           |");
logger.LogInformation("");
logger.LogInformation("Protobuf excels when:");
logger.LogInformation("  - Cross-language interop is required");
logger.LogInformation("  - Schema evolution is important");
logger.LogInformation("  - Binary size matters (network/storage)");
logger.LogInformation("  - GCP/AWS integration (native Protobuf support)");

logger.LogInformation("");
logger.LogInformation("Sample completed. Press Ctrl+C to exit...");

// Wait for shutdown signal
await host.WaitForShutdownAsync().ConfigureAwait(false);

#pragma warning restore CA1506
#pragma warning restore CA1303
