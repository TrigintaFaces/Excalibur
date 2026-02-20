using System;
using System.Threading.Tasks;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Excalibur.Hosting.AzureFunctions;

namespace examples.Excalibur.Dispatch.Examples.Serverless.Azure.Sagas;

/// <summary>
/// Activity functions for the order processing saga.
/// </summary>
public class OrderProcessingActivities {
 private readonly ILogger<OrderProcessingActivities> _logger;

 /// <summary>
 /// Initializes a new instance of the <see cref="OrderProcessingActivities"/> class.
 /// </summary>
 public OrderProcessingActivities(ILogger<OrderProcessingActivities> logger)
 {
 _logger = logger;
 }

 /// <summary>
 /// Validates an order.
 /// </summary>
 [Function("ValidateOrderActivity")]
 public async Task<ValidationResult> ValidateOrder(
 [ActivityTrigger] OrderRequest order,
 FunctionContext context)
 {
 _logger.LogInformation("Validating order {OrderId}", order.OrderId);

 await Task.Delay(100); // Simulate validation

 var errors = new List<string>();

 // Validate business rules
 if (order.TotalAmount > 10000)
 {
 errors.Add("Order amount exceeds maximum limit");
 }

 foreach (var item in order.Items)
 {
 if (item.Quantity > 100)
 {
 errors.Add($"Quantity for {item.ProductName} exceeds maximum");
 }
 }

 return new ValidationResult
 {
 IsValid = errors.Count == 0,
 Errors = errors
 };
 }

 /// <summary>
 /// Checks inventory availability.
 /// </summary>
 [Function("CheckInventoryActivity")]
 public async Task<InventoryResult> CheckInventory(
 [ActivityTrigger] InventoryRequest request,
 FunctionContext context)
 {
 _logger.LogInformation("Checking inventory for order {OrderId}", request.OrderId);

 await Task.Delay(200); // Simulate inventory check

 // Simulate inventory check - in real world, this would call inventory service
 var reservedItems = new Dictionary<string, int>();
 foreach (var item in request.Items)
 {
 reservedItems[item.ProductId] = item.Quantity;
 }

 return new InventoryResult
 {
 IsAvailable = true,
 ReservedItems = reservedItems
 };
 }

 /// <summary>
 /// Releases inventory reservation.
 /// </summary>
 [Function("ReleaseInventoryActivity")]
 public async Task ReleaseInventory(
 [ActivityTrigger] CompensationInput input,
 FunctionContext context)
 {
 var request = input.OriginalInput as InventoryRequest;
 var result = input.OriginalOutput as InventoryResult;

 _logger.LogInformation("Releasing inventory for order {OrderId}", request?.OrderId);

 await Task.Delay(100); // Simulate release

 // In real world, this would call inventory service to release reservation
 foreach (var item in result?.ReservedItems ?? new Dictionary<string, int>())
 {
 _logger.LogInformation("Released {Quantity} units of {ProductId}",
 item.Value, item.Key);
 }
 }

 /// <summary>
 /// Processes payment.
 /// </summary>
 [Function("ProcessPaymentActivity")]
 public async Task<PaymentResult> ProcessPayment(
 [ActivityTrigger] PaymentRequest request,
 FunctionContext context)
 {
 _logger.LogInformation("Processing payment of ${Amount} for order {OrderId}",
 request.Amount, request.OrderId);

 await Task.Delay(500); // Simulate payment processing

 // Simulate payment processing - in real world, this would call payment gateway
 return new PaymentResult
 {
 IsSuccessful = true,
 TransactionId = $"TXN-{Guid.NewGuid():N}",
 ProcessedAt = DateTime.UtcNow
 };
 }

 /// <summary>
 /// Refunds payment.
 /// </summary>
 [Function("RefundPaymentActivity")]
 public async Task RefundPayment(
 [ActivityTrigger] CompensationInput input,
 FunctionContext context)
 {
 var request = input.OriginalInput as PaymentRequest;
 var result = input.OriginalOutput as PaymentResult;

 _logger.LogInformation("Refunding payment {TransactionId} for order {OrderId}",
 result?.TransactionId, request?.OrderId);

 await Task.Delay(300); // Simulate refund

 // In real world, this would call payment gateway to process refund
 _logger.LogInformation("Refund processed successfully");
 }

 /// <summary>
 /// Creates shipment.
 /// </summary>
 [Function("CreateShipmentActivity")]
 public async Task<ShipmentResult> CreateShipment(
 [ActivityTrigger] ShipmentRequest request,
 FunctionContext context)
 {
 _logger.LogInformation("Creating shipment for order {OrderId}", request.OrderId);

 await Task.Delay(300); // Simulate shipment creation

 // Simulate shipment creation - in real world, this would call shipping service
 var deliveryDays = request.ShippingMethod switch
 {
 "Express" => 2,
 "Standard" => 5,
 "Economy" => 10,
 _ => 7
 };

 return new ShipmentResult
 {
 TrackingNumber = $"TRACK-{Guid.NewGuid():N}".Substring(0, 12).ToUpper(),
 EstimatedDeliveryDate = DateTime.UtcNow.AddDays(deliveryDays),
 Carrier = "Example Shipping Co."
 };
 }

 /// <summary>
 /// Cancels shipment.
 /// </summary>
 [Function("CancelShipmentActivity")]
 public async Task CancelShipment(
 [ActivityTrigger] CompensationInput input,
 FunctionContext context)
 {
 var request = input.OriginalInput as ShipmentRequest;
 var result = input.OriginalOutput as ShipmentResult;

 _logger.LogInformation("Cancelling shipment {TrackingNumber} for order {OrderId}",
 result?.TrackingNumber, request?.OrderId);

 await Task.Delay(200); // Simulate cancellation

 // In real world, this would call shipping service to cancel shipment
 _logger.LogInformation("Shipment cancelled successfully");
 }

 /// <summary>
 /// Sends email.
 /// </summary>
 [Function("SendEmailActivity")]
 public async Task<EmailResult> SendEmail(
 [ActivityTrigger] EmailRequest request,
 FunctionContext context)
 {
 _logger.LogInformation("Sending email to {To} with subject: {Subject}",
 request.To, request.Subject);

 await Task.Delay(100); // Simulate email sending

 // Simulate email sending - in real world, this would use email service
 return new EmailResult
 {
 IsSent = true,
 MessageId = $"MSG-{Guid.NewGuid():N}"
 };
 }
}