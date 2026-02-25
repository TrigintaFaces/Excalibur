// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

// Outbox Pattern Sample
// =====================
// This sample demonstrates the transactional outbox pattern for reliable message delivery:
// - Guaranteed message delivery even if the transport fails
// - At-least-once delivery semantics
// - Configurable retry and cleanup policies
// - Inbox for deduplication
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
using Excalibur.Outbox;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using OutboxPattern.Messages;

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
// Configure Dispatch messaging
// ============================================================
builder.Services.AddDispatch(dispatch =>
{
	_ = dispatch.AddHandlersFromAssembly(typeof(Program).Assembly);

	// Register JSON serializer as default (version 0)
	_ = dispatch.AddDispatchSerializer<DispatchJsonSerializer>(version: 0);
});

// ============================================================
// Configure Outbox Pattern for reliable messaging
// ============================================================
// The outbox pattern ensures messages are delivered reliably:
// 1. Messages are stored in the outbox atomically with business data
// 2. A background processor publishes messages asynchronously
// 3. Failed messages are retried with exponential backoff
// 4. Old messages are automatically cleaned up

// Use the preset-based fluent API for OutboxOptions (ADR-098)
// Start with a preset (Balanced, HighThroughput, HighReliability) then override specific settings
builder.Services.AddExcaliburOutbox(
	OutboxOptions.Balanced() // Sensible defaults for most scenarios
		.WithBatchSize(50) // Process 50 messages per batch
		.WithPollingInterval(TimeSpan.FromSeconds(2)) // Check for messages every 2 seconds
		.WithMaxRetries(3) // Retry failed messages up to 3 times
		.WithRetryDelay(TimeSpan.FromSeconds(10)) // Wait 10 seconds between retries
		.WithRetentionPeriod(TimeSpan.FromHours(1)) // Keep messages for 1 hour (demo)
		.WithCleanupInterval(TimeSpan.FromMinutes(5)) // Run cleanup every 5 minutes
		.Build());

// Register outbox and inbox stores (in-memory for demo)
builder.Services.AddOutbox<InMemoryOutboxStore>();
builder.Services.AddInbox<InMemoryInboxStore>();

// Register background processing services
builder.Services.AddOutboxHostedService(); // Processes outbox messages
builder.Services.AddInboxHostedService(); // Deduplicates incoming messages

// ============================================================
// Build and start the host
// ============================================================
using var host = builder.Build();

var logger = host.Services.GetRequiredService<ILogger<Program>>();
var dispatcher = host.Services.GetRequiredService<IDispatcher>();
var context = DispatchContextInitializer.CreateDefaultContext();

logger.LogInformation("Starting Outbox Pattern Sample...");
logger.LogInformation("");

await host.StartAsync().ConfigureAwait(false);

// ============================================================
// Demonstrate the Outbox Pattern
// ============================================================
logger.LogInformation("=== Transactional Outbox Pattern Demo ===");
logger.LogInformation("");
logger.LogInformation("The outbox pattern ensures reliable message delivery:");
logger.LogInformation("  1. Save message to outbox (same transaction as business data)");
logger.LogInformation("  2. Background processor publishes messages");
logger.LogInformation("  3. Retry on failure with configurable policy");
logger.LogInformation("  4. Auto-cleanup of old messages");
logger.LogInformation("");

// Simulate placing an order
var orderId = $"ORD-{DateTime.UtcNow:yyyyMMdd}-001";
logger.LogInformation("Placing order: {OrderId}", orderId);

var orderPlaced = new OrderPlacedEvent
{
	OrderId = orderId,
	CustomerId = "CUST-12345",
	TotalAmount = 299.99m,
	OccurredAt = DateTimeOffset.UtcNow,
};

await dispatcher.DispatchAsync(orderPlaced, context, cancellationToken: default).ConfigureAwait(false);
logger.LogInformation("  -> OrderPlacedEvent dispatched to outbox");

await Task.Delay(500).ConfigureAwait(false);

// ============================================================
// Demonstrate chained events
// ============================================================
logger.LogInformation("");
logger.LogInformation("=== Chained Events Demo ===");
logger.LogInformation("");
logger.LogInformation("Events can trigger other events, all stored reliably:");

// Simulate payment processing
var paymentProcessed = new PaymentProcessedEvent
{
	OrderId = orderId,
	TransactionId = $"TXN-{Guid.NewGuid():N}".ToUpperInvariant()[..20],
	Amount = 299.99m,
	OccurredAt = DateTimeOffset.UtcNow,
};

await dispatcher.DispatchAsync(paymentProcessed, context, cancellationToken: default).ConfigureAwait(false);
logger.LogInformation("  -> PaymentProcessedEvent dispatched");

await Task.Delay(500).ConfigureAwait(false);

// Simulate inventory reservation
var inventoryReserved = new InventoryReservedEvent
{
	OrderId = orderId,
	ProductSku = "WIDGET-001",
	Quantity = 2,
	OccurredAt = DateTimeOffset.UtcNow,
};

await dispatcher.DispatchAsync(inventoryReserved, context, cancellationToken: default).ConfigureAwait(false);
logger.LogInformation("  -> InventoryReservedEvent dispatched");

await Task.Delay(500).ConfigureAwait(false);

// ============================================================
// Demonstrate batch dispatching
// ============================================================
logger.LogInformation("");
logger.LogInformation("=== Batch Dispatching Demo ===");
logger.LogInformation("");
logger.LogInformation("Multiple messages can be dispatched and processed in batches:");

for (var i = 2; i <= 5; i++)
{
	var batchOrderId = $"ORD-{DateTime.UtcNow:yyyyMMdd}-{i:D3}";
	var batchOrder = new OrderPlacedEvent
	{
		OrderId = batchOrderId,
		CustomerId = $"CUST-{10000 + i}",
		TotalAmount = 100m * i,
		OccurredAt = DateTimeOffset.UtcNow,
	};

	_ = await dispatcher.DispatchAsync(batchOrder, context, cancellationToken: default).ConfigureAwait(false);
	logger.LogInformation("  -> Order {OrderId} dispatched", batchOrderId);
}

// Wait for batch processing
logger.LogInformation("");
logger.LogInformation("Waiting for outbox processor to deliver messages...");
await Task.Delay(3000).ConfigureAwait(false);

// ============================================================
// Explain configuration options
// ============================================================
logger.LogInformation("");
logger.LogInformation("=== Outbox Configuration Options ===");
logger.LogInformation("");
logger.LogInformation("  | Option                    | Default    | Description                          |");
logger.LogInformation("  |---------------------------|------------|--------------------------------------|");
logger.LogInformation("  | BatchSize                 | 100        | Messages processed per batch         |");
logger.LogInformation("  | PollingInterval           | 5 seconds  | Time between processing cycles       |");
logger.LogInformation("  | MaxRetryCount             | 3          | Maximum retry attempts               |");
logger.LogInformation("  | RetryDelay                | 5 minutes  | Delay between retries                |");
logger.LogInformation("  | MessageRetentionPeriod    | 7 days     | How long to keep processed messages  |");
logger.LogInformation("  | EnableAutomaticCleanup    | true       | Auto-delete old messages             |");
logger.LogInformation("  | CleanupInterval           | 1 hour     | Time between cleanup runs            |");
logger.LogInformation("  | EnableParallelProcessing  | false      | Process messages in parallel         |");
logger.LogInformation("  | MaxDegreeOfParallelism    | 4          | Max parallel message handlers        |");

// ============================================================
// Demonstrate failure scenario
// ============================================================
logger.LogInformation("");
logger.LogInformation("=== Failure Handling Demo ===");
logger.LogInformation("");
logger.LogInformation("When message delivery fails, outbox retries automatically:");

var failedOrder = new OrderFailedEvent
{
	OrderId = "ORD-FAILED-001",
	Reason = "Insufficient inventory",
	OccurredAt = DateTimeOffset.UtcNow,
};

await dispatcher.DispatchAsync(failedOrder, context, cancellationToken: default).ConfigureAwait(false);
logger.LogInformation("  -> OrderFailedEvent dispatched");

await Task.Delay(500).ConfigureAwait(false);

// ============================================================
// Explain inbox deduplication
// ============================================================
logger.LogInformation("");
logger.LogInformation("=== Inbox Deduplication ===");
logger.LogInformation("");
logger.LogInformation("The inbox pattern prevents duplicate message processing:");
logger.LogInformation("  1. Each message has a unique identifier");
logger.LogInformation("  2. Inbox tracks processed message IDs");
logger.LogInformation("  3. Duplicate messages are silently ignored");
logger.LogInformation("  4. Old inbox records are cleaned up automatically");

// ============================================================
// Best practices
// ============================================================
logger.LogInformation("");
logger.LogInformation("=== Best Practices ===");
logger.LogInformation("");
logger.LogInformation("1. Store outbox messages in the SAME transaction as business data");
logger.LogInformation("2. Use idempotent handlers for at-least-once delivery");
logger.LogInformation("3. Monitor outbox queue depth for backpressure");
logger.LogInformation("4. Configure retention based on compliance requirements");
logger.LogInformation("5. Use parallel processing for high-throughput scenarios");

// ============================================================
// Production considerations
// ============================================================
logger.LogInformation("");
logger.LogInformation("=== Production Considerations ===");
logger.LogInformation("");
logger.LogInformation("For production, replace InMemory stores with durable implementations:");
logger.LogInformation("  - Excalibur.Outbox.SqlServer for SQL Server");
logger.LogInformation("  - Custom implementations for other databases");
logger.LogInformation("");
logger.LogInformation("Example:");
logger.LogInformation("  services.AddSqlServerOutboxStore(connectionString);");
logger.LogInformation("  services.AddSqlServerInboxStore(connectionString);");

logger.LogInformation("");
logger.LogInformation("Sample completed. Press Ctrl+C to exit...");

// Wait for shutdown signal
await host.WaitForShutdownAsync().ConfigureAwait(false);

#pragma warning restore CA1506
#pragma warning restore CA1303
