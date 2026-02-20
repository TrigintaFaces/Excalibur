// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Threading;
using System.Threading.Tasks;
using Excalibur.Dispatch.CloudNative.Patterns.Sagas.Implementation;
using Excalibur.Dispatch.CloudNative.Patterns.Sagas.Models;
using Microsoft.Extensions.Logging;

namespace Excalibur.Dispatch.Examples.Patterns.Sagas.OrderProcessing;

/// <summary>
/// Saga step that reserves inventory for the order.
/// </summary>
public class ReserveInventoryStep : SagaStepBase<OrderSagaData>
{
 private readonly ILogger<ReserveInventoryStep> _logger;

 public override string Name => "ReserveInventory";

 public ReserveInventoryStep(ILogger<ReserveInventoryStep> logger)
 {
 _logger = logger;
 }

 public override async Task<StepResult> ExecuteAsync(SagaExecutionContext<OrderSagaData> context,
 CancellationToken cancellationToken = default)
 {
 _logger.LogInformation("Reserving inventory for order {OrderId}", context.Data.OrderId);

 try
 {
 // Simulate inventory service call
 await Task.Delay(TimeSpan.FromMilliseconds(500), cancellationToken);

 // Check if all items are in stock
 foreach (var item in context.Data.Items)
 {
 _logger.LogInformation(
 "Checking inventory for {ProductName} (Quantity: {Quantity})",
 item.ProductName, item.Quantity);
 }

 // Generate reservation ID
 context.Data.InventoryReservationId = $"RES-{Guid.NewGuid():N}";
 
 _logger.LogInformation(
 "Inventory reserved successfully. Reservation ID: {ReservationId}",
 context.Data.InventoryReservationId);

 return StepResult.Success();
 }
 catch (Exception ex)
 {
 _logger.LogError(ex, "Failed to reserve inventory");
 return StepResult.Failure("Failed to reserve inventory", ex);
 }
 }

 public override async Task<StepResult> CompensateAsync(SagaExecutionContext<OrderSagaData> context,
 CancellationToken cancellationToken = default)
 {
 if (string.IsNullOrEmpty(context.Data.InventoryReservationId))
 {
 return StepResult.Success(); // Nothing to compensate
 }

 _logger.LogInformation(
 "Releasing inventory reservation {ReservationId}",
 context.Data.InventoryReservationId);

 try
 {
 // Simulate inventory release
 await Task.Delay(TimeSpan.FromMilliseconds(200), cancellationToken);
 
 context.Data.InventoryReservationId = null;
 
 _logger.LogInformation("Inventory reservation released successfully");
 return StepResult.Success();
 }
 catch (Exception ex)
 {
 _logger.LogError(ex, "Failed to release inventory reservation");
 return StepResult.Failure("Failed to release inventory", ex);
 }
 }
}
