using CloudNative.CloudEvents;
using Excalibur.Dispatch.CloudNative.Serverless.Google.Framework;
using Microsoft.Extensions.Logging;

namespace examples.Excalibur.Dispatch.Examples.Serverless.Google;

/// <summary>
/// Example HTTP-triggered Google Cloud Function.
/// </summary>
public class HelloWorldFunction : GoogleCloudFunctionBase
{
 /// <summary>
 /// Initializes a new instance of the <see cref="HelloWorldFunction"/> class.
 /// </summary>
 public HelloWorldFunction(IServiceProvider serviceProvider)
 : base(serviceProvider)
 {
 }

 /// <summary>
 /// Handles HTTP requests.
 /// </summary>
 public override async Task<HttpResponseData> HandleHttpAsync(
 HttpRequestData request,
 GoogleCloudFunctionContext context,
 CancellationToken cancellationToken = default)
 {
 Logger.LogInformation("Processing HTTP request: {Method} {Path}",
 request.Method, request.Path);

 // Extract name from query string or body
 var name = "World";
 if (!string.IsNullOrEmpty(request.QueryString))
 {
 var query = System.Web.HttpUtility.ParseQueryString(request.QueryString);
 name = query["name"] ?? name;
 }

 var response = new HttpResponseData
 {
 StatusCode = 200,
 Headers = { ["Content-Type"] = new[] { "application/json" } }
 };

 var responseBody = System.Text.Json.JsonSerializer.Serialize(new
 {
 message = $"Hello, {name}!",
 functionName = context.FunctionName,
 executionId = context.RequestId,
 region = context.Region,
 coldStart = context.IsColdStart
 });

 response.Body = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(responseBody));

 LogExecutionMetrics(context);
 return response;
 }
}

/// <summary>
/// Example Pub/Sub triggered Google Cloud Function.
/// </summary>
public class PubSubMessageFunction : GoogleCloudFunctionBase<PubSubMessage>
{
 private readonly IMessageProcessor _messageProcessor;

 /// <summary>
 /// Initializes a new instance of the <see cref="PubSubMessageFunction"/> class.
 /// </summary>
 public PubSubMessageFunction(IServiceProvider serviceProvider, IMessageProcessor messageProcessor)
 : base(serviceProvider)
 {
 _messageProcessor = messageProcessor ?? throw new ArgumentNullException(nameof(messageProcessor));
 }

 /// <summary>
 /// Handles Pub/Sub messages.
 /// </summary>
 public override async Task HandleAsync(
 PubSubMessage data,
 CloudEvent cloudEvent,
 GoogleCloudFunctionContext context,
 CancellationToken cancellationToken = default)
 {
 Logger.LogInformation(
 "Processing Pub/Sub message: {MessageId} from {Source}",
 cloudEvent.Id,
 cloudEvent.Source);

 try
 {
 // Decode message data
 var messageData = Convert.FromBase64String(data.Data);
 var messageText = System.Text.Encoding.UTF8.GetString(messageData);

 // Process the message
 await _messageProcessor.ProcessAsync(messageText, cancellationToken);

 // Log attributes
 if (data.Attributes != null)
 {
 foreach (var attr in data.Attributes)
 {
 Logger.LogDebug("Message attribute: {Key} = {Value}", attr.Key, attr.Value);
 }
 }

 Logger.LogInformation(
 "Successfully processed message {MessageId} published at {PublishTime}",
 data.MessageId,
 data.PublishTime);
 }
 catch (Exception ex)
 {
 Logger.LogError(ex, "Error processing Pub/Sub message {MessageId}", data.MessageId);
 throw;
 }
 finally
 {
 LogExecutionMetrics(context);
 }
 }
}

/// <summary>
/// Example Cloud Storage triggered function.
/// </summary>
public class StorageEventFunction : GoogleCloudFunctionBase<StorageObject>
{
 /// <summary>
 /// Initializes a new instance of the <see cref="StorageEventFunction"/> class.
 /// </summary>
 public StorageEventFunction(IServiceProvider serviceProvider)
 : base(serviceProvider)
 {
 }

 /// <summary>
 /// Handles Cloud Storage events.
 /// </summary>
 public override async Task HandleAsync(
 StorageObject data,
 CloudEvent cloudEvent,
 GoogleCloudFunctionContext context,
 CancellationToken cancellationToken = default)
 {
 Logger.LogInformation(
 "Processing storage event: {EventType} for object {ObjectName} in bucket {BucketName}",
 cloudEvent.Type,
 data.Name,
 data.Bucket);

 // Process based on event type
 switch (cloudEvent.Type)
 {
 case "google.cloud.storage.object.v1.finalized":
 await ProcessObjectFinalized(data, cancellationToken);
 break;

 case "google.cloud.storage.object.v1.deleted":
 await ProcessObjectDeleted(data, cancellationToken);
 break;

 case "google.cloud.storage.object.v1.metadataUpdated":
 await ProcessObjectMetadataUpdated(data, cancellationToken);
 break;

 default:
 Logger.LogWarning("Unhandled storage event type: {EventType}", cloudEvent.Type);
 break;
 }

 LogExecutionMetrics(context);
 }

 private Task ProcessObjectFinalized(StorageObject data, CancellationToken cancellationToken)
 {
 Logger.LogInformation(
 "New object created: {ObjectName}, Size: {Size} bytes, ContentType: {ContentType}",
 data.Name,
 data.Size,
 data.ContentType);

 // Process the new object (e.g., trigger image processing, data pipeline, etc.)
 return Task.CompletedTask;
 }

 private Task ProcessObjectDeleted(StorageObject data, CancellationToken cancellationToken)
 {
 Logger.LogInformation("Object deleted: {ObjectName}", data.Name);

 // Handle object deletion (e.g., cleanup related data)
 return Task.CompletedTask;
 }

 private Task ProcessObjectMetadataUpdated(StorageObject data, CancellationToken cancellationToken)
 {
 Logger.LogInformation(
 "Object metadata updated: {ObjectName}, Updated: {UpdateTime}",
 data.Name,
 data.Updated);

 // Handle metadata changes
 return Task.CompletedTask;
 }
}

// Data models for Google Cloud events

/// <summary>
/// Pub/Sub message data.
/// </summary>
public class PubSubMessage {
 /// <summary>
 /// Gets or sets the message ID.
 /// </summary>
 public string MessageId { get; set; } = string.Empty;

 /// <summary>
 /// Gets or sets the base64-encoded message data.
 /// </summary>
 public string Data { get; set; } = string.Empty;

 /// <summary>
 /// Gets or sets the message attributes.
 /// </summary>
 public Dictionary<string, string>? Attributes { get; set; }

 /// <summary>
 /// Gets or sets the publish time.
 /// </summary>
 public DateTime PublishTime { get; set; }
}

/// <summary>
/// Cloud Storage object data.
/// </summary>
public class StorageObject {
 /// <summary>
 /// Gets or sets the object name.
 /// </summary>
 public string Name { get; set; } = string.Empty;

 /// <summary>
 /// Gets or sets the bucket name.
 /// </summary>
 public string Bucket { get; set; } = string.Empty;

 /// <summary>
 /// Gets or sets the object generation.
 /// </summary>
 public string Generation { get; set; } = string.Empty;

 /// <summary>
 /// Gets or sets the object size in bytes.
 /// </summary>
 public long Size { get; set; }

 /// <summary>
 /// Gets or sets the content type.
 /// </summary>
 public string ContentType { get; set; } = string.Empty;

 /// <summary>
 /// Gets or sets the creation time.
 /// </summary>
 public DateTime TimeCreated { get; set; }

 /// <summary>
 /// Gets or sets the update time.
 /// </summary>
 public DateTime Updated { get; set; }

 /// <summary>
 /// Gets or sets the storage class.
 /// </summary>
 public string StorageClass { get; set; } = string.Empty;

 /// <summary>
 /// Gets or sets the MD5 hash.
 /// </summary>
 public string Md5Hash { get; set; } = string.Empty;

 /// <summary>
 /// Gets or sets the media link.
 /// </summary>
 public string MediaLink { get; set; } = string.Empty;

 /// <summary>
 /// Gets or sets the object metadata.
 /// </summary>
 public Dictionary<string, string>? Metadata { get; set; }
}

/// <summary>
/// Message processor interface for dependency injection.
/// </summary>
public interface IMessageProcessor {
 /// <summary>
 /// Processes a message.
 /// </summary>
 Task ProcessAsync(string message, CancellationToken cancellationToken = default);
}

/// <summary>
/// Example message processor implementation.
/// </summary>
public class MessageProcessor : IMessageProcessor
{
 private readonly ILogger<MessageProcessor> _logger;

 /// <summary>
 /// Initializes a new instance of the <see cref="MessageProcessor"/> class.
 /// </summary>
 public MessageProcessor(ILogger<MessageProcessor> logger)
 {
 _logger = logger ?? throw new ArgumentNullException(nameof(logger));
 }

 /// <summary>
 /// Processes a message.
 /// </summary>
 public Task ProcessAsync(string message, CancellationToken cancellationToken = default)
 {
 _logger.LogInformation("Processing message: {Message}", message);
 // Add actual message processing logic here
 return Task.CompletedTask;
 }
}
