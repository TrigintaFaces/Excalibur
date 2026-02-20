using System;
using System.Threading.Tasks;
using Azure.Storage.Blobs;
using Excalibur.Dispatch.CloudNativePatterns.ClaimCheck;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace examples.ClaimCheck
 /// <summary>
 /// Basic example demonstrating the Claim Check pattern for handling large messages.
 /// </summary>
 public class BasicClaimCheckExample {
 public static async Task Main(string[] args)
 {
 // Create host with configured services
 var host = Host.CreateDefaultBuilder(args)
 .ConfigureServices((context, services) =>
 {
 // Configure Azure Blob Storage for claim storage
 services.AddSingleton<BlobServiceClient>(sp =>
 new BlobServiceClient(context.Configuration["AzureStorage:ConnectionString"]));

 // Add Claim Check services with Blob Storage
 services.AddClaimCheck(options =>
 {
 options.ContainerName = "claim-check-container";
 options.CompressionThreshold = 1024; // Compress payloads > 1KB
 options.DefaultTtl = TimeSpan.FromHours(24); // Claims expire after 24 hours
 });

 // Add your message handling services
 services.AddHostedService<MessageProcessor>();
 })
 .Build();

 await host.RunAsync();
 }
 }

 /// <summary>
 /// Example message processor that uses claim check for large payloads.
 /// </summary>
 public class MessageProcessor : BackgroundService
 {
 private readonly IClaimCheckProvider _claimCheck;
 private readonly ILogger<MessageProcessor> _logger;

 /// <summary>
 /// Initializes a new instance of the <see cref="MessageProcessor"/> class.
 /// </summary>
 public MessageProcessor(IClaimCheckProvider claimCheck, ILogger<MessageProcessor> logger)
 {
 _claimCheck = claimCheck;
 _logger = logger;
 }

 /// <summary>
 /// Processes messages with claim check pattern.
 /// </summary>
 protected override async Task ExecuteAsync(CancellationToken stoppingToken)
 {
 // Example: Process a large order message
 var largeOrder = new OrderMessage
 {
 OrderId = Guid.NewGuid().ToString(),
 CustomerId = "CUST-12345",
 Items = GenerateLargeItemList(1000), // Large payload
 OrderDate = DateTime.UtcNow
 };

 // Serialize the order to bytes
 var orderBytes = System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(largeOrder);
 
 // Store the large payload and get a claim reference
 var claimReference = await _claimCheck.StoreAsync(
 orderBytes,
 new ClaimCheckMetadata
 {
 MessageType = "OrderMessage",
 CorrelationId = largeOrder.OrderId
 },
 stoppingToken);

 _logger.LogInformation(
 "Stored large order {OrderId} as claim {ClaimId}. Size: {Size:N0} bytes",
 largeOrder.OrderId,
 claimReference.Id,
 orderBytes.Length);

 // Send only the claim reference through the message bus
 await SendClaimToMessageBus(claimReference.Id);

 // Simulate receiving the claim on the other side
 await ProcessReceivedClaim(claimReference.Id, stoppingToken);
 }

 /// <summary>
 /// Simulates sending claim ID through message bus.
 /// </summary>
 private async Task SendClaimToMessageBus(string claimId)
 {
 // In real implementation, this would send to Service Bus, Event Hub, etc.
 _logger.LogInformation("Sending claim ID {ClaimId} through message bus", claimId);
 await Task.Delay(100); // Simulate network call
 }

 /// <summary>
 /// Processes a received claim by retrieving the original payload.
 /// </summary>
 private async Task ProcessReceivedClaim(string claimId, CancellationToken cancellationToken)
 {
 try
 {
 // Create a claim reference for retrieval
 var claimReference = new ClaimCheckReference
 {
 ClaimId = claimId
 };
 
 // Retrieve the original payload bytes
 var payloadBytes = await _claimCheck.RetrieveAsync(claimReference, cancellationToken);
 
 // Deserialize back to the order object
 var order = System.Text.Json.JsonSerializer.Deserialize<OrderMessage>(payloadBytes);
 
 _logger.LogInformation(
 "Retrieved order {OrderId} with {ItemCount} items",
 order.OrderId,
 order.Items.Count);

 // Process the order
 await ProcessOrder(order);

 // Clean up the claim after processing
 await _claimCheck.DeleteAsync(claimReference, cancellationToken);
 }
 catch (Exception ex)
 {
 _logger.LogError(ex, "Failed to retrieve or process claim {ClaimId}", claimId);
 }
 }

 /// <summary>
 /// Processes the retrieved order.
 /// </summary>
 private async Task ProcessOrder(OrderMessage order)
 {
 _logger.LogInformation("Processing order {OrderId} with {ItemCount} items",
 order.OrderId, order.Items.Count);
 
 // Simulate order processing
 await Task.Delay(500);
 
 _logger.LogInformation("Order {OrderId} processed successfully", order.OrderId);
 }

 /// <summary>
 /// Generates a large list of items for testing.
 /// </summary>
 private List<OrderItem> GenerateLargeItemList(int count)
 {
 var items = new List<OrderItem>(count);
 for (int i = 0; i < count; i++)
 {
 items.Add(new OrderItem
 {
 ProductId = $"PROD-{i:D5}",
 ProductName = $"Product {i}",
 Quantity = Random.Shared.Next(1, 10),
 UnitPrice = Random.Shared.Next(10, 1000) / 10.0m,
 Description = $"This is a detailed description for product {i} with various attributes and specifications that make the payload larger."
 });
 }
 return items;
 }
 }

 /// <summary>
 /// Example order message with potentially large payload.
 /// </summary>
 public class OrderMessage {
 public string OrderId { get; set; }
 public string CustomerId { get; set; }
 public List<OrderItem> Items { get; set; }
 public DateTime OrderDate { get; set; }
 }

 /// <summary>
 /// Order item details.
 /// </summary>
 public class OrderItem {
 public string ProductId { get; set; }
 public string ProductName { get; set; }
 public int Quantity { get; set; }
 public decimal UnitPrice { get; set; }
 public string Description { get; set; }
 }
}