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
/// Saga step that sends order confirmation.
/// </summary>
public class SendConfirmationStep : SagaStepBase<OrderSagaData>
{
 private readonly ILogger<SendConfirmationStep> _logger;

 public override string Name => "SendConfirmation";
 
 public override bool CanCompensate => false; // Notifications don't need compensation

 public SendConfirmationStep(ILogger<SendConfirmationStep> logger)
 {
 _logger = logger;
 }

 public override async Task<StepResult> ExecuteAsync(SagaExecutionContext<OrderSagaData> context,
 CancellationToken cancellationToken = default)
 {
 _logger.LogInformation(
 "Sending order confirmation for order {OrderId} to customer {CustomerId}",
 context.Data.OrderId, context.Data.CustomerId);

 try
 {
 // Simulate email service call
 await Task.Delay(TimeSpan.FromMilliseconds(300), cancellationToken);

 var estimatedDelivery = context.SharedContext.TryGetValue("estimated_delivery", out var delivery)
 ? delivery
 : DateTime.UtcNow.AddDays(3);

 _logger.LogInformation(
 "Order confirmation sent successfully. Order: {OrderId}, " +
 "Transaction: {TransactionId}, Shipment: {ShipmentId}, " +
 "Estimated Delivery: {EstimatedDelivery}",
 context.Data.OrderId,
 context.Data.PaymentTransactionId,
 context.Data.ShipmentId,
 estimatedDelivery);

 return StepResult.Success();
 }
 catch (Exception ex)
 {
 _logger.LogError(ex, "Failed to send confirmation");
 // Don't fail the saga for notification failures
 return StepResult.Success();
 }
 }
}
