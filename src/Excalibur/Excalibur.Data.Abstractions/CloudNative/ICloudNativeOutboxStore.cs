// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch;

namespace Excalibur.Data.CloudNative;

/// <summary>
/// Defines outbox operations optimized for cloud-native databases that use change-feed
/// triggers rather than background polling.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Relationship to <c>IOutboxStore</c>:</strong>
/// This interface is intentionally separate from
/// <c>Excalibur.Dispatch.IOutboxStore</c>. The two serve fundamentally
/// different outbox patterns:
/// </para>
/// <list type="bullet">
/// <item><c>IOutboxStore</c> -- traditional polling-based outbox for SQL Server, Postgres, MongoDB.
///   Uses <c>OutboxBackgroundService</c> and SQL transactions.</item>
/// <item><c>ICloudNativeOutboxStore</c> -- serverless outbox for Cosmos DB, DynamoDB, Firestore.
///   Uses change-feed triggers, partition keys, and cloud-native batching.</item>
/// </list>
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
/// <para>
/// <strong>Tenancy:</strong>
/// Rows written through this contract belong to a tenant, so it declares
/// <see cref="TenantOwnedAttribute"/>. A store registered under this contract in a deployment using
/// row-discriminator multi-tenancy must present a tenant capability or be refused at registration.
/// The applicable one is the partitioned capability, not the ambient-scoping one: this is a
/// change-feed contract whose reads are addressed by partition key and are deliberately estate-wide -
/// <see cref="GetPendingAsync"/> takes no tenant - so the owning tenant is carried on the row in
/// <see cref="CloudOutboxMessage.TenantId"/> and re-established from it when the message is read back.
/// A store confining these reads to an ambient tenant would read that tenant as absent on the trigger
/// path, return the empty set, and stall publication for every tenant.
/// </para>
/// </remarks>
[TenantOwned]
public interface ICloudNativeOutboxStore
{
	/// <summary>
	/// Gets the underlying cloud provider type.
	/// </summary>
	CloudPersistenceProviderType ProviderType { get; }

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
	/// Gets pending (unpublished) messages from a partition in FIFO order.
	/// </summary>
	/// <remarks>
	/// <para>
	/// <strong>This is a query, not a claim.</strong> It modifies nothing: no lease is taken, no message is
	/// marked, no row is written. Two callers running it concurrently against the same partition therefore
	/// receive the <strong>same</strong> messages and both will publish them. It does not divide work
	/// between processes, and no amount of care at the call site makes it do so.
	/// </para>
	/// <para>
	/// <strong>Do not build a multi-process poller on it.</strong> Two models support concurrent publishing
	/// and this member is part of neither: the provider's change-feed trigger, which holds one lease per
	/// partition, and — where a self-managed poller is genuinely wanted —
	/// <see cref="ICloudNativeOutboxStoreClaim.ClaimPendingAsync"/>, which takes a real per-message lease.
	/// Choose one and do not combine them: the trigger path does not observe the claim's lease, so running
	/// both reproduces the duplicate delivery the claim exists to prevent.
	/// </para>
	/// <para>
	/// <strong>What this read is for: recovery.</strong> A change feed surfaces a document when it is
	/// written or updated, and not again on its own. A message whose publish failed, and whose failure was
	/// never recorded back onto the document, is consequently never surfaced a second time — the feed has
	/// already moved past it, and nothing else will mention it. This read is how such a message is found.
	/// The ordinary retry path does not need it: recording the failure through
	/// <see cref="ICloudNativeOutboxStoreBatch.IncrementRetryCountAsync"/> rewrites the document, which puts
	/// it back on the feed. This read is the backstop for when that did not happen — a publisher that
	/// swallowed the error, or one that stopped between the failed publish and the record of it.
	/// </para>
	/// <para>
	/// Run it from a single process — a scheduled sweep or an operator tool — and treat overlapping runs as
	/// producing duplicates, which handlers must tolerate in any case.
	/// </para>
	/// <para>
	/// <strong>FIFO, and what "eventually" can mean for a provider that orders via a secondary index.</strong>
	/// A provider whose native query engine can order a strongly-consistent read (Cosmos DB's <c>ORDER BY</c>,
	/// Firestore's <c>OrderBy</c>) returns messages in exact creation order with no further caveat. A provider
	/// that must order via a separately-maintained, eventually-consistent index (for example DynamoDB, whose
	/// base-table query is physically ordered by a per-message key and therefore orders pending reads through
	/// a Global Secondary Index instead) can, for a brief interval after <see cref="AddAsync"/> returns, omit
	/// a just-staged message from this read — a LATENCY property, not a loss one: the message is durably
	/// present on the provider's strongly-consistent primary store throughout, and a subsequent call (the next
	/// poll, or this recovery sweep) will see it. AWS documents GSI propagation as typically completing
	/// within a fraction of a second under normal conditions, with no formal upper bound (a failure
	/// scenario can extend it) — design a recovery sweep interval, never a single immediate read, around
	/// this guarantee. Messages this call DOES return are always in creation order.
	/// </para>
	/// </remarks>
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
	/// Creates a change feed subscription for outbox processing.
	/// </summary>
	/// <param name="options">Change feed options.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>A subscription that streams new outbox messages.</returns>
	Task<IChangeFeedSubscription<CloudOutboxMessage>> SubscribeToNewMessagesAsync(
		IChangeFeedOptions? options,
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
	/// Gets the tenant identifier, preserving tenant isolation through the cloud-native outbox round-trip.
	/// </summary>
	public string? TenantId { get; init; }

	/// <summary>
	/// Gets the delivery destination this message is routed to, preserving the routing target through the
	/// cloud-native outbox round-trip rather than dropping it on the stage-then-reload path.
	/// </summary>
	public string? Destination { get; init; }

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
	/// Gets the instant this message's current claim lease was stamped, or <see langword="null"/> if the
	/// message is unclaimed.
	/// </summary>
	/// <remarks>
	/// The lease <i>expires</i> at this instant plus the store's configured lease timeout; the timeout is
	/// configuration rather than a per-message field, so that changing it takes effect on messages already
	/// staged. Only stores implementing <see cref="ICloudNativeOutboxStoreClaim"/> ever set this.
	/// </remarks>
	public DateTimeOffset? LeasedAt { get; init; }

	/// <summary>
	/// Gets the identifier of the claimant currently holding this message, or <see langword="null"/> if the
	/// message is unclaimed.
	/// </summary>
	/// <remarks>
	/// A stale value is expected and harmless: once the lease expires the message is claimable regardless of
	/// who is named here, and the next claim overwrites it. Read this as "who took it last", not as a live
	/// ownership assertion.
	/// </remarks>
	public string? LeasedBy { get; init; }

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
