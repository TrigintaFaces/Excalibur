// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Dispatch.Abstractions;

namespace Excalibur.Data.Abstractions.CloudNative;

/// <summary>
/// Defines event store operations optimized for cloud-native document databases.
/// </summary>
/// <remarks>
/// <para>
/// This interface extends the core event store pattern with cloud-native optimizations:
/// <list type="bullet">
/// <item>Partition-aware event storage and retrieval</item>
/// <item>Optimistic concurrency via document versioning/ETags</item>
/// <item>Change feed integration for subscriptions</item>
/// <item>Cost tracking (RU/capacity consumption)</item>
/// </list>
/// </para>
/// <para>
/// <strong>Document Model:</strong>
/// <code>
/// Document ID: {tenantId}:{aggregateId}:{version}
/// Partition Key: {tenantId} or {aggregateId}
/// </code>
/// </para>
/// <para>
/// <strong>Concurrency:</strong>
/// Uses conditional writes on version field to ensure optimistic concurrency
/// without cross-partition transactions.
/// </para>
/// </remarks>
public interface ICloudNativeEventStore
{
	/// <summary>
	/// Gets the underlying cloud provider type.
	/// </summary>
	CloudProviderType ProviderType { get; }

	/// <summary>
	/// Loads all events for an aggregate within a partition.
	/// </summary>
	/// <param name="aggregateId">The aggregate identifier.</param>
	/// <param name="aggregateType">The aggregate type name.</param>
	/// <param name="partitionKey">The partition key containing the aggregate.</param>
	/// <param name="consistencyOptions">Consistency options for the read.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>The events for the aggregate in version order, with cost information.</returns>
	Task<CloudEventLoadResult> LoadAsync(
		string aggregateId,
		string aggregateType,
		IPartitionKey partitionKey,
		IConsistencyOptions? consistencyOptions,
		CancellationToken cancellationToken);

	/// <summary>
	/// Loads events for an aggregate from a specific version.
	/// </summary>
	/// <param name="aggregateId">The aggregate identifier.</param>
	/// <param name="aggregateType">The aggregate type name.</param>
	/// <param name="partitionKey">The partition key containing the aggregate.</param>
	/// <param name="fromVersion">The version to start loading from (exclusive).</param>
	/// <param name="consistencyOptions">Consistency options for the read.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>The events from the specified version, with cost information.</returns>
	Task<CloudEventLoadResult> LoadFromVersionAsync(
		string aggregateId,
		string aggregateType,
		IPartitionKey partitionKey,
		long fromVersion,
		IConsistencyOptions? consistencyOptions,
		CancellationToken cancellationToken);

	/// <summary>
	/// Appends events to the store with optimistic concurrency control.
	/// </summary>
	/// <param name="aggregateId">The aggregate identifier.</param>
	/// <param name="aggregateType">The aggregate type name.</param>
	/// <param name="partitionKey">The partition key for the aggregate.</param>
	/// <param name="events">The events to append.</param>
	/// <param name="expectedVersion">The expected current version (-1 for new aggregate).</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>The result of the append operation with cost information.</returns>
	Task<CloudAppendResult> AppendAsync(
		string aggregateId,
		string aggregateType,
		IPartitionKey partitionKey,
		IEnumerable<IDomainEvent> events,
		long expectedVersion,
		CancellationToken cancellationToken);

	/// <summary>
	/// Creates a subscription to the event store change feed.
	/// </summary>
	/// <param name="options">Change feed options.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>A subscription that streams stored events.</returns>
	Task<IChangeFeedSubscription<CloudStoredEvent>> SubscribeToChangesAsync(
		IChangeFeedOptions? options,
		CancellationToken cancellationToken);

	/// <summary>
	/// Gets the current aggregate version without loading events.
	/// </summary>
	/// <param name="aggregateId">The aggregate identifier.</param>
	/// <param name="aggregateType">The aggregate type name.</param>
	/// <param name="partitionKey">The partition key containing the aggregate.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>The current version, or -1 if the aggregate doesn't exist.</returns>
	Task<long> GetCurrentVersionAsync(
		string aggregateId,
		string aggregateType,
		IPartitionKey partitionKey,
		CancellationToken cancellationToken);
}

/// <summary>
/// Represents a stored event in a cloud-native event store.
/// </summary>
/// <remarks>
/// Extends the base stored event with cloud-native metadata.
/// </remarks>
public sealed record CloudStoredEvent
{
	/// <summary>
	/// Gets the unique event identifier.
	/// </summary>
	public required string EventId { get; init; }

	/// <summary>
	/// Gets the aggregate identifier.
	/// </summary>
	public required string AggregateId { get; init; }

	/// <summary>
	/// Gets the aggregate type name.
	/// </summary>
	public required string AggregateType { get; init; }

	/// <summary>
	/// Gets the event type name.
	/// </summary>
	public required string EventType { get; init; }

	/// <summary>
	/// Gets the serialized event data.
	/// </summary>
	public required byte[] EventData { get; init; }

	/// <summary>
	/// Gets the serialized event metadata.
	/// </summary>
	public byte[]? Metadata { get; init; }

	/// <summary>
	/// Gets the event version within the aggregate.
	/// </summary>
	public required long Version { get; init; }

	/// <summary>
	/// Gets when the event occurred.
	/// </summary>
	public required DateTimeOffset Timestamp { get; init; }

	/// <summary>
	/// Gets the partition key for the event.
	/// </summary>
	public required string PartitionKeyValue { get; init; }

	/// <summary>
	/// Gets the ETag for optimistic concurrency.
	/// </summary>
	public string? ETag { get; init; }

	/// <summary>
	/// Gets the document ID in the store.
	/// </summary>
	public string? DocumentId { get; init; }

	/// <summary>
	/// Gets a value indicating whether the event has been dispatched via outbox.
	/// </summary>
	public bool IsDispatched { get; init; }
}

/// <summary>
/// Represents the result of loading events from a cloud-native event store.
/// </summary>
public sealed class CloudEventLoadResult
{
	/// <summary>
	/// Initializes a new instance of the <see cref="CloudEventLoadResult"/> class.
	/// </summary>
	public CloudEventLoadResult(
		IReadOnlyList<CloudStoredEvent> events,
		double requestCharge,
		string? sessionToken = null)
	{
		Events = events;
		RequestCharge = requestCharge;
		SessionToken = sessionToken;
	}

	/// <summary>
	/// Gets the loaded events in version order.
	/// </summary>
	public IReadOnlyList<CloudStoredEvent> Events { get; }

	/// <summary>
	/// Gets the request charge (RUs for Cosmos DB, RCUs for DynamoDB).
	/// </summary>
	public double RequestCharge { get; }

	/// <summary>
	/// Gets the session token for session consistency.
	/// </summary>
	public string? SessionToken { get; }

	/// <summary>
	/// Gets the current version (version of the last event, or -1 if empty).
	/// </summary>
	public long CurrentVersion => Events.Count > 0 ? Events[^1].Version : -1;
}

/// <summary>
/// Represents the result of appending events to a cloud-native event store.
/// </summary>
public sealed class CloudAppendResult
{
	private readonly bool _isConcurrencyConflict;

	private CloudAppendResult(
		bool success,
		long nextExpectedVersion,
		double requestCharge,
		string? sessionToken = null,
		string? errorMessage = null,
		bool isConcurrencyConflict = false)
	{
		Success = success;
		NextExpectedVersion = nextExpectedVersion;
		RequestCharge = requestCharge;
		SessionToken = sessionToken;
		ErrorMessage = errorMessage;
		_isConcurrencyConflict = isConcurrencyConflict;
	}

	/// <summary>
	/// Gets a value indicating whether the append operation succeeded.
	/// </summary>
	public bool Success { get; }

	/// <summary>
	/// Gets the next expected version for the aggregate after this append.
	/// </summary>
	public long NextExpectedVersion { get; }

	/// <summary>
	/// Gets the request charge (RUs for Cosmos DB, WCUs for DynamoDB).
	/// </summary>
	public double RequestCharge { get; }

	/// <summary>
	/// Gets the session token for session consistency.
	/// </summary>
	public string? SessionToken { get; }

	/// <summary>
	/// Gets the error message if the operation failed.
	/// </summary>
	public string? ErrorMessage { get; }

	/// <summary>
	/// Gets a value indicating whether the failure was due to a concurrency conflict.
	/// </summary>
	public bool IsConcurrencyConflict => _isConcurrencyConflict;

	/// <summary>
	/// Creates a successful append result.
	/// </summary>
	/// <param name="nextExpectedVersion">The next expected version.</param>
	/// <param name="requestCharge">The request charge consumed.</param>
	/// <param name="sessionToken">The session token for consistency.</param>
	/// <returns>A successful append result.</returns>
	public static CloudAppendResult CreateSuccess(
		long nextExpectedVersion,
		double requestCharge,
		string? sessionToken = null) =>
		new(success: true, nextExpectedVersion, requestCharge, sessionToken);

	/// <summary>
	/// Creates a failed append result due to version mismatch.
	/// </summary>
	/// <param name="expectedVersion">The expected version.</param>
	/// <param name="actualVersion">The actual version.</param>
	/// <param name="requestCharge">The request charge consumed.</param>
	/// <returns>A failed append result indicating concurrency conflict.</returns>
	public static CloudAppendResult CreateConcurrencyConflict(
		long expectedVersion,
		long actualVersion,
		double requestCharge) =>
		new(
			success: false,
			actualVersion,
			requestCharge,
			errorMessage: $"Concurrency conflict: expected version {expectedVersion} but current version is {actualVersion}",
			isConcurrencyConflict: true);

	/// <summary>
	/// Creates a failed append result with custom error.
	/// </summary>
	/// <param name="errorMessage">The error message.</param>
	/// <param name="requestCharge">The request charge consumed.</param>
	/// <returns>A failed append result.</returns>
	public static CloudAppendResult CreateFailure(string errorMessage, double requestCharge) =>
		new(success: false, -1, requestCharge, errorMessage: errorMessage);
}
