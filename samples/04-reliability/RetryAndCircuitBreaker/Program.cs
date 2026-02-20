// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

// Retry and Circuit Breaker Sample
// =================================
// This sample demonstrates resilience patterns using Polly:
// - Retry with exponential backoff and jitter
// - Circuit breaker for failing dependencies
// - Timeout handling for slow services
// - Bulkhead isolation for resource protection
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
using Excalibur.Dispatch.Resilience;
using Excalibur.Dispatch.Resilience.Polly;
using Excalibur.Dispatch.Serialization;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using RetryAndCircuitBreaker.Messages;
using RetryAndCircuitBreaker.Services;

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
// Configure Polly Resilience Patterns
// ============================================================

// Add Polly resilience infrastructure
builder.Services.AddPollyResilience(builder.Configuration);

// Configure named retry policy with exponential backoff and jitter
builder.Services.AddPollyRetryPolicy("payment-retry", options =>
{
	options.MaxRetries = 5; // Retry up to 5 times
	options.BaseDelay = TimeSpan.FromMilliseconds(200); // Start with 200ms delay
	options.BackoffStrategy = BackoffStrategy.Exponential; // 200ms -> 400ms -> 800ms -> etc.
	options.UseJitter = true; // Add randomness to prevent thundering herd
	options.JitterStrategy = JitterStrategy.Equal; // Equal jitter distribution
	options.JitterFactor = 0.3; // 30% jitter
	options.MaxDelay = TimeSpan.FromSeconds(10); // Cap at 10 seconds
	options.EnableDetailedLogging = true; // Log retry attempts
	options.ShouldRetry = ex => ex is PaymentServiceException; // Only retry payment exceptions
});

// Configure circuit breaker for inventory service
builder.Services.AddPollyCircuitBreaker("inventory-circuit", options =>
{
	options.FailureThreshold = 3; // Open after 3 failures
	options.SuccessThreshold = 2; // Close after 2 successes
	options.OpenDuration = TimeSpan.FromSeconds(10); // Stay open for 10 seconds
	options.OperationTimeout = TimeSpan.FromSeconds(5); // Timeout operations at 5 seconds
	options.MaxHalfOpenTests = 2; // Allow 2 test requests in half-open
});

// Configure timeout manager for slow operations
builder.Services.ConfigureTimeoutManager(options =>
{
	// Add named timeouts for different operation types
	options.DefaultTimeout = TimeSpan.FromSeconds(5);
	options.OperationTimeouts["notification"] = TimeSpan.FromSeconds(2);
	options.OperationTimeouts["payment"] = TimeSpan.FromSeconds(10);
	options.OperationTimeouts["inventory"] = TimeSpan.FromSeconds(3);
});

// Configure bulkhead for notification service
builder.Services.AddBulkhead("notification-bulkhead", options =>
{
	options.MaxConcurrency = 5; // Max 5 concurrent notifications
	options.MaxQueueLength = 10; // Queue up to 10 more
	options.OperationTimeout = TimeSpan.FromSeconds(5); // Timeout operations after 5s
});

// ============================================================
// Register external services (simulated)
// ============================================================
builder.Services.AddSingleton<FlakyPaymentService>();
builder.Services.AddSingleton<UnreliableInventoryService>();
builder.Services.AddSingleton<SlowNotificationService>();

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

// Get services for demo configuration
var paymentService = host.Services.GetRequiredService<FlakyPaymentService>();
var inventoryService = host.Services.GetRequiredService<UnreliableInventoryService>();
var notificationService = host.Services.GetRequiredService<SlowNotificationService>();

logger.LogInformation("Starting Retry and Circuit Breaker Sample...");
logger.LogInformation("");

await host.StartAsync().ConfigureAwait(false);

// ============================================================
// Demo 1: Retry with Exponential Backoff
// ============================================================
logger.LogInformation("=== Demo 1: Retry with Exponential Backoff ===");
logger.LogInformation("");
logger.LogInformation("Retry pattern handles transient failures:");
logger.LogInformation("  - Automatic retry with configurable attempts");
logger.LogInformation("  - Exponential backoff prevents overwhelming services");
logger.LogInformation("  - Jitter prevents thundering herd");
logger.LogInformation("");

// Configure payment service to fail twice then succeed
paymentService.SetFailuresBeforeSuccess(2);

var paymentCommand = new ProcessPaymentCommand { PaymentId = "PAY-001", Amount = 99.99m, CustomerId = "CUST-12345", };

logger.LogInformation("Sending payment (will fail 2 times, then succeed)...");
try
{
	_ = await dispatcher.DispatchAsync(paymentCommand, context, cancellationToken: default).ConfigureAwait(false);
	logger.LogInformation("Payment completed successfully!");
}
catch (Exception ex)
{
	logger.LogError("Payment failed after retries: {Error}", ex.Message);
}

await Task.Delay(500).ConfigureAwait(false);

// ============================================================
// Demo 2: Circuit Breaker Pattern
// ============================================================
logger.LogInformation("");
logger.LogInformation("=== Demo 2: Circuit Breaker Pattern ===");
logger.LogInformation("");
logger.LogInformation("Circuit breaker protects failing dependencies:");
logger.LogInformation("  - CLOSED: Normal operation, requests flow through");
logger.LogInformation("  - OPEN: After failures, requests fail fast");
logger.LogInformation("  - HALF-OPEN: Test requests to check recovery");
logger.LogInformation("");

// First, healthy requests
inventoryService.SetHealth(true);
var inventoryCommand = new CheckInventoryCommand { ProductSku = "WIDGET-001", RequestedQuantity = 5, };

logger.LogInformation("Checking inventory (healthy service)...");
await dispatcher.DispatchAsync(inventoryCommand, context, cancellationToken: default).ConfigureAwait(false);

await Task.Delay(300).ConfigureAwait(false);

// Now simulate service degradation
logger.LogInformation("");
logger.LogInformation("Simulating inventory service failure...");
inventoryService.SetHealth(false);

for (var i = 1; i <= 5; i++)
{
	try
	{
		var cmd = new CheckInventoryCommand { ProductSku = $"SKU-{i:D3}", RequestedQuantity = i };
		_ = await dispatcher.DispatchAsync(cmd, context, cancellationToken: default).ConfigureAwait(false);
	}
	catch (Exception ex)
	{
		logger.LogWarning("Request {N} failed: {Error}", i, ex.GetType().Name);
	}

	await Task.Delay(100).ConfigureAwait(false);
}

// Restore health
logger.LogInformation("");
logger.LogInformation("Restoring inventory service health...");
inventoryService.SetHealth(true);

await Task.Delay(500).ConfigureAwait(false);

// ============================================================
// Demo 3: Timeout Handling
// ============================================================
logger.LogInformation("");
logger.LogInformation("=== Demo 3: Timeout Handling ===");
logger.LogInformation("");
logger.LogInformation("Timeouts prevent indefinite waits:");
logger.LogInformation("  - Per-operation timeout configuration");
logger.LogInformation("  - Cancellation on timeout");
logger.LogInformation("  - Graceful degradation on timeout");
logger.LogInformation("");

// Fast notification
notificationService.SetResponseDelay(TimeSpan.FromMilliseconds(100));
var fastNotification = new SendNotificationCommand
{
	NotificationType = "Email",
	Recipient = "user@example.com",
	Message = "Your order has shipped!",
};

logger.LogInformation("Sending fast notification (100ms delay)...");
await dispatcher.DispatchAsync(fastNotification, context, cancellationToken: default).ConfigureAwait(false);

await Task.Delay(300).ConfigureAwait(false);

// ============================================================
// Configuration Reference
// ============================================================
logger.LogInformation("");
logger.LogInformation("=== Resilience Configuration Reference ===");
logger.LogInformation("");
logger.LogInformation("Retry Policy Options:");
logger.LogInformation("  | Option              | Default     | Description                      |");
logger.LogInformation("  |---------------------|-------------|----------------------------------|");
logger.LogInformation("  | MaxRetries          | 3           | Maximum retry attempts           |");
logger.LogInformation("  | BaseDelay           | 1 second    | Initial delay between retries    |");
logger.LogInformation("  | BackoffStrategy     | Exponential | Linear, Exponential, or Constant |");
logger.LogInformation("  | UseJitter           | true        | Add randomness to delays         |");
logger.LogInformation("  | JitterFactor        | 0.2         | Jitter magnitude (0.0-1.0)       |");
logger.LogInformation("  | MaxDelay            | 1 minute    | Maximum delay cap                |");
logger.LogInformation("");
logger.LogInformation("Circuit Breaker Options:");
logger.LogInformation("  | Option              | Default     | Description                      |");
logger.LogInformation("  |---------------------|-------------|----------------------------------|");
logger.LogInformation("  | FailureThreshold    | 5           | Failures to open circuit         |");
logger.LogInformation("  | SuccessThreshold    | 3           | Successes to close circuit       |");
logger.LogInformation("  | OpenDuration        | 30 seconds  | Time circuit stays open          |");
logger.LogInformation("  | OperationTimeout    | 5 seconds   | Timeout for each operation       |");
logger.LogInformation("  | MaxHalfOpenTests    | 3           | Max concurrent half-open tests   |");

// ============================================================
// Best Practices
// ============================================================
logger.LogInformation("");
logger.LogInformation("=== Best Practices ===");
logger.LogInformation("");
logger.LogInformation("1. RETRY: Use for transient failures (network, timeouts)");
logger.LogInformation("2. CIRCUIT BREAKER: Protect against cascading failures");
logger.LogInformation("3. TIMEOUT: Always set timeouts for external calls");
logger.LogInformation("4. BULKHEAD: Isolate critical resources");
logger.LogInformation("5. COMBINE: Use retry + circuit breaker + timeout together");
logger.LogInformation("");
logger.LogInformation("Example combined policy:");
logger.LogInformation("  Retry(3) -> CircuitBreaker(5, 30s) -> Timeout(10s)");

logger.LogInformation("");
logger.LogInformation("Sample completed. Press Ctrl+C to exit...");

// Wait for shutdown signal
await host.WaitForShutdownAsync().ConfigureAwait(false);

#pragma warning restore CA1506
#pragma warning restore CA1303
