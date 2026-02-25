using System;
using System.Text;
using System.Threading.Tasks;
using Azure.Messaging.ServiceBus;
using Azure.Storage.Blobs;
using Excalibur.Dispatch.CloudNativePatterns.ClaimCheck;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace examples.ClaimCheck
 /// <summary>
 /// Example demonstrating migration from Base64-encoded messages to Claim Check pattern.
 /// </summary>
 public class MigrationFromBase64Example {
 public static async Task Main(string[] args)
 {
 var host = Host.CreateDefaultBuilder(args)
 .ConfigureServices((context, services) =>
 {
 // Configure services
 services.AddSingleton<BlobServiceClient>(sp =>
 new BlobServiceClient(context.Configuration["AzureStorage:ConnectionString"]));
 
 services.AddSingleton<ServiceBusClient>(sp =>
 new ServiceBusClient(context.Configuration["ServiceBus:ConnectionString"]));

 // Configure Claim Check
 services.AddClaimCheck()
 .WithBlobStorage("migrated-claims")
 .WithCompressionThreshold(512) // Compress anything over 512 bytes
 .WithBackwardCompatibility() // Support both old and new message formats
 .WithMigrationMode(); // Enable migration helpers

 // Add migration service
 services.AddHostedService<MessageMigrationService>();
 })
 .Build();

 await host.RunAsync();
 }
 }

 /// <summary>
 /// Service that demonstrates migrating from Base64 to Claim Check pattern.
 /// </summary>
 public class MessageMigrationService : BackgroundService
 {
 private readonly IClaimCheckService _claimCheck;
 private readonly ServiceBusClient _serviceBusClient;
 private readonly ILogger<MessageMigrationService> _logger;
 private readonly IConfiguration _configuration;

 /// <summary>
 /// Initializes a new instance of the <see cref="MessageMigrationService"/> class.
 /// </summary>
 public MessageMigrationService(
 IClaimCheckService claimCheck,
 ServiceBusClient serviceBusClient,
 ILogger<MessageMigrationService> logger,
 IConfiguration configuration)
 {
 _claimCheck = claimCheck;
 _serviceBusClient = serviceBusClient;
 _logger = logger;
 _configuration = configuration;
 }

 /// <summary>
 /// Executes the migration examples.
 /// </summary>
 protected override async Task ExecuteAsync(CancellationToken stoppingToken)
 {
 // Example 1: Show the old way (Base64)
 await DemonstrateOldBase64Approach();

 // Example 2: Show the new way (Claim Check)
 await DemonstrateNewClaimCheckApproach(stoppingToken);

 // Example 3: Show hybrid approach for gradual migration
 await DemonstrateHybridApproach(stoppingToken);

 // Example 4: Process messages that could be either format
 await ProcessMixedFormatMessages(stoppingToken);
 }

 /// <summary>
 /// Demonstrates the old Base64 encoding approach and its limitations.
 /// </summary>
 private async Task DemonstrateOldBase64Approach()
 {
 _logger.LogInformation("=== OLD APPROACH: Base64 Encoding ===");

 // Create a sample report
 var report = new BusinessReport
 {
 Id = Guid.NewGuid(),
 Title = "Quarterly Sales Report",
 GeneratedAt = DateTime.UtcNow,
 Content = GenerateLargeReportContent(1000) // 1000 lines
 };

 // Old way: Serialize and Base64 encode
 var json = System.Text.Json.JsonSerializer.Serialize(report);
 var bytes = Encoding.UTF8.GetBytes(json);
 var base64 = Convert.ToBase64String(bytes);

 // Create Service Bus message
 var oldMessage = new ServiceBusMessage
 {
 Body = BinaryData.FromString(base64),
 ContentType = "application/base64",
 Subject = "BusinessReport"
 };

 _logger.LogWarning(
 "OLD: Message size: {Size:N0} bytes (Base64: {Base64Size:N0} bytes) - {Increase:P} increase!",
 bytes.Length,
 base64.Length,
 (double)base64.Length / bytes.Length - 1);

 // This would fail if message > 256KB (Service Bus limit)
 if (base64.Length > 256 * 1024)
 {
 _logger.LogError("OLD: Message too large for Service Bus! Would fail to send.");
 }
 }

 /// <summary>
 /// Demonstrates the new Claim Check approach.
 /// </summary>
 private async Task DemonstrateNewClaimCheckApproach(CancellationToken cancellationToken)
 {
 _logger.LogInformation("\n=== NEW APPROACH: Claim Check Pattern ===");

 // Create the same report
 var report = new BusinessReport
 {
 Id = Guid.NewGuid(),
 Title = "Quarterly Sales Report",
 GeneratedAt = DateTime.UtcNow,
 Content = GenerateLargeReportContent(1000)
 };

 // New way: Store as claim
 var claimResult = await _claimCheck.StoreAsync(
 report,
 new ClaimMetadata
 {
 MessageType = "BusinessReport",
 CorrelationId = report.Id.ToString(),
 Source = "ReportingService"
 },
 cancellationToken);

 if (claimResult.Success)
 {
 // Create lightweight message with just claim reference
 var newMessage = new ServiceBusMessage
 {
 Body = BinaryData.FromObjectAsJson(new ClaimCheckEnvelope
 {
 ClaimId = claimResult.ClaimId,
 MessageType = "BusinessReport",
 OriginalSize = claimResult.OriginalSize,
 StoredSize = claimResult.StoredSize,
 CompressionUsed = claimResult.WasCompressed
 }),
 ContentType = "application/json",
 Subject = "BusinessReport-ClaimCheck"
 };

 _logger.LogInformation(
 "NEW: Message size: {MessageSize:N0} bytes, Claim size: {ClaimSize:N0} bytes - {Reduction:P} reduction!",
 newMessage.Body.ToArray().Length,
 claimResult.StoredSize,
 1 - (double)newMessage.Body.ToArray().Length / claimResult.OriginalSize);
 }
 }

 /// <summary>
 /// Demonstrates a hybrid approach for gradual migration.
 /// </summary>
 private async Task DemonstrateHybridApproach(CancellationToken cancellationToken)
 {
 _logger.LogInformation("\n=== HYBRID APPROACH: Gradual Migration ===");

 var report = new BusinessReport
 {
 Id = Guid.NewGuid(),
 Title = "Monthly Analytics",
 GeneratedAt = DateTime.UtcNow,
 Content = GenerateLargeReportContent(500)
 };

 // Hybrid: Use size threshold to decide approach
 var json = System.Text.Json.JsonSerializer.Serialize(report);
 var sizeThreshold = _configuration.GetValue<int>("Migration:SizeThreshold", 50 * 1024); // 50KB default

 ServiceBusMessage message;

 if (json.Length > sizeThreshold)
 {
 _logger.LogInformation("HYBRID: Message > {Threshold:N0} bytes, using Claim Check", sizeThreshold);

 // Use Claim Check for large messages
 var claimResult = await _claimCheck.StoreAsync(report, new ClaimMetadata
 {
 MessageType = "BusinessReport",
 CorrelationId = report.Id.ToString()
 }, cancellationToken);

 message = new ServiceBusMessage
 {
 Body = BinaryData.FromObjectAsJson(new HybridMessage
 {
 UseClaimCheck = true,
 ClaimId = claimResult.ClaimId,
 Data = null // No inline data
 }),
 ContentType = "application/json",
 Subject = "BusinessReport-Hybrid"
 };
 }
 else
 {
 _logger.LogInformation("HYBRID: Message < {Threshold:N0} bytes, using inline Base64", sizeThreshold);

 // Use Base64 for small messages
 var base64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(json));
 message = new ServiceBusMessage
 {
 Body = BinaryData.FromObjectAsJson(new HybridMessage
 {
 UseClaimCheck = false,
 ClaimId = null,
 Data = base64 // Inline data
 }),
 ContentType = "application/json",
 Subject = "BusinessReport-Hybrid"
 };
 }

 _logger.LogInformation("HYBRID: Final message size: {Size:N0} bytes", 
 message.Body.ToArray().Length);
 }

 /// <summary>
 /// Demonstrates processing messages in mixed formats.
 /// </summary>
 private async Task ProcessMixedFormatMessages(CancellationToken cancellationToken)
 {
 _logger.LogInformation("\n=== PROCESSING MIXED FORMAT MESSAGES ===");

 var processor = _serviceBusClient.CreateProcessor("migration-queue");
 
 processor.ProcessMessageAsync += async args =>
 {
 try
 {
 BusinessReport report = null;

 // Check content type to determine format
 switch (args.Message.ContentType)
 {
 case "application/base64":
 // Old format: Base64 encoded
 _logger.LogInformation("Processing legacy Base64 message");
 var base64 = args.Message.Body.ToString();
 var bytes = Convert.FromBase64String(base64);
 var json = Encoding.UTF8.GetString(bytes);
 report = System.Text.Json.JsonSerializer.Deserialize<BusinessReport>(json);
 break;

 case "application/json" when args.Message.Subject?.Contains("ClaimCheck") == true:
 // New format: Claim Check
 _logger.LogInformation("Processing Claim Check message");
 var envelope = args.Message.Body.ToObjectFromJson<ClaimCheckEnvelope>();
 var result = await _claimCheck.RetrieveAsync<BusinessReport>(
 envelope.ClaimId, 
 cancellationToken);
 if (result.Success)
 {
 report = result.Payload;
 }
 break;

 case "application/json" when args.Message.Subject?.Contains("Hybrid") == true:
 // Hybrid format
 _logger.LogInformation("Processing hybrid message");
 var hybrid = args.Message.Body.ToObjectFromJson<HybridMessage>();
 if (hybrid.UseClaimCheck)
 {
 var result = await _claimCheck.RetrieveAsync<BusinessReport>(
 hybrid.ClaimId, 
 cancellationToken);
 report = result.Success ? result.Payload : null;
 }
 else
 {
 var bytes2 = Convert.FromBase64String(hybrid.Data);
 var json2 = Encoding.UTF8.GetString(bytes2);
 report = System.Text.Json.JsonSerializer.Deserialize<BusinessReport>(json2);
 }
 break;

 default:
 _logger.LogWarning("Unknown message format: {ContentType}", args.Message.ContentType);
 break;
 }

 if (report != null)
 {
 await ProcessReport(report);
 await args.CompleteMessageAsync(args.Message);
 }
 else
 {
 await args.DeadLetterMessageAsync(args.Message, "InvalidFormat");
 }
 }
 catch (Exception ex)
 {
 _logger.LogError(ex, "Error processing message");
 await args.DeadLetterMessageAsync(args.Message, "ProcessingError", ex.Message);
 }
 };

 processor.ProcessErrorAsync += args =>
 {
 _logger.LogError(args.Exception, "Service Bus processing error");
 return Task.CompletedTask;
 };

 // Process for a short time in this example
 await processor.StartProcessingAsync(cancellationToken);
 await Task.Delay(TimeSpan.FromSeconds(30), cancellationToken);
 await processor.StopProcessingAsync(cancellationToken);
 }

 /// <summary>
 /// Processes a business report.
 /// </summary>
 private async Task ProcessReport(BusinessReport report)
 {
 _logger.LogInformation("Processing report: {Title} (ID: {Id})",
 report.Title, report.Id);
 
 // Simulate processing
 await Task.Delay(100);
 
 _logger.LogInformation("Report processed successfully");
 }

 /// <summary>
 /// Generates sample report content.
 /// </summary>
 private string GenerateLargeReportContent(int lines)
 {
 var sb = new StringBuilder();
 for (int i = 0; i < lines; i++)
 {
 sb.AppendLine($"Line {i + 1}: Sales data for region {i % 10}, " +
 $"revenue: ${Random.Shared.Next(10000, 100000):N2}, " +
 $"units: {Random.Shared.Next(100, 1000)}, " +
 $"growth: {Random.Shared.Next(-20, 50)}%");
 }
 return sb.ToString();
 }
 }

 /// <summary>
 /// Example business report model.
 /// </summary>
 public class BusinessReport {
 public Guid Id { get; set; }
 public string Title { get; set; }
 public DateTime GeneratedAt { get; set; }
 public string Content { get; set; }
 }

 /// <summary>
 /// Claim Check envelope for new message format.
 /// </summary>
 public class ClaimCheckEnvelope {
 public string ClaimId { get; set; }
 public string MessageType { get; set; }
 public long OriginalSize { get; set; }
 public long StoredSize { get; set; }
 public bool CompressionUsed { get; set; }
 }

 /// <summary>
 /// Hybrid message format supporting both inline and claim check.
 /// </summary>
 public class HybridMessage {
 public bool UseClaimCheck { get; set; }
 public string ClaimId { get; set; }
 public string Data { get; set; } // Base64 data if not using claim check
 }
}