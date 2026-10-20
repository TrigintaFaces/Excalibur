// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

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

using Excalibur.Dispatch;
using Excalibur.Dispatch.Configuration;
using Excalibur.Dispatch.Messaging;
using Excalibur.Dispatch.Resilience;
using Excalibur.Dispatch.Resilience.Polly;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using RetryAndCircuitBreaker.Messages;
using RetryAndCircuitBreaker.Services;

using Polly.Timeout;

// Build configuration
var verify = args.Contains("--verify", StringComparer.Ordinal);
var builder = new HostApplicationBuilder(args.Where(argument => argument != "--verify").ToArray());

// Add appsettings.json configuration
builder.Configuration.AddJsonFile("appsettings.json", optional: true, reloadOnChange: true);

// Configure logging for visibility
builder.Services.AddLogging(logging =>
{
	_ = logging.AddConsole();
	_ = logging.SetMinimumLevel(LogLevel.Information);
});

// ============================================================
// Configure Dispatch messaging with resilience
// ============================================================
builder.Services.AddDispatch(dispatch =>
{
	_ = dispatch.AddHandlersFromAssembly(typeof(Program).Assembly);

	// Register JSON serializer via the builder serialization API

	// Add Polly resilience infrastructure via the builder
	_ = dispatch.UseResilience();
});

// ============================================================
// Configure Named Resilience Policies
// ============================================================
// Named policies are registered on IServiceCollection because each
// targets a specific downstream dependency with its own thresholds.

// Retry with exponential backoff and jitter for payment service
builder.Services.AddPollyRetryPolicy("payment-retry", options =>
{
	options.MaxRetryAttempts = 5; // Retry up to 5 times
	options.BaseDelay = TimeSpan.FromMilliseconds(verify ? 10 : 200);
	options.BackoffStrategy = BackoffStrategy.Exponential; // 200ms -> 400ms -> 800ms -> etc.
	options.UseJitter = !verify; // The demonstration uses jitter; verification keeps delays deterministic.
	options.MaxDelay = verify ? TimeSpan.FromMilliseconds(100) : TimeSpan.FromSeconds(10);
	options.EnableDetailedLogging = true; // Log retry attempts
	options.ShouldRetry = ex => ex is PaymentServiceException; // Only retry payment exceptions
});

// Circuit breaker for inventory service
builder.Services.AddPollyCircuitBreaker("inventory-circuit", options =>
{
	options.MinimumThroughput = verify ? 2 : 4;
	options.FailureRatio = verify ? 1 : 0.75;
	options.BreakDuration = TimeSpan.FromSeconds(10); // Stay open for 10 seconds
	options.OperationTimeout = TimeSpan.FromSeconds(5); // Timeout operations at 5 seconds

	// There is no "close after N successes" or "N concurrent half-open tests" setting. The circuit
	// admits ONE trial call while half-open and closes if it succeeds. That is deliberate: several
	// concurrent trial calls re-create the surge the breaker exists to prevent, and waiting for a
	// run of successes delays recovery without making it safer.
});

// Bulkhead for notification service
builder.Services.AddBulkhead("notification-bulkhead", options =>
{
	options.MaxConcurrency = 5; // Max 5 concurrent notifications
	options.MaxQueueLength = 10; // Queue up to 10 more
	options.OperationTimeout = verify ? TimeSpan.FromMilliseconds(100) : TimeSpan.FromSeconds(5);
});

// ============================================================
// Register external services (simulated)
// ============================================================
builder.Services.AddSingleton<FlakyPaymentService>();
builder.Services.AddSingleton<UnreliableInventoryService>();
builder.Services.AddSingleton<SlowNotificationService>();

// ============================================================
// Build and start the host
// ============================================================
using var host = builder.Build();

var logger = host.Services.GetRequiredService<ILogger<Program>>();
var dispatcher = host.Services.GetRequiredService<IDispatcher>();

// Get services for demo configuration
var paymentService = host.Services.GetRequiredService<FlakyPaymentService>();
var inventoryService = host.Services.GetRequiredService<UnreliableInventoryService>();
var notificationService = host.Services.GetRequiredService<SlowNotificationService>();

logger.LogInformation("Starting Retry and Circuit Breaker Sample...");
logger.LogInformation("");

await host.StartAsync().ConfigureAwait(false);

if (verify)
{
	using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(20));
	var token = deadline.Token;
	async Task<IMessageResult> DispatchAsync(IDispatchAction command) =>
		await dispatcher.DispatchAsync(command, DispatchContextInitializer.CreateDefaultContext(host.Services), token)
			.ConfigureAwait(false);
	static void Require(bool condition, string message)
	{
		if (!condition)
		{
			throw new InvalidOperationException(message);
		}
	}
	async Task ExpectAsync<TException>(IDispatchAction command) where TException : Exception
	{
		try
		{
			await DispatchAsync(command).ConfigureAwait(false);
		}
		catch (TException)
		{
			return;
		}

		throw new InvalidOperationException($"Expected {typeof(TException).Name}.");
	}

	paymentService.SetFailuresBeforeSuccess(2);
	var payment = await DispatchAsync(new ProcessPaymentCommand
	{
		PaymentId = "VERIFY-PAYMENT", Amount = 10, CustomerId = "VERIFY-CUSTOMER",
	}).ConfigureAwait(false);
	Require(payment.Succeeded && paymentService.CallCount == 3 && paymentService.CompletedPayments == 1,
		"Dependency retry must make exactly three attempts and complete one payment.");
	paymentService.SetFailuresBeforeSuccess(1000);
	await ExpectAsync<PaymentServiceException>(new ProcessPaymentCommand
	{
		PaymentId = "VERIFY-PERSISTENT-FAILURE", Amount = 10, CustomerId = "VERIFY-CUSTOMER",
	}).ConfigureAwait(false);
	Require(paymentService.CallCount == 6 && paymentService.CompletedPayments == 0,
		"Five retries must bound persistent failure to six total dependency attempts and no payment effect.");

	var inventory = new CheckInventoryCommand { ProductSku = "VERIFY-SKU", RequestedQuantity = 1 };
	inventoryService.SetHealth(false);
	await ExpectAsync<InventoryServiceException>(inventory).ConfigureAwait(false);
	await ExpectAsync<InventoryServiceException>(inventory).ConfigureAwait(false);
	var callsBeforeRefusal = inventoryService.CallCount;
	await ExpectAsync<CircuitBreakerOpenException>(inventory).ConfigureAwait(false);
	Require(inventoryService.CallCount == callsBeforeRefusal, "Open breaker must not call the dependency.");
	inventoryService.SetHealth(true);
	await host.Services.GetRequiredKeyedService<ICircuitBreakerPolicy>("inventory-circuit")
		.ResetAsync(token).ConfigureAwait(false);
	Require((await DispatchAsync(inventory).ConfigureAwait(false)).Succeeded, "Manual breaker recovery must succeed.");

	var notification = new SendNotificationCommand
	{
		NotificationType = "Email", Recipient = "verify@example.com", Message = "Verification",
	};
	notificationService.SetResponseDelay(System.Threading.Timeout.InfiniteTimeSpan);
	await ExpectAsync<TimeoutRejectedException>(notification).ConfigureAwait(false);
	var bulkhead = host.Services.GetRequiredKeyedService<IBulkheadPolicy>("notification-bulkhead");
	Require(notificationService.CanceledNotifications == 1 && notificationService.CompletedNotifications == 0 &&
		bulkhead.GetMetrics().ActiveExecutions == 0, "Timeout must cancel the dependency and release capacity without completing an effect.");
	notificationService.SetResponseDelay(TimeSpan.Zero);
	Require((await DispatchAsync(notification).ConfigureAwait(false)).Succeeded && notificationService.CompletedNotifications == 1,
		"A healthy operation must complete after timeout recovery.");
	Console.WriteLine("RESILIENCE_SAMPLE_VERIFIED: payment attempts=3 effects=1; breaker refusal and manual recovery; timeout cancellation and permit recovery");
	await host.StopAsync(token).ConfigureAwait(false);
	return;
}

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
	var paymentResult = await dispatcher.DispatchAsync(paymentCommand, DispatchContextInitializer.CreateDefaultContext(host.Services), cancellationToken: default).ConfigureAwait(false);
	if (!paymentResult.Succeeded || paymentService.CallCount != 3 || paymentService.CompletedPayments != 1)
	{
		throw new InvalidOperationException("Payment recovery did not produce three attempts and one completed effect.");
	}
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
await dispatcher.DispatchAsync(inventoryCommand, DispatchContextInitializer.CreateDefaultContext(host.Services), cancellationToken: default).ConfigureAwait(false);

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
		_ = await dispatcher.DispatchAsync(cmd, DispatchContextInitializer.CreateDefaultContext(host.Services), cancellationToken: default).ConfigureAwait(false);
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

await host.Services.GetRequiredKeyedService<ICircuitBreakerPolicy>("inventory-circuit")
	.ResetAsync(CancellationToken.None).ConfigureAwait(false);
var recoveredInventory = await dispatcher.DispatchAsync(inventoryCommand, DispatchContextInitializer.CreateDefaultContext(host.Services), cancellationToken: default).ConfigureAwait(false);
if (!recoveredInventory.Succeeded)
{
	throw new InvalidOperationException("Inventory did not recover after manual circuit reset.");
}
logger.LogInformation("Inventory recovered after an explicit manual circuit reset.");

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
await dispatcher.DispatchAsync(fastNotification, DispatchContextInitializer.CreateDefaultContext(host.Services), cancellationToken: default).ConfigureAwait(false);

notificationService.SetResponseDelay(System.Threading.Timeout.InfiniteTimeSpan);
try
{
	await dispatcher.DispatchAsync(fastNotification, DispatchContextInitializer.CreateDefaultContext(host.Services), cancellationToken: default).ConfigureAwait(false);
	throw new InvalidOperationException("The slow notification did not time out.");
}
catch (TimeoutRejectedException)
{
	logger.LogInformation("Notification timeout canceled the dependency operation.");
}

notificationService.SetResponseDelay(TimeSpan.Zero);
await dispatcher.DispatchAsync(fastNotification, DispatchContextInitializer.CreateDefaultContext(host.Services), cancellationToken: default).ConfigureAwait(false);
var notificationBulkhead = host.Services.GetRequiredKeyedService<IBulkheadPolicy>("notification-bulkhead");
if (notificationService.CanceledNotifications != 1 || notificationService.CompletedNotifications != 2 ||
	notificationBulkhead.GetMetrics().ActiveExecutions != 0)
{
	throw new InvalidOperationException("Notification timeout and capacity recovery were not observed.");
}

logger.LogInformation("Sample completed.");

// Wait for shutdown signal
// The demo is a fixed script, not a service: everything above has run, so stop the host and
// exit. Waiting for shutdown here would leave a finished sample sitting idle until killed.
await host.StopAsync().ConfigureAwait(false);

#pragma warning restore CA1506
#pragma warning restore CA1303
