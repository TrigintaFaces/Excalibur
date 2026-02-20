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
/// Saga step that processes payment for the order.
/// </summary>
public class ProcessPaymentStep : SagaStepBase<OrderSagaData>
{
 private readonly ILogger<ProcessPaymentStep> _logger;

 public override string Name => "ProcessPayment";
 
 public override TimeSpan Timeout => TimeSpan.FromMinutes(2);

 public ProcessPaymentStep(ILogger<ProcessPaymentStep> logger)
 {
 _logger = logger;
 }

 public override async Task<StepResult> ExecuteAsync(SagaExecutionContext<OrderSagaData> context,
 CancellationToken cancellationToken = default)
 {
 _logger.LogInformation(
 "Processing payment of {Amount} for order {OrderId} using {PaymentMethod}",
 context.Data.TotalAmount, context.Data.OrderId, context.Data.PaymentMethod);

 try
 {
 // Simulate payment gateway call
 await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);

 // Simulate occasional payment failures
 if (Random.Shared.NextDouble() < 0.1) // 10% failure rate
 {
 return StepResult.Retry(
 TimeSpan.FromSeconds(5),
 "Payment gateway temporarily unavailable");
 }

 // Generate transaction ID
 context.Data.PaymentTransactionId = $"TXN-{Guid.NewGuid():N}";
 
 _logger.LogInformation(
 "Payment processed successfully. Transaction ID: {TransactionId}",
 context.Data.PaymentTransactionId);

 // Store transaction details in shared context
 context.SharedContext["payment_timestamp"] = DateTime.UtcNow;
 context.SharedContext["payment_gateway"] = "StripeGateway";

 return StepResult.Success();
 }
 catch (Exception ex)
 {
 _logger.LogError(ex, "Failed to process payment");
 return StepResult.Failure("Payment processing failed", ex);
 }
 }

 public override async Task<StepResult> CompensateAsync(SagaExecutionContext<OrderSagaData> context,
 CancellationToken cancellationToken = default)
 {
 if (string.IsNullOrEmpty(context.Data.PaymentTransactionId))
 {
 return StepResult.Success(); // No payment to refund
 }

 _logger.LogInformation(
 "Refunding payment transaction {TransactionId}",
 context.Data.PaymentTransactionId);

 try
 {
 // Simulate refund process
 await Task.Delay(TimeSpan.FromMilliseconds(800), cancellationToken);
 
 _logger.LogInformation("Payment refunded successfully");
 context.Data.PaymentTransactionId = null;
 
 return StepResult.Success();
 }
 catch (Exception ex)
 {
 _logger.LogError(ex, "Failed to refund payment");
 return StepResult.Failure("Refund failed", ex);
 }
 }
}
