// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Threading;
using System.Threading.Tasks;
using Excalibur.Dispatch.CloudNative.Patterns.Sagas.Abstractions;
using Excalibur.Dispatch.CloudNative.Patterns.Sagas.Implementation;
using Excalibur.Dispatch.CloudNative.Patterns.Sagas.Models;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace examples.Dispatch.Examples.Patterns.Sagas;

/// <summary>
/// Example worker demonstrating saga orchestration.
/// </summary>
public class SagaExampleWorker : BackgroundService
{
 private readonly ISagaOrchestrator _orchestrator;
 private readonly ILogger<SagaExampleWorker> _logger;

 public SagaExampleWorker(
 ISagaOrchestrator orchestrator,
 ILogger<SagaExampleWorker> logger)
 {
 _orchestrator = orchestrator;
 _logger = logger;
 }

 protected override async Task ExecuteAsync(CancellationToken stoppingToken)
 {
 // Example 1: Simple order processing saga
 await ExecuteOrderProcessingSagaAsync();

 // Example 2: Saga with compensation
 await ExecuteSagaWithCompensationAsync();

 // Example 3: Check saga state from cache
 await CheckSagaStateAsync();

 // Wait for cancellation
 await Task.Delay(Timeout.Infinite, stoppingToken);
 }

 private async Task ExecuteOrderProcessingSagaAsync()
 {
 _logger.LogInformation("Starting order processing saga example");

 var orderSaga = SagaBuilder<OrderData>
 .Create()
 .WithId("order-processing-saga")
 .WithName("Order Processing Saga")
 .WithTimeout(TimeSpan.FromMinutes(5))
 .WithRetryPolicy(DefaultSagaRetryPolicy.ExponentialBackoff(3))
 .AddStep("validate-order", 
 execute: async (context, ct) =>
 {
 _logger.LogInformation("Validating order {OrderId}", context.Data.OrderId);
 
 // Simulate validation
 await Task.Delay(100, ct);
 
 if (context.Data.TotalAmount <= 0)
 {
 return StepResult.Failure("Invalid order amount");
 }
 
 context.SetStepData("validated", true);
 return StepResult.Success();
 },
 compensate: async (context, ct) =>
 {
 _logger.LogInformation("Compensating order validation");
 await Task.Delay(50, ct);
 return StepResult.Success();
 })
 .AddStep("reserve-inventory",
 execute: async (context, ct) =>
 {
 _logger.LogInformation("Reserving inventory for {ProductCount} products", 
 context.Data.ProductIds.Length);
 
 // Simulate inventory reservation
 await context.Dispatcher.SendAsync(new
 {
 Type = "ReserveInventory",
 OrderId = context.Data.OrderId,
 Products = context.Data.ProductIds
 }, ct);
 
 context.SetStepData("reservationId", Guid.NewGuid().ToString());
 return StepResult.Success();
 },
 compensate: async (context, ct) =>
 {
 _logger.LogInformation("Releasing inventory reservation");
 
 var reservationId = context.StepData["reservationId"];
 await context.Dispatcher.SendAsync(new
 {
 Type = "ReleaseInventory",
 ReservationId = reservationId
 }, ct);
 
 return StepResult.Success();
 })
 .AddStep("charge-payment",
 execute: async (context, ct) =>
 {
 _logger.LogInformation("Charging payment of {Amount:C}", context.Data.TotalAmount);
 
 // Simulate payment processing
 await context.Dispatcher.SendAsync(new
 {
 Type = "ChargePayment",
 CustomerId = context.Data.CustomerId,
 Amount = context.Data.TotalAmount,
 Method = context.Data.PaymentMethod
 }, ct);
 
 context.SetStepData("transactionId", $"TXN-{Guid.NewGuid()}");
 return StepResult.Success();
 },
 compensate: async (context, ct) =>
 {
 _logger.LogInformation("Refunding payment");
 
 var transactionId = context.StepData["transactionId"];
 await context.Dispatcher.SendAsync(new
 {
 Type = "RefundPayment",
 TransactionId = transactionId
 }, ct);
 
 return StepResult.Success();
 })
 .AddStep("ship-order",
 execute: async (context, ct) =>
 {
 _logger.LogInformation("Creating shipment for order {OrderId}", 
 context.Data.OrderId);
 
 await context.Dispatcher.SendAsync(new
 {
 Type = "CreateShipment",
 OrderId = context.Data.OrderId,
 CustomerId = context.Data.CustomerId
 }, ct);
 
 return StepResult.Success();
 },
 compensate: null) // Shipping cannot be compensated
 .Build();

 var orderData = new OrderData
 {
 OrderId = $"ORDER-{DateTime.UtcNow:yyyyMMddHHmmss}",
 CustomerId = "CUST-123",
 TotalAmount = 299.99m,
 ProductIds = new[] { "PROD-001", "PROD-002" },
 PaymentMethod = "CreditCard"
 };

 var result = await _orchestrator.ExecuteSagaAsync(
 orderSaga, orderData, $"order-{orderData.OrderId}");

 _logger.LogInformation(
 "Order saga completed - Success: {Success}, Duration: {Duration}ms, Steps: {Steps}/{Total}",
 result.IsSuccess, result.Duration.TotalMilliseconds, 
 result.CompletedSteps, result.TotalSteps);
 }

 private async Task ExecuteSagaWithCompensationAsync()
 {
 _logger.LogInformation("Starting saga with compensation example");

 var failingSaga = SagaBuilder<OrderData>
 .Create()
 .WithId("failing-saga")
 .WithName("Failing Saga Demo")
 .AddStep("step-1",
 execute: async (context, ct) =>
 {
 _logger.LogInformation("Executing step 1 - will succeed");
 await Task.Delay(100, ct);
 return StepResult.Success();
 },
 compensate: async (context, ct) =>
 {
 _logger.LogInformation("Compensating step 1");
 await Task.Delay(50, ct);
 return StepResult.Success();
 })
 .AddStep("step-2",
 execute: async (context, ct) =>
 {
 _logger.LogInformation("Executing step 2 - will succeed");
 await Task.Delay(100, ct);
 return StepResult.Success();
 },
 compensate: async (context, ct) =>
 {
 _logger.LogInformation("Compensating step 2");
 await Task.Delay(50, ct);
 return StepResult.Success();
 })
 .AddStep("step-3",
 execute: async (context, ct) =>
 {
 _logger.LogInformation("Executing step 3 - will fail");
 await Task.Delay(100, ct);
 // Simulate failure
 return StepResult.Failure("Simulated failure in step 3");
 },
 compensate: async (context, ct) =>
 {
 _logger.LogInformation("Step 3 compensation not needed (never executed)");
 return StepResult.Success();
 })
 .Build();

 var testData = new OrderData
 {
 OrderId = "TEST-COMPENSATION",
 CustomerId = "CUST-999",
 TotalAmount = 100m
 };

 var result = await _orchestrator.ExecuteSagaAsync(failingSaga, testData);

 _logger.LogInformation(
 "Failing saga result - Success: {Success}, Status: {Status}, " +
 "CompensationPerformed: {Compensation}, FailedStep: {FailedStep}",
 result.IsSuccess, result.Status, 
 result.CompensationPerformed, result.FailedStep);
 }

 private async Task CheckSagaStateAsync()
 {
 _logger.LogInformation("Checking saga states example");

 // Get recent saga instances
 var filter = new SagaInstanceFilter
 {
 CreatedAfter = DateTime.UtcNow.AddHours(-1),
 MaxResults = 10
 };

 var instances = await _orchestrator.GetSagaInstancesAsync(filter);

 foreach (var instance in instances)
 {
 _logger.LogInformation(
 "Saga instance {InstanceId} - Status: {Status}, Created: {Created}, " +
 "Current Step: {Step}/{Total}",
 instance.InstanceId, instance.Status, instance.CreatedAt,
 instance.CurrentStepIndex + 1, instance.StepStates.Count);

 // Demonstrate cached state retrieval
 var cachedState = await _orchestrator.GetSagaStateAsync(instance.InstanceId);
 if (cachedState != null)
 {
 _logger.LogInformation("Retrieved saga state from cache in < 5ms");
 }
 }
 }
}

/// <summary>
/// Custom saga step implementation example.
/// </summary>
public class EmailNotificationStep : ISagaStep<OrderData>
{
 private readonly ILogger<EmailNotificationStep> _logger;

 public EmailNotificationStep(ILogger<EmailNotificationStep> logger)
 {
 _logger = logger;
 }

 public string Name => "send-email-notification";
 public TimeSpan? Timeout => TimeSpan.FromSeconds(30);
 public bool CanCompensate => false; // Emails cannot be "unsent"

 public async Task<StepResult> ExecuteAsync(ISagaContext<OrderData> context,
 CancellationToken cancellationToken = default)
 {
 try
 {
 _logger.LogInformation("Sending order confirmation email to customer {CustomerId}",
 context.Data.CustomerId);

 await context.Dispatcher.SendAsync(new
 {
 Type = "SendEmail",
 To = $"customer-{context.Data.CustomerId}@example.com",
 Subject = $"Order {context.Data.OrderId} Confirmation",
 Template = "OrderConfirmation",
 Data = context.Data
 }, cancellationToken);

 return StepResult.Success();
 }
 catch (Exception ex)
 {
 _logger.LogError(ex, "Failed to send email notification");
 // Email failure shouldn't fail the entire saga
 return StepResult.Success(); 
 }
 }

 public Task<StepResult> CompensateAsync(ISagaContext<OrderData> context,
 CancellationToken cancellationToken = default)
 {
 // Cannot compensate email sending
 return Task.FromResult(StepResult.Success());
 }
}
