using System;
using System.Threading.Tasks;
using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask;
using Microsoft.Extensions.Logging;
using Excalibur.Hosting.AzureFunctions;

namespace examples.Excalibur.Dispatch.Examples.Serverless.Azure.Sagas;

/// <summary>
/// Example of an e-commerce order processing saga with payment, inventory, and shipping steps.
/// </summary>
public class OrderProcessingSaga : SagaOrchestrationBase<OrderRequest, OrderResult>
{
 /// <summary>
 /// Initializes a new instance of the <see cref="OrderProcessingSaga"/> class.
 /// </summary>
 public OrderProcessingSaga(ILogger<OrderProcessingSaga> logger) : base(logger)
 {
 }

 /// <inheritdoc />
 protected override string SagaName => "OrderProcessing";

 /// <inheritdoc />
 protected override TimeSpan SagaTimeout => TimeSpan.FromMinutes(10);

 /// <inheritdoc />
 protected override void ConfigureSteps()
 {
 // Step 1: Validate order
 AddStep<OrderRequest, ValidationResult>(
 "ValidateOrder",
 "ValidateOrderActivity",
 (input, state) => input,
 (output, state) => state.CustomData["ValidationResult"] = output);

 // Step 2: Check inventory
 AddStep<InventoryRequest, InventoryResult>(
 "CheckInventory",
 "CheckInventoryActivity",
 (input, state) => new InventoryRequest
 {
 OrderId = input.OrderId,
 Items = input.Items
 },
 (output, state) => state.CustomData["InventoryResult"] = output,
 "ReleaseInventoryActivity");

 // Step 3: Process payment
 AddStep<PaymentRequest, PaymentResult>(
 "ProcessPayment",
 "ProcessPaymentActivity",
 (input, state) => new PaymentRequest
 {
 OrderId = input.OrderId,
 Amount = input.TotalAmount,
 CustomerId = input.CustomerId,
 PaymentMethod = input.PaymentMethod
 },
 (output, state) => state.CustomData["PaymentResult"] = output,
 "RefundPaymentActivity");

 // Step 4: Create shipment
 AddStep<ShipmentRequest, ShipmentResult>(
 "CreateShipment",
 "CreateShipmentActivity",
 (input, state) => new ShipmentRequest
 {
 OrderId = input.OrderId,
 Items = input.Items,
 ShippingAddress = input.ShippingAddress,
 ShippingMethod = input.ShippingMethod
 },
 (output, state) => state.CustomData["ShipmentResult"] = output,
 "CancelShipmentActivity");

 // Step 5: Send confirmation email
 AddStep<EmailRequest, EmailResult>(
 "SendConfirmationEmail",
 "SendEmailActivity",
 (input, state) => new EmailRequest
 {
 To = input.CustomerEmail,
 Subject = $"Order {input.OrderId} Confirmed",
 Body = BuildConfirmationEmail(input, state)
 },
 (output, state) => state.CustomData["EmailResult"] = output);
 }

 /// <inheritdoc />
 protected override async Task ValidateInputAsync(
 TaskOrchestrationContext context,
 OrderRequest input,
 SagaState sagaState)
 {
 if (string.IsNullOrEmpty(input.OrderId))
 {
 throw new ArgumentException("OrderId is required");
 }

 if (input.Items == null || input.Items.Count == 0)
 {
 throw new ArgumentException("Order must contain at least one item");
 }

 if (input.TotalAmount <= 0)
 {
 throw new ArgumentException("Order total must be greater than zero");
 }

 await Task.CompletedTask;
 }

 /// <inheritdoc />
 protected override Task<OrderResult> CreateOutputAsync(
 TaskOrchestrationContext context,
 OrderRequest input,
 SagaState sagaState)
 {
 var paymentResult = sagaState.CustomData["PaymentResult"] as PaymentResult;
 var shipmentResult = sagaState.CustomData["ShipmentResult"] as ShipmentResult;

 return Task.FromResult(new OrderResult
 {
 OrderId = input.OrderId,
 Status = "Completed",
 PaymentTransactionId = paymentResult?.TransactionId,
 ShipmentTrackingNumber = shipmentResult?.TrackingNumber,
 EstimatedDeliveryDate = shipmentResult?.EstimatedDeliveryDate,
 ProcessingTime = sagaState.Duration
 });
 }

 private static string BuildConfirmationEmail(OrderRequest order, SagaState state)
 {
 var shipmentResult = state.CustomData["ShipmentResult"] as ShipmentResult;

 return $@"
Dear Customer,

Your order {order.OrderId} has been successfully processed!

Order Details:
- Total Amount: ${order.TotalAmount:F2}
- Items: {order.Items.Count}
- Shipping Method: {order.ShippingMethod}

Tracking Number: {shipmentResult?.TrackingNumber}
Estimated Delivery: {shipmentResult?.EstimatedDeliveryDate:yyyy-MM-dd}

Thank you for your business!
";
 }
}

/// <summary>
/// Order request model.
/// </summary>
public class OrderRequest {
 public string OrderId { get; set; } = string.Empty;
 public string CustomerId { get; set; } = string.Empty;
 public string CustomerEmail { get; set; } = string.Empty;
 public List<OrderItem> Items { get; set; } = new();
 public decimal TotalAmount { get; set; }
 public Address ShippingAddress { get; set; } = new();
 public string ShippingMethod { get; set; } = string.Empty;
 public PaymentMethod PaymentMethod { get; set; } = new();
}

/// <summary>
/// Order result model.
/// </summary>
public class OrderResult {
 public string OrderId { get; set; } = string.Empty;
 public string Status { get; set; } = string.Empty;
 public string? PaymentTransactionId { get; set; }
 public string? ShipmentTrackingNumber { get; set; }
 public DateTime? EstimatedDeliveryDate { get; set; }
 public TimeSpan? ProcessingTime { get; set; }
}

/// <summary>
/// Order item model.
/// </summary>
public class OrderItem {
 public string ProductId { get; set; } = string.Empty;
 public string ProductName { get; set; } = string.Empty;
 public int Quantity { get; set; }
 public decimal UnitPrice { get; set; }
}

/// <summary>
/// Address model.
/// </summary>
public class Address {
 public string Street { get; set; } = string.Empty;
 public string City { get; set; } = string.Empty;
 public string State { get; set; } = string.Empty;
 public string PostalCode { get; set; } = string.Empty;
 public string Country { get; set; } = string.Empty;
}

/// <summary>
/// Payment method model.
/// </summary>
public class PaymentMethod {
 public string Type { get; set; } = string.Empty; // Credit Card, PayPal, etc.
 public string? CardNumber { get; set; }
 public string? ExpiryDate { get; set; }
}

// Request/Result models for activities

public class ValidationResult {
 public bool IsValid { get; set; }
 public List<string> Errors { get; set; } = new();
}

public class InventoryRequest {
 public string OrderId { get; set; } = string.Empty;
 public List<OrderItem> Items { get; set; } = new();
}

public class InventoryResult {
 public bool IsAvailable { get; set; }
 public Dictionary<string, int> ReservedItems { get; set; } = new();
}

public class PaymentRequest {
 public string OrderId { get; set; } = string.Empty;
 public string CustomerId { get; set; } = string.Empty;
 public decimal Amount { get; set; }
 public PaymentMethod PaymentMethod { get; set; } = new();
}

public class PaymentResult {
 public bool IsSuccessful { get; set; }
 public string TransactionId { get; set; } = string.Empty;
 public DateTime ProcessedAt { get; set; }
}

public class ShipmentRequest {
 public string OrderId { get; set; } = string.Empty;
 public List<OrderItem> Items { get; set; } = new();
 public Address ShippingAddress { get; set; } = new();
 public string ShippingMethod { get; set; } = string.Empty;
}

public class ShipmentResult {
 public string TrackingNumber { get; set; } = string.Empty;
 public DateTime EstimatedDeliveryDate { get; set; }
 public string Carrier { get; set; } = string.Empty;
}

public class EmailRequest {
 public string To { get; set; } = string.Empty;
 public string Subject { get; set; } = string.Empty;
 public string Body { get; set; } = string.Empty;
}

public class EmailResult {
 public bool IsSent { get; set; }
 public string MessageId { get; set; } = string.Empty;
}