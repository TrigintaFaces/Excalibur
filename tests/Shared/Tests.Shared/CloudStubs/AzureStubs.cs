// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Tests.Shared.CloudStubs.Azure;

/// <summary>Azure Service Bus client stub for compilation.</summary>
public class ServiceBusClient : IAsyncDisposable
{
	/// <summary>Initializes a new instance with connection string.</summary>
	public ServiceBusClient(string connectionString) { }

	/// <summary>Initializes a new instance with namespace and credential.</summary>
	public ServiceBusClient(string fullyQualifiedNamespace, object credential) { }

	/// <summary>Creates a sender for the specified queue or topic.</summary>
	public ServiceBusSender CreateSender(string queueOrTopicName) => new();

	/// <summary>Creates a receiver for the specified queue.</summary>
	public ServiceBusReceiver CreateReceiver(string queueName) => new();

	/// <summary>Creates a receiver for the specified subscription.</summary>
	public ServiceBusReceiver CreateReceiver(string topicName, string subscriptionName) => new();

	/// <summary>Creates a processor for the specified queue.</summary>
	public ServiceBusProcessor CreateProcessor(string queueName) => new();

	/// <inheritdoc/>
	public ValueTask DisposeAsync()
	{
		GC.SuppressFinalize(this);
		return ValueTask.CompletedTask;
	}
}

/// <summary>Azure Service Bus sender stub.</summary>
public class ServiceBusSender : IAsyncDisposable
{
	/// <summary>Sends a message.</summary>
	public Task SendMessageAsync(ServiceBusMessage message, CancellationToken cancellationToken = default) => Task.CompletedTask;

	/// <summary>Sends a batch of messages.</summary>
	public Task SendMessagesAsync(IEnumerable<ServiceBusMessage> messages, CancellationToken cancellationToken = default) => Task.CompletedTask;

	/// <inheritdoc/>
	public ValueTask DisposeAsync()
	{
		GC.SuppressFinalize(this);
		return ValueTask.CompletedTask;
	}
}

/// <summary>Azure Service Bus receiver stub.</summary>
public class ServiceBusReceiver : IAsyncDisposable
{
	/// <summary>Receives a message.</summary>
	public Task<ServiceBusReceivedMessage?> ReceiveMessageAsync(TimeSpan? maxWaitTime = null, CancellationToken cancellationToken = default)
		=> Task.FromResult<ServiceBusReceivedMessage?>(null);

	/// <summary>Receives messages.</summary>
	public Task<IReadOnlyList<ServiceBusReceivedMessage>> ReceiveMessagesAsync(int maxMessages, TimeSpan? maxWaitTime = null, CancellationToken cancellationToken = default)
		=> Task.FromResult<IReadOnlyList<ServiceBusReceivedMessage>>([]);

	/// <summary>Completes a message.</summary>
	public Task CompleteMessageAsync(ServiceBusReceivedMessage message, CancellationToken cancellationToken = default) => Task.CompletedTask;

	/// <summary>Abandons a message.</summary>
	public Task AbandonMessageAsync(ServiceBusReceivedMessage message, IDictionary<string, object>? propertiesToModify = null, CancellationToken cancellationToken = default) => Task.CompletedTask;

	/// <summary>Dead letters a message.</summary>
	public Task DeadLetterMessageAsync(ServiceBusReceivedMessage message, string deadLetterReason, string? deadLetterErrorDescription = null, CancellationToken cancellationToken = default) => Task.CompletedTask;

	/// <inheritdoc/>
	public ValueTask DisposeAsync()
	{
		GC.SuppressFinalize(this);
		return ValueTask.CompletedTask;
	}
}

/// <summary>Azure Service Bus processor stub.</summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1003:Use generic event handler instances", Justification = "Matching Azure SDK signature for stub compatibility.")]
public class ServiceBusProcessor : IAsyncDisposable
{
	/// <summary>Event handler for processing messages.</summary>
	public event Func<ProcessMessageEventArgs, Task>? ProcessMessageAsync;

	/// <summary>Event handler for processing errors.</summary>
	public event Func<ProcessErrorEventArgs, Task>? ProcessErrorAsync;

	/// <summary>Starts the processor.</summary>
	public Task StartProcessingAsync(CancellationToken cancellationToken = default)
	{
		_ = ProcessMessageAsync;
		_ = ProcessErrorAsync;
		return Task.CompletedTask;
	}

	/// <summary>Stops the processor.</summary>
	public Task StopProcessingAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

	/// <inheritdoc/>
	public ValueTask DisposeAsync()
	{
		GC.SuppressFinalize(this);
		return ValueTask.CompletedTask;
	}
}

/// <summary>Process message event args stub.</summary>
public class ProcessMessageEventArgs
{
	/// <summary>Gets the received message.</summary>
	public ServiceBusReceivedMessage Message { get; } = new();

	/// <summary>Gets the cancellation token.</summary>
	public CancellationToken CancellationToken { get; }

	/// <summary>Completes the message.</summary>
	public Task CompleteMessageAsync(ServiceBusReceivedMessage message, CancellationToken cancellationToken = default) => Task.CompletedTask;

	/// <summary>Dead-letters the message.</summary>
	public Task DeadLetterMessageAsync(ServiceBusReceivedMessage message, string? deadLetterReason = null, string? deadLetterErrorDescription = null, CancellationToken cancellationToken = default) => Task.CompletedTask;

	/// <summary>Abandons the message for redelivery.</summary>
	public Task AbandonMessageAsync(ServiceBusReceivedMessage message, IDictionary<string, object>? propertiesToModify = null, CancellationToken cancellationToken = default) => Task.CompletedTask;
}

/// <summary>Process error event args stub.</summary>
public class ProcessErrorEventArgs
{
	/// <summary>Gets the exception.</summary>
	public Exception Exception { get; } = new InvalidOperationException();

	/// <summary>Gets the error source.</summary>
	public string ErrorSource { get; } = string.Empty;

	/// <summary>Gets the identifier of the processor that raised the error.</summary>
	public string Identifier { get; } = string.Empty;
}

/// <summary>Azure Service Bus message stub.</summary>
public class ServiceBusMessage
{
	/// <summary>Initializes a new instance.</summary>
	public ServiceBusMessage() { }

	/// <summary>Initializes a new instance with body.</summary>
	public ServiceBusMessage(string body) => Body = BinaryData.FromString(body);

	/// <summary>Initializes a new instance with binary body.</summary>
	public ServiceBusMessage(BinaryData body) => Body = body;

	/// <summary>Gets or sets the message body.</summary>
	public BinaryData Body { get; set; } = BinaryData.FromString(string.Empty);

	/// <summary>Gets or sets the message ID.</summary>
	public string? MessageId { get; set; }

	/// <summary>Gets or sets the content type.</summary>
	public string? ContentType { get; set; }

	/// <summary>Gets or sets the subject.</summary>
	public string? Subject { get; set; }

	/// <summary>Gets or sets the correlation ID.</summary>
	public string? CorrelationId { get; set; }

	/// <summary>Gets the application properties.</summary>
	public IDictionary<string, object> ApplicationProperties { get; } = new Dictionary<string, object>();
}

/// <summary>Azure Service Bus received message stub.</summary>
public class ServiceBusReceivedMessage
{
	/// <summary>Gets the message body.</summary>
	public BinaryData Body { get; } = BinaryData.FromString(string.Empty);

	/// <summary>Gets the message ID.</summary>
	public string MessageId { get; } = Guid.NewGuid().ToString();

	/// <summary>Gets the content type.</summary>
	public string? ContentType { get; }

	/// <summary>Gets the subject.</summary>
	public string? Subject { get; }

	/// <summary>Gets the correlation ID.</summary>
	public string? CorrelationId { get; }

	/// <summary>Gets the lock token.</summary>
	public string LockToken { get; } = Guid.NewGuid().ToString();

	/// <summary>Gets the delivery count.</summary>
	public int DeliveryCount { get; }

	/// <summary>Gets the enqueued time.</summary>
	public DateTimeOffset EnqueuedTime { get; }

	/// <summary>Gets the partition key.</summary>
	public string? PartitionKey { get; }

	/// <summary>Gets the session ID.</summary>
	public string? SessionId { get; }

	/// <summary>Gets the time the lock on this message expires.</summary>
	public DateTimeOffset LockedUntil { get; }

	/// <summary>Gets the sequence number.</summary>
	public long SequenceNumber { get; }

	/// <summary>Gets the application properties.</summary>
	public IReadOnlyDictionary<string, object> ApplicationProperties { get; } = new Dictionary<string, object>();

	/// <summary>Gets the dead letter reason.</summary>
	public string? DeadLetterReason { get; }

	/// <summary>Gets the dead letter error description.</summary>
	public string? DeadLetterErrorDescription { get; }
}

/// <summary>Azure Service Bus administration client stub.</summary>
public class ServiceBusAdministrationClient
{
	/// <summary>Initializes a new instance with connection string.</summary>
	public ServiceBusAdministrationClient(string connectionString) { }

	/// <summary>Creates a queue if it doesn't exist.</summary>
	public Task<QueueProperties> CreateQueueAsync(string queueName, CancellationToken cancellationToken = default) => Task.FromResult(new QueueProperties());

	/// <summary>Gets queue runtime properties.</summary>
	public Task<QueueRuntimeProperties> GetQueueRuntimePropertiesAsync(string queueName, CancellationToken cancellationToken = default)
		=> Task.FromResult(new QueueRuntimeProperties());

	/// <summary>Checks if a queue exists.</summary>
	public Task<bool> QueueExistsAsync(string queueName, CancellationToken cancellationToken = default) => Task.FromResult(true);
}

/// <summary>Queue properties stub.</summary>
public class QueueProperties
{
	/// <summary>Gets or sets the queue name.</summary>
	public string Name { get; set; } = string.Empty;
}

/// <summary>Queue runtime properties stub.</summary>
public class QueueRuntimeProperties
{
	/// <summary>Gets the active message count.</summary>
	public long ActiveMessageCount { get; }

	/// <summary>Gets the dead letter message count.</summary>
	public long DeadLetterMessageCount { get; }

	/// <summary>Gets the total message count.</summary>
	public long TotalMessageCount { get; }
}

/// <summary>Service Bus connection pool stub.</summary>
public class ServiceBusConnectionPool : IAsyncDisposable
{
	/// <summary>Gets a client from the pool.</summary>
	public ServiceBusClient GetClient() => new("connection-string");

	/// <inheritdoc/>
	public ValueTask DisposeAsync()
	{
		GC.SuppressFinalize(this);
		return ValueTask.CompletedTask;
	}
}

/// <summary>Service Bus health checker stub.</summary>
public class ServiceBusHealthChecker
{
	/// <summary>Checks the health of a queue.</summary>
	public Task<bool> CheckHealthAsync(string queueName, CancellationToken cancellationToken = default) => Task.FromResult(true);
}

/// <summary>Service Bus integration test base stub.</summary>
public abstract class ServiceBusIntegrationTestBase : IAsyncLifetime
{
	/// <inheritdoc/>
	public virtual Task InitializeAsync() => Task.CompletedTask;

	/// <inheritdoc/>
	public virtual Task DisposeAsync() => Task.CompletedTask;
}

/// <summary>Batch receiving options stub.</summary>
public class BatchReceivingOptions
{
	/// <summary>Gets or sets the max batch size.</summary>
	public int MaxBatchSize { get; set; } = 10;

	/// <summary>Gets or sets the max wait time.</summary>
	public TimeSpan MaxWaitTime { get; set; } = TimeSpan.FromSeconds(5);
}

/// <summary>Service Bus channel options stub.</summary>
public class ServiceBusChannelOptions
{
	/// <summary>Gets or sets the connection string.</summary>
	public string? ConnectionString { get; set; }

	/// <summary>Gets or sets the queue name.</summary>
	public string? QueueName { get; set; }
}

/// <summary>Service Bus message serializer interface stub.</summary>
public interface IServiceBusMessageSerializer
{
	/// <summary>Serializes the message.</summary>
	ServiceBusMessage Serialize<T>(T message);

	/// <summary>Deserializes the message.</summary>
	T? Deserialize<T>(ServiceBusReceivedMessage message);
}

/// <summary>Service Bus container stub for testcontainers.</summary>
public class ServiceBusContainer : IAsyncDisposable
{
	/// <summary>Gets the connection string.</summary>
	public string GetConnectionString() => "Endpoint=sb://localhost;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=test";

	/// <summary>Starts the container.</summary>
	public Task StartAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

	/// <inheritdoc/>
	public ValueTask DisposeAsync()
	{
		GC.SuppressFinalize(this);
		return ValueTask.CompletedTask;
	}
}

/// <summary>Azurite container stub for testcontainers.</summary>
public class AzuriteContainer : IAsyncDisposable
{
	/// <summary>Gets the blob service connection string.</summary>
	public string GetConnectionString() => "UseDevelopmentStorage=true";

	/// <summary>Starts the container.</summary>
	public Task StartAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

	/// <inheritdoc/>
	public ValueTask DisposeAsync()
	{
		GC.SuppressFinalize(this);
		return ValueTask.CompletedTask;
	}
}

/// <summary>Dead letter manager interface stub.</summary>
public interface IDeadLetterManager
{
	/// <summary>Gets dead letter messages.</summary>
	Task<IReadOnlyList<ServiceBusReceivedMessage>> GetDeadLetterMessagesAsync(string queueName, int maxMessages, CancellationToken cancellationToken = default);

	/// <summary>Reprocesses a dead letter message.</summary>
	Task ReprocessAsync(ServiceBusReceivedMessage message, CancellationToken cancellationToken = default);
}

/// <summary>Dead letter recovery strategy interface stub.</summary>
public interface IDeadLetterRecoveryStrategy
{
	/// <summary>Evaluates the recovery decision for a dead letter message asynchronously.</summary>
	ValueTask<RecoveryDecision> EvaluateAsync(DeadLetterContext context, CancellationToken cancellationToken = default);
}

/// <summary>Dead letter context stub.</summary>
public class DeadLetterContext
{
	/// <summary>Gets or sets the message.</summary>
	public ServiceBusReceivedMessage? Message { get; set; }

	/// <summary>Gets or sets the dead letter reason.</summary>
	public string? DeadLetterReason { get; set; }

	/// <summary>Gets or sets the error description.</summary>
	public string? ErrorDescription { get; set; }
}

/// <summary>Recovery decision stub.</summary>
public class RecoveryDecision
{
	private RecoveryDecision(RecoveryAction action, TimeSpan? delay = null)
	{
		Action = action;
		Delay = delay;
	}

	/// <summary>Gets the recovery action.</summary>
	public RecoveryAction Action { get; }

	/// <summary>Gets the delay before retry (if applicable).</summary>
	public TimeSpan? Delay { get; }

	/// <summary>Creates a retry immediately decision.</summary>
	public static RecoveryDecision RetryImmediately() => new(RecoveryAction.Retry);

	/// <summary>Creates a retry after delay decision.</summary>
	public static RecoveryDecision RetryAfter(TimeSpan delay) => new(RecoveryAction.Retry, delay);

	/// <summary>Creates an abandon decision.</summary>
	public static RecoveryDecision Abandon() => new(RecoveryAction.Abandon);

	/// <summary>Creates a delete decision.</summary>
	public static RecoveryDecision Delete() => new(RecoveryAction.Delete);
}

/// <summary>Recovery action enum.</summary>
public enum RecoveryAction
{
	/// <summary>Retry the message.</summary>
	Retry,

	/// <summary>Abandon the message.</summary>
	Abandon,

	/// <summary>Delete the message.</summary>
	Delete
}

/// <summary>Dead letter metrics interface stub.</summary>
public interface IDeadLetterMetrics
{
	/// <summary>Records a batch processed.</summary>
	void RecordBatchProcessed(int messageCount, long totalBytes);

	/// <summary>Records a batch failed.</summary>
	void RecordBatchFailed(int messageCount, Exception exception);

	/// <summary>Records a recovery attempt.</summary>
	void RecordRecoveryAttempt(DeadLetterReason reason, bool success);

	/// <summary>Records the queue depth.</summary>
	void RecordQueueDepth(long depth);
}

/// <summary>Dead letter reason enum stub.</summary>
public enum DeadLetterReason
{
	/// <summary>Max delivery attempts exceeded.</summary>
	MaxDeliveryExceeded,

	/// <summary>Processing error.</summary>
	ProcessingError,

	/// <summary>Message expired.</summary>
	Expired,

	/// <summary>Manual dead lettering.</summary>
	Manual
}

/// <summary>Dead letter batch stub.</summary>
public class DeadLetterBatch
{
	private readonly List<ServiceBusReceivedMessage> _messages;

	/// <summary>Creates a new dead letter batch with a capacity.</summary>
	public DeadLetterBatch(int capacity = 100)
	{
		_messages = new List<ServiceBusReceivedMessage>(capacity);
	}

	/// <summary>Gets the messages.</summary>
	public IReadOnlyList<ServiceBusReceivedMessage> Messages => _messages;

	/// <summary>Gets the batch size.</summary>
	public int BatchSize => _messages.Count;

	/// <summary>Adds a message to the batch.</summary>
	public void Add(ServiceBusReceivedMessage message) => _messages.Add(message);

	/// <summary>Clears the batch.</summary>
	public void Clear() => _messages.Clear();
}

/// <summary>Claim check provider interface stub.</summary>
public interface IClaimCheckProvider
{
	/// <summary>Stores large message payload.</summary>
	Task<string> StoreAsync(BinaryData data, CancellationToken cancellationToken = default);

	/// <summary>Retrieves large message payload.</summary>
	Task<BinaryData> RetrieveAsync(string reference, CancellationToken cancellationToken = default);

	/// <summary>Deletes stored payload.</summary>
	Task DeleteAsync(string reference, CancellationToken cancellationToken = default);
}

/// <summary>Message buffer pool stub.</summary>
public class MessageBufferPool
{
	/// <summary>Rents a buffer.</summary>
	public byte[] Rent(int minimumLength) => new byte[minimumLength];

	/// <summary>Returns a buffer.</summary>
	public void Return(byte[] buffer) { }
}

/// <summary>Service Bus session receiver stub.</summary>
public class ServiceBusSessionReceiver : IAsyncDisposable
{
	/// <summary>Gets the session ID.</summary>
	public string SessionId { get; } = string.Empty;

	/// <summary>Gets the session locked until time.</summary>
	public DateTimeOffset SessionLockedUntil { get; } = DateTimeOffset.UtcNow.AddMinutes(5);

	/// <summary>Receives a message.</summary>
	public Task<ServiceBusReceivedMessage?> ReceiveMessageAsync(TimeSpan? maxWaitTime = null, CancellationToken cancellationToken = default)
		=> Task.FromResult<ServiceBusReceivedMessage?>(null);

	/// <summary>Receives messages.</summary>
	public Task<IReadOnlyList<ServiceBusReceivedMessage>> ReceiveMessagesAsync(int maxMessages, TimeSpan? maxWaitTime = null, CancellationToken cancellationToken = default)
		=> Task.FromResult<IReadOnlyList<ServiceBusReceivedMessage>>([]);

	/// <summary>Completes a message.</summary>
	public Task CompleteMessageAsync(ServiceBusReceivedMessage message, CancellationToken cancellationToken = default) => Task.CompletedTask;

	/// <summary>Gets the session state.</summary>
	public Task<BinaryData?> GetSessionStateAsync(CancellationToken cancellationToken = default)
		=> Task.FromResult<BinaryData?>(null);

	/// <summary>Sets the session state.</summary>
	public Task SetSessionStateAsync(BinaryData sessionState, CancellationToken cancellationToken = default) => Task.CompletedTask;

	/// <summary>Renews the session lock.</summary>
	public Task RenewSessionLockAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

	/// <inheritdoc/>
	public ValueTask DisposeAsync()
	{
		GC.SuppressFinalize(this);
		return ValueTask.CompletedTask;
	}
}

/// <summary>Session state provider interface stub.</summary>
public interface ISessionStateProvider
{
	/// <summary>Gets session state.</summary>
	Task<T?> GetStateAsync<T>(string sessionId, CancellationToken cancellationToken = default) where T : class;

	/// <summary>Sets session state.</summary>
	Task SetStateAsync<T>(string sessionId, T state, CancellationToken cancellationToken = default) where T : class;

	/// <summary>Clears session state.</summary>
	Task ClearStateAsync(string sessionId, CancellationToken cancellationToken = default);
}
