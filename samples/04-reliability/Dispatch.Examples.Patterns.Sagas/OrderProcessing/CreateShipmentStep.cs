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
/// Saga step that creates shipment for the order.
/// </summary>
public class CreateShipmentStep : SagaStepBase<OrderSagaData>
{
 private readonly ILogger<CreateShipmentStep> _logger;

 public override string Name => "CreateShipment";

 public CreateShipmentStep(ILogger<CreateShipmentStep> logger)
 {
 _logger = logger;
 }

 public override async Task<StepResult> ExecuteAsync(SagaExecutionContext<OrderSagaData> context,
 CancellationToken cancellationToken = default)
 {
 _logger.LogInformation(
 "Creating shipment for order {OrderId} to {Address}",
 context.Data.OrderId, context.Data.ShippingAddress);

 try
 {
 // Simulate shipping service call
 await Task.Delay(TimeSpan.FromMilliseconds(700), cancellationToken);

 // Generate shipment ID
 context.Data.ShipmentId = $"SHIP-{Guid.NewGuid():N}";
 
 _logger.LogInformation(
 "Shipment created successfully. Shipment ID: {ShipmentId}",
 context.Data.ShipmentId);

 // Store shipping details
 context.SharedContext["carrier"] = "FedEx";
 context.SharedContext["estimated_delivery"] = DateTime.UtcNow.AddDays(3);

 return StepResult.Success();
 }
 catch (Exception ex)
 {
 _logger.LogError(ex, "Failed to create shipment");
 return StepResult.Failure("Shipment creation failed", ex);
 }
 }

 public override async Task<StepResult> CompensateAsync(SagaExecutionContext<OrderSagaData> context,
 CancellationToken cancellationToken = default)
 {
 if (string.IsNullOrEmpty(context.Data.ShipmentId))
 {
 return StepResult.Success(); // No shipment to cancel
 }

 _logger.LogInformation(
 "Cancelling shipment {ShipmentId}",
 context.Data.ShipmentId);

 try
 {
 // Simulate shipment cancellation
 await Task.Delay(TimeSpan.FromMilliseconds(300), cancellationToken);
 
 context.Data.ShipmentId = null;
 
 _logger.LogInformation("Shipment cancelled successfully");
 return StepResult.Success();
 }
 catch (Exception ex)
 {
 _logger.LogError(ex, "Failed to cancel shipment");
 return StepResult.Failure("Shipment cancellation failed", ex);
 }
 }
}
