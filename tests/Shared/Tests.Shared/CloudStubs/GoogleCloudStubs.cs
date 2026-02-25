// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Text.Json;

using Tests.Shared.TestTypes;

namespace Tests.Shared.CloudStubs.Google;

/// <summary>Google Cloud Function context stub.</summary>
public class CloudFunctionContext
{
	/// <summary>Gets or sets the function name.</summary>
	public string FunctionName { get; set; } = string.Empty;

	/// <summary>Gets or sets the project ID.</summary>
	public string ProjectId { get; set; } = string.Empty;

	/// <summary>Gets or sets the region.</summary>
	public string Region { get; set; } = string.Empty;

	/// <summary>Gets or sets the event ID.</summary>
	public string EventId { get; set; } = Guid.NewGuid().ToString();

	/// <summary>Gets or sets the event type.</summary>
	public string EventType { get; set; } = string.Empty;

	/// <summary>Gets or sets the timestamp.</summary>
	public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;
}

/// <summary>Google Cloud Function interface stub.</summary>
public interface IGoogleCloudFunction
{
	/// <summary>Handles the cloud function request.</summary>
	Task HandleAsync(CloudFunctionContext context, CancellationToken cancellationToken = default);
}

/// <summary>Google Cloud Pub/Sub message stub.</summary>
public class PubSubMessage
{
	/// <summary>Gets or sets the message data (base64 encoded).</summary>
	public string? Data { get; set; }

	/// <summary>Gets or sets the message ID.</summary>
	public string? MessageId { get; set; }

	/// <summary>Gets or sets the publish time.</summary>
	public DateTimeOffset PublishTime { get; set; } = DateTimeOffset.UtcNow;

	/// <summary>Gets or sets the ordering key.</summary>
	public string? OrderingKey { get; set; }

	/// <summary>Gets or sets the attributes.</summary>
	public Dictionary<string, string>? Attributes { get; set; }

	/// <summary>Gets the decoded data as a string.</summary>
	public string GetDecodedData() => Data is not null ? System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(Data)) : string.Empty;
}

/// <summary>Google Cloud Pub/Sub subscription stub.</summary>
public class PubSubSubscription
{
	/// <summary>Gets or sets the subscription name.</summary>
	public string Name { get; set; } = string.Empty;

	/// <summary>Gets or sets the topic.</summary>
	public string Topic { get; set; } = string.Empty;

	/// <summary>Gets or sets the ack deadline in seconds.</summary>
	public int AckDeadlineSeconds { get; set; } = 10;
}

/// <summary>Firestore event stub.</summary>
public class FirestoreEvent
{
	/// <summary>Gets or sets the document path.</summary>
	public string DocumentPath { get; set; } = string.Empty;

	/// <summary>Gets or sets the old value.</summary>
	public JsonDocument? OldValue { get; set; }

	/// <summary>Gets or sets the new value.</summary>
	public JsonDocument? Value { get; set; }

	/// <summary>Gets or sets the update mask (fields that changed).</summary>
	public IReadOnlyList<string> UpdateMask { get; set; } = [];

	/// <summary>Gets or sets the event type.</summary>
	public FirestoreEventType EventType { get; set; }
}

/// <summary>Firestore event type enum.</summary>
public enum FirestoreEventType
{
	/// <summary>Document created.</summary>
	Create,

	/// <summary>Document updated.</summary>
	Update,

	/// <summary>Document deleted.</summary>
	Delete,

	/// <summary>Document written (create or update).</summary>
	Write
}

/// <summary>Cloud Storage event stub.</summary>
public class CloudStorageEvent
{
	/// <summary>Gets or sets the bucket name.</summary>
	public string Bucket { get; set; } = string.Empty;

	/// <summary>Gets or sets the object name.</summary>
	public string Name { get; set; } = string.Empty;

	/// <summary>Gets or sets the content type.</summary>
	public string? ContentType { get; set; }

	/// <summary>Gets or sets the size in bytes.</summary>
	public long Size { get; set; }

	/// <summary>Gets or sets the MD5 hash.</summary>
	public string? Md5Hash { get; set; }

	/// <summary>Gets or sets the creation time.</summary>
	public DateTimeOffset TimeCreated { get; set; } = DateTimeOffset.UtcNow;

	/// <summary>Gets or sets the event type.</summary>
	public CloudStorageEventType EventType { get; set; }
}

/// <summary>Cloud Storage event type enum.</summary>
public enum CloudStorageEventType
{
	/// <summary>Object finalized (created or overwritten).</summary>
	Finalize,

	/// <summary>Object deleted.</summary>
	Delete,

	/// <summary>Object archived.</summary>
	Archive,

	/// <summary>Object metadata updated.</summary>
	MetadataUpdate
}

/// <summary>Google Cloud Run autoscaler stub.</summary>
public class CloudRunAutoscaler
{
	/// <summary>Gets or sets the minimum instances.</summary>
	public int MinInstances { get; set; }

	/// <summary>Gets or sets the maximum instances.</summary>
	public int MaxInstances { get; set; } = 100;

	/// <summary>Gets or sets the concurrency per instance.</summary>
	public int Concurrency { get; set; } = 80;

	/// <summary>Gets or sets the CPU threshold for scaling.</summary>
	public double CpuThreshold { get; set; } = 0.6;
}

/// <summary>Google Pub/Sub publisher client stub.</summary>
public class PublisherClient : IAsyncDisposable
{
	/// <summary>Gets the topic name.</summary>
	public string TopicName { get; } = string.Empty;

	/// <summary>Publishes a message.</summary>
	public Task<string> PublishAsync(PubSubMessage message, CancellationToken cancellationToken = default)
		=> Task.FromResult(Guid.NewGuid().ToString());

	/// <summary>Publishes multiple messages.</summary>
	public Task<IReadOnlyList<string>> PublishAsync(IEnumerable<PubSubMessage> messages, CancellationToken cancellationToken = default)
		=> Task.FromResult<IReadOnlyList<string>>(messages.Select(_ => Guid.NewGuid().ToString()).ToList());

	/// <inheritdoc/>
	public ValueTask DisposeAsync()
	{
		GC.SuppressFinalize(this);
		return ValueTask.CompletedTask;
	}
}

/// <summary>Google Pub/Sub subscriber client stub.</summary>
public class SubscriberClient : IAsyncDisposable
{
	/// <summary>Gets the subscription name.</summary>
	public string SubscriptionName { get; } = string.Empty;

	/// <summary>Starts receiving messages.</summary>
	public Task StartAsync(Func<PubSubMessage, CancellationToken, Task<SubscribeResult>> handler, CancellationToken cancellationToken = default)
		=> Task.CompletedTask;

	/// <summary>Stops receiving messages.</summary>
	public Task StopAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

	/// <inheritdoc/>
	public ValueTask DisposeAsync()
	{
		GC.SuppressFinalize(this);
		return ValueTask.CompletedTask;
	}
}

/// <summary>Subscribe result enum.</summary>
public enum SubscribeResult
{
	/// <summary>Acknowledge the message.</summary>
	Ack,

	/// <summary>Negative acknowledge (will be redelivered).</summary>
	Nack
}

/// <summary>Google Cloud Firestore client stub.</summary>
public class FirestoreDb
{
	/// <summary>Creates a client for the specified project.</summary>
	public static FirestoreDb Create(string projectId) => new();

	/// <summary>Gets a collection reference.</summary>
	public CollectionReference Collection(string path) => new();

	/// <summary>Gets a document reference.</summary>
	public DocumentReference Document(string path) => new();
}

/// <summary>Firestore collection reference stub.</summary>
public class CollectionReference
{
	/// <summary>Gets a document reference.</summary>
	public DocumentReference Document(string documentId) => new();

	/// <summary>Adds a document.</summary>
	public Task<DocumentReference> AddAsync(object data, CancellationToken cancellationToken = default)
		=> Task.FromResult(new DocumentReference());

	/// <summary>Gets all documents.</summary>
	public Task<QuerySnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default)
		=> Task.FromResult(new QuerySnapshot());
}

/// <summary>Firestore document reference stub.</summary>
public class DocumentReference
{
	/// <summary>Gets the document ID.</summary>
	public string Id { get; } = Guid.NewGuid().ToString();

	/// <summary>Gets the document.</summary>
	public Task<DocumentSnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default)
		=> Task.FromResult(new DocumentSnapshot());

	/// <summary>Sets the document data.</summary>
	public Task SetAsync(object data, CancellationToken cancellationToken = default) => Task.CompletedTask;

	/// <summary>Updates the document data.</summary>
	public Task UpdateAsync(IDictionary<string, object> updates, CancellationToken cancellationToken = default) => Task.CompletedTask;

	/// <summary>Deletes the document.</summary>
	public Task DeleteAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
}

/// <summary>Firestore document snapshot stub.</summary>
public class DocumentSnapshot
{
	/// <summary>Gets whether the document exists.</summary>
	public bool Exists { get; }

	/// <summary>Gets the document ID.</summary>
	public string Id { get; } = Guid.NewGuid().ToString();

	/// <summary>Converts to a dictionary.</summary>
	public Dictionary<string, object> ToDictionary() => new();

	/// <summary>Converts to the specified type.</summary>
	public T? ConvertTo<T>() => default;
}

/// <summary>Firestore query snapshot stub.</summary>
public class QuerySnapshot
{
	/// <summary>Gets the documents.</summary>
	public IReadOnlyList<DocumentSnapshot> Documents { get; } = [];

	/// <summary>Gets the count.</summary>
	public int Count => Documents.Count;
}

/// <summary>Google Cloud Function context with extended properties for function execution.</summary>
public class GoogleCloudFunctionContext
{
	/// <summary>Gets or sets the function name.</summary>
	public string FunctionName { get; set; } = string.Empty;

	/// <summary>Gets or sets the project ID.</summary>
	public string ProjectId { get; set; } = string.Empty;

	/// <summary>Gets or sets the region.</summary>
	public string Region { get; set; } = string.Empty;

	/// <summary>Gets or sets the trace ID.</summary>
	public string TraceId { get; set; } = Guid.NewGuid().ToString();

	/// <summary>Gets or sets the event ID.</summary>
	public string EventId { get; set; } = Guid.NewGuid().ToString();

	/// <summary>Gets or sets the execution ID.</summary>
	public string ExecutionId { get; set; } = Guid.NewGuid().ToString();

	/// <summary>Gets or sets the timestamp.</summary>
	public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;

	/// <summary>Gets or sets the service account email.</summary>
	public string? ServiceAccountEmail { get; set; }

	/// <summary>Gets or sets whether cold start.</summary>
	public bool IsColdStart { get; set; }
}

/// <summary>HTTP response data for Google Cloud Functions.</summary>
public class HttpResponseData
{
	/// <summary>Gets or sets the status code.</summary>
	public int StatusCode { get; set; } = 200;

	/// <summary>Gets or sets the response body.</summary>
	public Stream? Body { get; set; }

	/// <summary>Gets the headers.</summary>
	public Dictionary<string, string[]> Headers { get; } = new();

	/// <summary>Writes a string to the body.</summary>
	public void WriteString(string content)
	{
		Body = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(content));
	}
}

/// <summary>HTTP request data for Google Cloud Functions.</summary>
public class HttpRequestData
{
	/// <summary>Gets or sets the HTTP method.</summary>
	public string Method { get; set; } = "GET";

	/// <summary>Gets or sets the request URI.</summary>
	public Uri? Url { get; set; }

	/// <summary>Gets the headers.</summary>
	public Dictionary<string, string[]> Headers { get; } = new();

	/// <summary>Gets or sets the request body.</summary>
	public Stream? Body { get; set; }

	/// <summary>Gets the query parameters.</summary>
	public Dictionary<string, string> Query { get; } = new();
}

/// <summary>Base class for Google Cloud Functions (non-generic).</summary>
public abstract class GoogleCloudFunctionBase
{
	/// <summary>Gets the service provider.</summary>
	protected IServiceProvider ServiceProvider { get; }

	/// <summary>Initializes a new instance.</summary>
	protected GoogleCloudFunctionBase(IServiceProvider serviceProvider)
	{
		ServiceProvider = serviceProvider;
	}

	/// <summary>Handles an HTTP request.</summary>
	public virtual Task<HttpResponseData> HandleHttpAsync(
		HttpRequestData request,
		GoogleCloudFunctionContext context,
		CancellationToken cancellationToken = default)
		=> Task.FromResult(new HttpResponseData { StatusCode = 200 });
}

/// <summary>Base class for Google Cloud Functions (generic, event-based).</summary>
/// <typeparam name="TEventData">The event data type.</typeparam>
public abstract class GoogleCloudFunctionBase<TEventData>
{
	/// <summary>Gets the service provider.</summary>
	protected IServiceProvider ServiceProvider { get; }

	/// <summary>Initializes a new instance.</summary>
	protected GoogleCloudFunctionBase(IServiceProvider serviceProvider)
	{
		ServiceProvider = serviceProvider;
	}

	/// <summary>Handles a cloud event.</summary>
	public virtual Task HandleEventAsync(
		TEventData eventData,
		GoogleCloudFunctionContext context,
		CancellationToken cancellationToken = default)
		=> Task.CompletedTask;
}

/// <summary>Cloud Run instance details.</summary>
public class CloudRunInstance
{
	/// <summary>Gets or sets the instance ID.</summary>
	public string InstanceId { get; set; } = Guid.NewGuid().ToString();

	/// <summary>Gets or sets the CPU utilization (0-1).</summary>
	public double CpuUtilization { get; set; }

	/// <summary>Gets or sets the memory utilization (0-1).</summary>
	public double MemoryUtilization { get; set; }

	/// <summary>Gets or sets the active request count.</summary>
	public int ActiveRequests { get; set; }

	/// <summary>Gets or sets the start time.</summary>
	public DateTimeOffset StartTime { get; set; } = DateTimeOffset.UtcNow;

	/// <summary>Gets or sets whether the instance is healthy.</summary>
	public bool IsHealthy { get; set; } = true;
}

/// <summary>Cloud Run instance manager interface.</summary>
public interface IInstanceManager : IDisposable
{
	/// <summary>Gets the current instance count asynchronously.</summary>
	Task<int> GetCurrentInstanceCountAsync(CancellationToken cancellationToken = default);

	/// <summary>Sets the instance count.</summary>
	Task SetInstanceCountAsync(int count, CancellationToken cancellationToken = default);

	/// <summary>Gets details about all instances.</summary>
	Task<CloudRunInstance[]> GetInstanceDetailsAsync(CancellationToken cancellationToken = default);

	/// <summary>Scales to zero instances.</summary>
	Task ScaleToZeroAsync(CancellationToken cancellationToken = default);

	/// <summary>Warms up instances.</summary>
	Task WarmupInstancesAsync(int count, CancellationToken cancellationToken = default);

	/// <summary>Applies traffic split across instances.</summary>
	Task ApplyTrafficSplitAsync(TrafficTarget[] trafficSplit, CancellationToken cancellationToken = default);
}

/// <summary>Google Cloud SDK container stub for testing.</summary>
public class GoogleCloudSdkContainer : IAsyncDisposable
{
	/// <summary>Gets the project ID.</summary>
	public string ProjectId { get; set; } = "test-project";

	/// <summary>Starts the container.</summary>
	public Task StartAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

	/// <summary>Stops the container.</summary>
	public Task StopAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

	/// <inheritdoc/>
	public ValueTask DisposeAsync()
	{
		GC.SuppressFinalize(this);
		return ValueTask.CompletedTask;
	}
}

/// <summary>Cloud Storage object stub.</summary>
public class StorageObject
{
	/// <summary>Gets or sets the name.</summary>
	public string? Name { get; set; }

	/// <summary>Gets or sets the bucket.</summary>
	public string? Bucket { get; set; }

	/// <summary>Gets or sets the content type.</summary>
	public string? ContentType { get; set; }

	/// <summary>Gets or sets the size.</summary>
	public ulong? Size { get; set; }
}

/// <summary>Google Cloud Storage client stub.</summary>
public sealed class StorageClient : IDisposable
{
	/// <summary>Creates a new storage client.</summary>
	public static StorageClient Create() => new();

	/// <summary>Uploads an object.</summary>
	public Task<StorageObject> UploadObjectAsync(
		string bucket,
		string objectName,
		string contentType,
		Stream source,
		CancellationToken cancellationToken = default)
		=> Task.FromResult(new StorageObject { Name = objectName, Bucket = bucket });

	/// <summary>Downloads an object.</summary>
	public Task DownloadObjectAsync(
		string bucket,
		string objectName,
		Stream destination,
		CancellationToken cancellationToken = default)
		=> Task.CompletedTask;

	/// <summary>Deletes an object.</summary>
	public Task DeleteObjectAsync(
		string bucket,
		string objectName,
		CancellationToken cancellationToken = default)
		=> Task.CompletedTask;

	/// <inheritdoc/>
	public void Dispose()
	{
		GC.SuppressFinalize(this);
	}
}

/// <summary>Google Cloud Function execution context.</summary>
public class GoogleCloudFunctionExecutionContext
{
	/// <summary>Gets or sets the function name.</summary>
	public string FunctionName { get; set; } = string.Empty;

	/// <summary>Gets or sets the project ID.</summary>
	public string ProjectId { get; set; } = string.Empty;

	/// <summary>Gets or sets the trace ID.</summary>
	public string TraceId { get; set; } = Guid.NewGuid().ToString();

	/// <summary>Gets or sets the execution ID.</summary>
	public string ExecutionId { get; set; } = Guid.NewGuid().ToString();

	/// <summary>Gets or sets whether this is a cold start.</summary>
	public bool IsColdStart { get; set; }

	/// <summary>Gets or sets the timestamp.</summary>
	public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;
}

/// <summary>Firebase Firestore container stub for testing.</summary>
public class FirebaseFirestoreContainer : IAsyncDisposable
{
	/// <summary>Gets the project ID.</summary>
	public string ProjectId { get; set; } = "test-project";

	/// <summary>Gets the connection string.</summary>
	public string ConnectionString => $"localhost:8080";

	/// <summary>Starts the container.</summary>
	public Task StartAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

	/// <summary>Stops the container.</summary>
	public Task StopAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

	/// <inheritdoc/>
	public ValueTask DisposeAsync()
	{
		GC.SuppressFinalize(this);
		return ValueTask.CompletedTask;
	}
}

/// <summary>Cloud Storage function base class.</summary>
public abstract class CloudStorageFunction
{
	/// <summary>Handles a cloud storage event.</summary>
	public abstract Task HandleAsync(CloudStorageEvent storageEvent, GoogleCloudFunctionExecutionContext context, CancellationToken cancellationToken = default);
}

/// <summary>Cloud Storage function base class (generic).</summary>
/// <typeparam name="TMetadata">The metadata type.</typeparam>
public abstract class CloudStorageFunction<TMetadata>
{
	/// <summary>Handles a cloud storage event with metadata.</summary>
	public abstract Task HandleAsync(CloudStorageEvent storageEvent, TMetadata? metadata, GoogleCloudFunctionExecutionContext context, CancellationToken cancellationToken = default);
}

/// <summary>Cloud Storage trigger options.</summary>
public class CloudStorageTriggerOptions
{
	/// <summary>Gets or sets the bucket name.</summary>
	public string BucketName { get; set; } = string.Empty;

	/// <summary>Gets or sets the object name prefix filter.</summary>
	public string? ObjectNamePrefix { get; set; }

	/// <summary>Gets or sets the event types to listen for.</summary>
	public CloudStorageEventType[] EventTypes { get; set; } = [CloudStorageEventType.Finalize];
}

/// <summary>Pub/Sub function base class.</summary>
/// <typeparam name="TMessage">The message type.</typeparam>
public abstract class PubSubFunction<TMessage>
{
	/// <summary>Handles a pub/sub message.</summary>
	public abstract Task HandleAsync(TMessage message, GoogleCloudFunctionExecutionContext context, CancellationToken cancellationToken = default);
}

/// <summary>Pub/Sub handler context.</summary>
public class PubSubHandlerContext
{
	/// <summary>Gets or sets the message ID.</summary>
	public string MessageId { get; set; } = Guid.NewGuid().ToString();

	/// <summary>Gets or sets the publish time.</summary>
	public DateTimeOffset PublishTime { get; set; } = DateTimeOffset.UtcNow;

	/// <summary>Gets or sets the subscription.</summary>
	public string? Subscription { get; set; }

	/// <summary>Gets or sets the ordering key.</summary>
	public string? OrderingKey { get; set; }

	/// <summary>Gets the attributes.</summary>
	public Dictionary<string, string> Attributes { get; } = new();
}

/// <summary>Traffic target for Cloud Run traffic splitting.</summary>
public class TrafficTarget
{
	/// <summary>Gets or sets the revision name.</summary>
	public string? RevisionName { get; set; }

	/// <summary>Gets or sets the traffic percentage (0-100).</summary>
	public int Percent { get; set; }

	/// <summary>Gets or sets the tag for this revision.</summary>
	public string? Tag { get; set; }

	/// <summary>Gets or sets whether this is the latest revision.</summary>
	public bool LatestRevision { get; set; }
}

/// <summary>Service information for service mesh registry.</summary>
public class ServiceInfo
{
	/// <summary>Gets or sets the service name.</summary>
	public string Name { get; set; } = string.Empty;

	/// <summary>Gets or sets the namespace.</summary>
	public string Namespace { get; set; } = string.Empty;

	/// <summary>Gets or sets the endpoint.</summary>
	public string Endpoint { get; set; } = string.Empty;

	/// <summary>Gets or sets the service version.</summary>
	public string Version { get; set; } = string.Empty;

	/// <summary>Gets or sets whether the service is healthy.</summary>
	public bool IsHealthy { get; set; } = true;
}

/// <summary>Service registry interface for service mesh.</summary>
public interface IServiceRegistry
{
	/// <summary>Registers a service.</summary>
	Task RegisterAsync(ServiceInfo serviceInfo, CancellationToken cancellationToken = default);

	/// <summary>Deregisters a service.</summary>
	Task DeregisterAsync(string serviceName, string namespaceName, CancellationToken cancellationToken = default);

	/// <summary>Discovers services in a namespace.</summary>
	Task<List<ServiceInfo>> DiscoverAsync(string namespaceName, CancellationToken cancellationToken = default);

	/// <summary>Updates health status of a service.</summary>
	Task UpdateHealthStatusAsync(string serviceName, string namespaceName, bool isHealthy, CancellationToken cancellationToken = default);

	/// <summary>Gets a specific service.</summary>
	Task<ServiceInfo?> GetServiceAsync(string serviceName, string namespaceName, CancellationToken cancellationToken = default);
}

/// <summary>Traffic manager interface for service mesh.</summary>
public interface ITrafficManager
{
	/// <summary>Configures traffic settings.</summary>
	Task ConfigureAsync(TrafficConfiguration configuration, CancellationToken cancellationToken = default);

	/// <summary>Applies a traffic rule.</summary>
	Task ApplyRuleAsync(TrafficRule rule, CancellationToken cancellationToken = default);

	/// <summary>Removes a traffic rule.</summary>
	Task RemoveRuleAsync(string ruleName, CancellationToken cancellationToken = default);

	/// <summary>Gets active rules.</summary>
	Task<List<TrafficRule>> GetActiveRulesAsync(CancellationToken cancellationToken = default);

	/// <summary>Applies traffic split for a service.</summary>
	Task ApplyTrafficSplitAsync(string serviceName, List<TrafficSplit> splits, CancellationToken cancellationToken = default);
}
