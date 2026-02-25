// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Data.Abstractions.CloudNative;

/// <summary>
/// Defines outbox operations optimized for cloud-native databases.
/// </summary>
/// <remarks>
/// <para>
/// This interface provides serverless-friendly outbox pattern implementation:
/// <list type="bullet">
/// <item>No background service required - uses change feed triggers</item>
/// <item>Transactional batch support within partitions</item>
/// <item>Idempotent message processing</item>
/// <item>Cost-aware operations with RU tracking</item>
/// </list>
/// </para>
/// <para>
/// <strong>Serverless Pattern:</strong>
/// <code>
/// 1. Write event + outbox entry in transactional batch
/// 2. Change feed triggers serverless function (Azure Function / Lambda / Cloud Function)
/// 3. Function publishes to message broker
/// 4. Mark outbox entry as processed
/// </code>
/// </para>
/// <para>
/// <strong>Provider-Specific Triggers:</strong>
/// <list type="bullet">
/// <item>Cosmos DB: Change feed → Azure Function</item>
/// <item>DynamoDB: DynamoDB Streams → Lambda</item>
/// <item>Firestore: Cloud Functions trigger</item>
/// </list>
/// </para>
/// </remarks>
public interface ICloudNativeOutboxStore
{
	/// <summary>
	/// Gets the underlying cloud provider type.
	/// </summary>
	CloudProviderType ProviderType { get; }

	/// <summary>
	/// Adds a message to the outbox within a transactional batch.
	/// </summary>
	/// <param name="message">The outbox message to add.</param>
	/// <param name="partitionKey">The partition key for the message.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>The operation result with cost information.</returns>
	Task<CloudOperationResult<CloudOutboxMessage>> AddAsync(
		CloudOutboxMessage message,
		IPartitionKey partitionKey,
		CancellationToken cancellationToken);

	/// <summary>
	/// Adds multiple messages to the outbox in a transactional batch.
	/// </summary>
	/// <param name="messages">The outbox messages to add.</param>
	/// <param name="partitionKey">The partition key for all messages.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>The batch result with cost information.</returns>
	/// <remarks>
	/// All messages must belong to the same partition for transactional guarantees.
	/// </remarks>
	Task<CloudBatchResult> AddBatchAsync(
		IEnumerable<CloudOutboxMessage> messages,
		IPartitionKey partitionKey,
		CancellationToken cancellationToken);

	/// <summary>
	/// Gets pending (unpublished) messages from a partition in FIFO order.
	/// </summary>
	/// <param name="partitionKey">The partition key to query.</param>
	/// <param name="batchSize">Maximum number of messages to retrieve.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>Pending messages with cost information.</returns>
	Task<CloudQueryResult<CloudOutboxMessage>> GetPendingAsync(
		IPartitionKey partitionKey,
		int batchSize,
		CancellationToken cancellationToken);

	/// <summary>
	/// Marks a message as published.
	/// </summary>
	/// <param name="messageId">The message identifier.</param>
	/// <param name="partitionKey">The partition key for the message.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>The operation result with cost information.</returns>
	Task<CloudOperationResult> MarkAsPublishedAsync(
		string messageId,
		IPartitionKey partitionKey,
		CancellationToken cancellationToken);

	/// <summary>
	/// Marks multiple messages as published in a batch.
	/// </summary>
	/// <param name="messageIds">The message identifiers.</param>
	/// <param name="partitionKey">The partition key for all messages.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>The batch result with cost information.</returns>
	Task<CloudBatchResult> MarkBatchAsPublishedAsync(
		IEnumerable<string> messageIds,
		IPartitionKey partitionKey,
		CancellationToken cancellationToken);

	/// <summary>
	/// Deletes published messages older than the specified retention period.
	/// </summary>
	/// <param name="partitionKey">The partition key to clean.</param>
	/// <param name="retentionPeriod">The retention period for published messages.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>The number of messages deleted and cost information.</returns>
	Task<CloudCleanupResult> CleanupOldMessagesAsync(
		IPartitionKey partitionKey,
		TimeSpan retentionPeriod,
		CancellationToken cancellationToken);

	/// <summary>
	/// Creates a change feed subscription for outbox processing.
	/// </summary>
	/// <param name="options">Change feed options.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>A subscription that streams new outbox messages.</returns>
	/// <remarks>
	/// Use this for push-based outbox processing instead of polling.
	/// </remarks>
	Task<IChangeFeedSubscription<CloudOutboxMessage>> SubscribeToNewMessagesAsync(
		IChangeFeedOptions? options,
		CancellationToken cancellationToken);

	/// <summary>
	/// Increments the retry count for a message that failed to publish.
	/// </summary>
	/// <param name="messageId">The message identifier.</param>
	/// <param name="partitionKey">The partition key for the message.</param>
	/// <param name="errorMessage">Optional error message to record.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>The operation result with cost information.</returns>
	Task<CloudOperationResult> IncrementRetryCountAsync(
		string messageId,
		IPartitionKey partitionKey,
		string? errorMessage,
		CancellationToken cancellationToken);
}

/// <summary>
/// Represents an outbox message in a cloud-native database.
/// </summary>
public sealed record CloudOutboxMessage
{
	/// <summary>
	/// Gets the unique message identifier.
	/// </summary>
	public required string MessageId { get; init; }

	/// <summary>
	/// Gets the message type (typically the event type name).
	/// </summary>
	public required string MessageType { get; init; }

	/// <summary>
	/// Gets the serialized message payload.
	/// </summary>
	public required byte[] Payload { get; init; }

	/// <summary>
	/// Gets the message headers/metadata.
	/// </summary>
	public IDictionary<string, string>? Headers { get; init; }

	/// <summary>
	/// Gets the aggregate ID associated with the message.
	/// </summary>
	public string? AggregateId { get; init; }

	/// <summary>
	/// Gets the aggregate type associated with the message.
	/// </summary>
	public string? AggregateType { get; init; }

	/// <summary>
	/// Gets the correlation ID for distributed tracing.
	/// </summary>
	public string? CorrelationId { get; init; }

	/// <summary>
	/// Gets the causation ID linking to the causing message.
	/// </summary>
	public string? CausationId { get; init; }

	/// <summary>
	/// Gets when the message was created.
	/// </summary>
	public required DateTimeOffset CreatedAt { get; init; }

	/// <summary>
	/// Gets when the message was published, or null if not yet published.
	/// </summary>
	public DateTimeOffset? PublishedAt { get; init; }

	/// <summary>
	/// Gets the number of publish retry attempts.
	/// </summary>
	public int RetryCount { get; init; }

	/// <summary>
	/// Gets the last error message if publishing failed.
	/// </summary>
	public string? LastError { get; init; }

	/// <summary>
	/// Gets the partition key value for the message.
	/// </summary>
	public required string PartitionKeyValue { get; init; }

	/// <summary>
	/// Gets the ETag for optimistic concurrency.
	/// </summary>
	public string? ETag { get; init; }

	/// <summary>
	/// Gets a value indicating whether the message has been published.
	/// </summary>
	public bool IsPublished => PublishedAt.HasValue;
}

/// <summary>
/// Represents the result of a cleanup operation.
/// </summary>
public sealed class CloudCleanupResult
{
	/// <summary>
	/// Initializes a new instance of the <see cref="CloudCleanupResult"/> class.
	/// </summary>
	public CloudCleanupResult(int deletedCount, double requestCharge)
	{
		DeletedCount = deletedCount;
		RequestCharge = requestCharge;
	}

	/// <summary>
	/// Gets the number of items deleted.
	/// </summary>
	public int DeletedCount { get; }

	/// <summary>
	/// Gets the total request charge for the cleanup operation.
	/// </summary>
	public double RequestCharge { get; }
}
