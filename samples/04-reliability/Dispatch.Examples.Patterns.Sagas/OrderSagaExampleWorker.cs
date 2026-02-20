// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Excalibur.Dispatch.CloudNative.Patterns.Sagas.Abstractions;
using Excalibur.Dispatch.CloudNative.Patterns.Sagas.Models;
using examples.Dispatch.Examples.Patterns.Sagas.OrderProcessing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace examples.Dispatch.Examples.Patterns.Sagas;

/// <summary>
/// Worker that demonstrates saga orchestration.
/// </summary>
public class OrderSagaExampleWorker : BackgroundService
{
 private readonly ISagaOrchestrator _orchestrator;
 private readonly IServiceProvider _serviceProvider;
 private readonly ILogger<OrderSagaExampleWorker> _logger;

 public OrderSagaExampleWorker(
 ISagaOrchestrator orchestrator,
 IServiceProvider serviceProvider,
 ILogger<OrderSagaExampleWorker> logger)
 {
 _orchestrator = orchestrator;
 _serviceProvider = serviceProvider;
 _logger = logger;
 }

 protected override async Task ExecuteAsync(CancellationToken stoppingToken)
 {
 // Create sample order data
 var orderData = new OrderSagaData
 {
 OrderId = $"ORD-{Guid.NewGuid():N}",
 CustomerId = "CUST-12345",
 TotalAmount = 299.99m,
 PaymentMethod = "CreditCard",
 ShippingAddress = "123 Main St, Seattle, WA 98101",
 Items = new List<OrderItem>
 {
 new() { ProductId = "PROD-001", ProductName = "Laptop", Quantity = 1, Price = 999.99m },
 new() { ProductId = "PROD-002", ProductName = "Mouse", Quantity = 2, Price = 29.99m }
 }
 };

 // Define the saga
 var sagaDefinition = new SagaDefinition<OrderSagaData>
 {
 Name = "OrderProcessingSaga",
 Description = "Processes customer orders through inventory, payment, and shipping",
 Timeout = TimeSpan.FromMinutes(10),
 EnableCaching = true,
 CacheTtl = TimeSpan.FromMinutes(5)
 };

 // Add steps
 using var scope = _serviceProvider.CreateScope();
 sagaDefinition.Steps.Add(scope.ServiceProvider.GetRequiredService<ReserveInventoryStep>());
 sagaDefinition.Steps.Add(scope.ServiceProvider.GetRequiredService<ProcessPaymentStep>());
 sagaDefinition.Steps.Add(scope.ServiceProvider.GetRequiredService<CreateShipmentStep>());
 sagaDefinition.Steps.Add(scope.ServiceProvider.GetRequiredService<SendConfirmationStep>());

 _logger.LogInformation("Starting order processing saga for order {OrderId}", orderData.OrderId);

 // Start the saga
 var sagaId = await _orchestrator.StartSagaAsync(orderData, sagaDefinition, stoppingToken);
 _logger.LogInformation("Saga started with ID: {SagaId}", sagaId);

 // Monitor saga progress
 await MonitorSagaProgressAsync(sagaId, stoppingToken);

 // Demonstrate caching by checking status multiple times
 await DemonstrateCachingAsync(sagaId, stoppingToken);

 // Keep running until cancelled
 await Task.Delay(Timeout.Infinite, stoppingToken);
 }

 private async Task MonitorSagaProgressAsync(string sagaId, CancellationToken cancellationToken)
 {
 var completed = false;
 var checkCount = 0;

 while (!completed && !cancellationToken.IsCancellationRequested)
 {
 checkCount++;
 var status = await _orchestrator.GetSagaStatusAsync(sagaId, cancellationToken);
 
 if (status != null)
 {
 _logger.LogInformation(
 "[Check #{CheckCount}] Saga {SagaId} - Status: {Status}, " +
 "Progress: {Completed}/{Total} steps, Current: {CurrentStep}",
 checkCount, sagaId, status.Status, 
 status.CompletedSteps, status.TotalSteps, status.CurrentStep);

 if (status.Status == SagaStatus.Completed)
 {
 _logger.LogInformation(
 "✅ Saga completed successfully in {Duration}!",
 status.Duration);
 completed = true;
 }
 else if (status.Status == SagaStatus.Failed || status.Status == SagaStatus.Compensated)
 {
 _logger.LogError(
 "❌ Saga failed: {ErrorMessage}. Final status: {Status}",
 status.ErrorMessage, status.Status);
 completed = true;
 }
 }

 if (!completed)
 {
 await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);
 }
 }
 }

 private async Task DemonstrateCachingAsync(string sagaId, CancellationToken cancellationToken)
 {
 _logger.LogInformation("🔄 Demonstrating caching behavior...");

 // Make multiple rapid status checks
 for (int i = 0; i < 5; i++)
 {
 var start = DateTime.UtcNow;
 var status = await _orchestrator.GetSagaStatusAsync(sagaId, cancellationToken);
 var elapsed = DateTime.UtcNow - start;

 _logger.LogInformation(
 "Cache test #{Test}: Status retrieved in {Elapsed}ms (Status: {Status})",
 i + 1, elapsed.TotalMilliseconds, status?.Status);

 await Task.Delay(TimeSpan.FromMilliseconds(100), cancellationToken);
 }

 _logger.LogInformation(
 "💡 Notice how subsequent calls are faster due to caching!");
 }
}
