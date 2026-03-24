// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

// Outbox Pattern Sample
// =====================
// This sample demonstrates the transactional outbox pattern for reliable message delivery
// using the middleware pipeline approach:
// - UseOutbox() middleware intercepts outgoing messages and stages them in the outbox
// - UseInbox() middleware provides deduplication for at-least-once delivery
// - Background processor publishes messages asynchronously
// - Configurable retry and cleanup policies
//
// Prerequisites:
// 1. Run the sample: dotnet run

#pragma warning disable CA1303 // Sample code uses literal strings
#pragma warning disable CA1506 // Sample has high coupling by design

using Excalibur.Dispatch.Configuration;
using Excalibur.Dispatch.Middleware.Inbox;
using Excalibur.Dispatch.Middleware.Outbox;

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
// Configure Dispatch with Outbox + Inbox middleware pipeline
// ============================================================
// The middleware pipeline approach is the recommended way to configure
// outbox and inbox. Middleware is registered in the dispatch pipeline
// and automatically intercepts messages.
builder.Services.AddDispatch(dispatch =>
{
	// Add inbox middleware first -- deduplicates before processing
	dispatch.UseInbox();

	// Add outbox middleware -- stages integration events for reliable delivery
	dispatch.UseOutbox();

	// Register handlers from this assembly
	dispatch.AddHandlersFromAssembly(typeof(Program).Assembly);
});

// ============================================================
// Configure Outbox infrastructure (store + background processing)
// ============================================================
// The outbox builder configures the storage provider and processing behavior.
// The UseOutbox() middleware above handles pipeline integration;
// AddExcaliburOutbox() configures the backend infrastructure.
builder.Services.AddExcaliburOutbox(outbox =>
{
	outbox.UseInMemory() // In-memory for demo; use UseSqlServer() in production
		.WithProcessing(processing =>
		{
			processing.BatchSize(50)              // Process 50 messages per batch
				.PollingInterval(TimeSpan.FromSeconds(2))  // Check for messages every 2 seconds
				.MaxRetryCount(3)                  // Retry failed messages up to 3 times
				.RetryDelay(TimeSpan.FromSeconds(10));      // Wait 10 seconds between retries
		})
		.WithCleanup(cleanup =>
		{
			cleanup.EnableAutoCleanup(true)
				.RetentionPeriod(TimeSpan.FromHours(1))    // Keep messages for 1 hour (demo)
				.CleanupInterval(TimeSpan.FromMinutes(5)); // Run cleanup every 5 minutes
		})
		.EnableBackgroundProcessing(); // Start the background processor hosted service
});

// ============================================================
// Configure Inbox infrastructure (store)
// ============================================================
builder.Services.AddExcaliburInbox(inbox =>
{
	inbox.UseInMemory(); // In-memory for demo; use UseSqlServer() in production
});

// ============================================================
// Build and start the host
// ============================================================
using var host = builder.Build();

var logger = host.Services.GetRequiredService<ILogger<Program>>();

logger.LogInformation("Starting Outbox Pattern Sample...");
logger.LogInformation("");

await host.StartAsync().ConfigureAwait(false);

// ============================================================
// Demonstrate the Outbox Pattern
// ============================================================
logger.LogInformation("=== Transactional Outbox Pattern Demo ===");
logger.LogInformation("");
logger.LogInformation("Pipeline: UseInbox() -> UseOutbox() -> Handler");
logger.LogInformation("");
logger.LogInformation("The middleware pipeline ensures reliable message delivery:");
logger.LogInformation("  1. UseInbox() deduplicates incoming messages by ID");
logger.LogInformation("  2. UseOutbox() stages outgoing events in the outbox store");
logger.LogInformation("  3. Background processor publishes staged messages");
logger.LogInformation("  4. Failed messages are retried with configurable policy");
logger.LogInformation("  5. Old messages are automatically cleaned up");
logger.LogInformation("");

// Simulate placing an order
var orderId = $"ORD-{DateTime.UtcNow:yyyyMMdd}-001";
logger.LogInformation("Placing order: {OrderId}", orderId);

var dispatcher = host.Services.GetRequiredService<Excalibur.Dispatch.Abstractions.IDispatcher>();
var context = Excalibur.Dispatch.Messaging.DispatchContextInitializer.CreateDefaultContext();

var orderPlaced = new OrderPlacedEvent
{
	OrderId = orderId,
	CustomerId = "CUST-12345",
	TotalAmount = 299.99m,
	OccurredAt = DateTimeOffset.UtcNow,
};

await dispatcher.DispatchAsync(orderPlaced, context, cancellationToken: default).ConfigureAwait(false);
logger.LogInformation("  -> OrderPlacedEvent dispatched through pipeline");

await Task.Delay(500).ConfigureAwait(false);

// ============================================================
// Demonstrate chained events
// ============================================================
logger.LogInformation("");
logger.LogInformation("=== Chained Events Demo ===");
logger.LogInformation("");
logger.LogInformation("Events can trigger other events, all staged reliably via UseOutbox():");

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
logger.LogInformation("Multiple messages are dispatched through the pipeline and processed in batches:");

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
// Explain middleware pipeline
// ============================================================
logger.LogInformation("");
logger.LogInformation("=== Middleware Pipeline Configuration ===");
logger.LogInformation("");
logger.LogInformation("  Recommended pipeline order:");
logger.LogInformation("    dispatch.UseSecurityStack();       // Auth, authz, tenant");
logger.LogInformation("    dispatch.UseResilienceStack();     // Timeout, retry, circuit breaker");
logger.LogInformation("    dispatch.UseValidationStack();     // Payload validation");
logger.LogInformation("    dispatch.UseTransaction();         // TransactionScope");
logger.LogInformation("    dispatch.UseInbox();               // Idempotency");
logger.LogInformation("    dispatch.UseOutbox();              // Reliable delivery");
logger.LogInformation("");
logger.LogInformation("  See samples/09-advanced/ProductionPipeline for full example.");

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
// Production considerations
// ============================================================
logger.LogInformation("");
logger.LogInformation("=== Production Considerations ===");
logger.LogInformation("");
logger.LogInformation("For production, replace InMemory stores with durable implementations:");
logger.LogInformation("  services.AddExcaliburOutbox(outbox => outbox.UseSqlServer(opts => opts.ConnectionString = connectionString));");
logger.LogInformation("  services.AddExcaliburInbox(inbox => inbox.UseSqlServer(opts => opts.ConnectionString = connectionString));");

logger.LogInformation("");
logger.LogInformation("Sample completed. Press Ctrl+C to exit...");

// Wait for shutdown signal
await host.WaitForShutdownAsync().ConfigureAwait(false);

#pragma warning restore CA1506
#pragma warning restore CA1303
