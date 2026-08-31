// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Dispatch;

namespace Excalibur.Data.CloudNative;

/// <summary>
/// Defines core event store operations optimized for cloud-native document databases.
/// </summary>
/// <remarks>
/// <para>
/// This interface provides partition-aware event storage and retrieval with
/// optimistic concurrency via document versioning/ETags and cost tracking.
/// </para>
/// <para>
/// Advanced features are available as ISP sub-interfaces via <see cref="GetService"/>:
/// <list type="bullet">
/// <item><see cref="ICloudNativeProviderInfo"/> -- cloud provider type metadata</item>
/// <item><see cref="ICloudNativeEventStoreChangeFeed"/> -- change feed subscriptions</item>
/// <item><see cref="ICloudNativeEventStoreInfo"/> -- version queries without event loading</item>
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
/// <para>
/// <strong>Tenancy:</strong>
/// Events written through this contract belong to a tenant -- the document model above composes the
/// owning tenant into the document id -- so it declares <see cref="TenantOwnedAttribute"/>. A store
/// registered under this contract in a multi-tenant deployment must present a tenant capability or be
/// refused at registration. The applicable one is the ambient-scoping capability: every read here is
/// addressed by aggregate and partition key with no tenant argument, so confinement can only come from
/// the store applying the ambient tenant. A store that composes its keys without the tenant returns
/// another tenant's events for the same aggregate id, so the refusal is the correct outcome for it
/// rather than a limitation to work around.
/// </para>
/// </remarks>
[TenantOwned]
public interface ICloudNativeEventStore
{
	/// <summary>
	/// Loads all events for an aggregate within a partition.
	/// </summary>
	/// <param name="aggregateId">The aggregate identifier. Must not be <see langword="null"/>, empty, or white space.</param>
	/// <param name="aggregateType">The aggregate type name. Must not be <see langword="null"/>, empty, or white space.</param>
	/// <param name="partitionKey">The partition key containing the aggregate.</param>
	/// <param name="consistencyOptions">Consistency options for the read.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>The events for the aggregate in version order, with cost information.</returns>
	/// <exception cref="System.ArgumentException">
	/// <paramref name="aggregateId"/> or <paramref name="aggregateType"/> is empty or white space.
	/// </exception>
	/// <exception cref="System.ArgumentNullException">
	/// <paramref name="aggregateId"/>, <paramref name="aggregateType"/>, or <paramref name="partitionKey"/> is
	/// <see langword="null"/>.
	/// </exception>
	Task<CloudEventLoadResult> LoadAsync(
		string aggregateId,
		string aggregateType,
		IPartitionKey partitionKey,
		IConsistencyOptions? consistencyOptions,
		CancellationToken cancellationToken);

	/// <summary>
	/// Loads events for an aggregate from a specific version.
	/// </summary>
	/// <param name="aggregateId">The aggregate identifier. Must not be <see langword="null"/>, empty, or white space.</param>
	/// <param name="aggregateType">The aggregate type name. Must not be <see langword="null"/>, empty, or white space.</param>
	/// <param name="partitionKey">The partition key containing the aggregate.</param>
	/// <param name="fromVersion">The version to start loading from (exclusive).</param>
	/// <param name="consistencyOptions">Consistency options for the read.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>The events from the specified version, with cost information.</returns>
	/// <exception cref="System.ArgumentException">
	/// <paramref name="aggregateId"/> or <paramref name="aggregateType"/> is empty or white space.
	/// </exception>
	/// <exception cref="System.ArgumentNullException">
	/// <paramref name="aggregateId"/>, <paramref name="aggregateType"/>, or <paramref name="partitionKey"/> is
	/// <see langword="null"/>.
	/// </exception>
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
	/// <param name="aggregateId">The aggregate identifier. Must not be <see langword="null"/>, empty, or white space.</param>
	/// <param name="aggregateType">The aggregate type name. Must not be <see langword="null"/>, empty, or white space.</param>
	/// <param name="partitionKey">The partition key for the aggregate.</param>
	/// <param name="events">The events to append.</param>
	/// <param name="expectedVersion">The expected current version (-1 for new aggregate).</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>The result of the append operation with cost information.</returns>
	/// <remarks>
	/// An identifier that is <see langword="null"/>, empty, or white space is a usage error, never a legitimate
	/// stream: accepting one would fabricate a stream or partition key and write events where no reader will
	/// ever look. Every implementation rejects such an argument by throwing, before any request reaches the
	/// service. A returned <see cref="CloudAppendResult"/> models a domain or infrastructure outcome — a
	/// concurrency conflict, a throttled request — never a caller defect.
	/// </remarks>
	/// <exception cref="System.ArgumentException">
	/// <paramref name="aggregateId"/> or <paramref name="aggregateType"/> is empty or white space.
	/// </exception>
	/// <exception cref="System.ArgumentNullException">
	/// <paramref name="aggregateId"/>, <paramref name="aggregateType"/>, <paramref name="partitionKey"/>, or
	/// <paramref name="events"/> is <see langword="null"/>.
	/// </exception>
	Task<CloudAppendResult> AppendAsync(
		string aggregateId,
		string aggregateType,
		IPartitionKey partitionKey,
		IEnumerable<IDomainEvent> events,
		long expectedVersion,
		CancellationToken cancellationToken);

	/// <summary>
	/// Gets an implementation-specific service. Use to access optional capabilities
	/// such as <see cref="ICloudNativeProviderInfo"/>, <see cref="ICloudNativeEventStoreChangeFeed"/>,
	/// or <see cref="ICloudNativeEventStoreInfo"/>.
	/// </summary>
	/// <param name="serviceType">The type of the requested service.</param>
	/// <returns>The service instance, or <see langword="null"/> if not supported.</returns>
	object? GetService(Type serviceType) => null;
}

/// <summary>
/// Provides change feed subscription capabilities for cloud-native event stores.
/// Obtain via <see cref="ICloudNativeEventStore.GetService"/> on an event store instance.
/// </summary>
public interface ICloudNativeEventStoreChangeFeed
{
	/// <summary>
	/// Creates a subscription to the event store change feed.
	/// </summary>
	/// <param name="options">Change feed options.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>A subscription that streams stored events.</returns>
	Task<IChangeFeedSubscription<CloudStoredEvent>> SubscribeToChangesAsync(
		IChangeFeedOptions? options,
		CancellationToken cancellationToken);
}

/// <summary>
/// Provides version query capabilities for cloud-native event stores without
/// loading events. Obtain via <see cref="ICloudNativeEventStore.GetService"/>
/// on an event store instance.
/// </summary>
public interface ICloudNativeEventStoreInfo
{
	/// <summary>
	/// Gets the current aggregate version without loading events.
	/// </summary>
	/// <param name="aggregateId">The aggregate identifier. Must not be <see langword="null"/>, empty, or white space.</param>
	/// <param name="aggregateType">The aggregate type name. Must not be <see langword="null"/>, empty, or white space.</param>
	/// <param name="partitionKey">The partition key containing the aggregate.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>The current version, or -1 if the aggregate doesn't exist.</returns>
	/// <exception cref="System.ArgumentException">
	/// <paramref name="aggregateId"/> or <paramref name="aggregateType"/> is empty or white space.
	/// </exception>
	/// <exception cref="System.ArgumentNullException">
	/// <paramref name="aggregateId"/>, <paramref name="aggregateType"/>, or <paramref name="partitionKey"/> is
	/// <see langword="null"/>.
	/// </exception>
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
		long? nextExpectedVersion,
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
	/// Gets the next expected version for the aggregate after this append — or <see langword="null"/>
	/// when this result cannot state one.
	/// </summary>
	/// <remarks>
	/// <para>
	/// A failed append reports <see langword="null"/> rather than a number, because it has no version to
	/// report and <c>-1</c> is not free to borrow as a sentinel: under this store's version base <c>-1</c>
	/// is the ordinary value meaning <em>this stream does not exist</em>. Reporting it after a failure
	/// would hand a caller a number asserting the opposite of the truth, which they could pass straight
	/// back as an expected version and create a stream that already holds events.
	/// </para>
	/// <para>
	/// A concurrency conflict is the one failure that <em>can</em> state a version: the store read the
	/// stream's actual version in order to detect the conflict, so it reports that measured value here —
	/// including a genuine <c>-1</c> when the conflict is that the stream does not exist at all.
	/// </para>
	/// </remarks>
	/// <value>The stream's current version after the append, or <see langword="null"/> when unavailable.</value>
	public long? NextExpectedVersion { get; }

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
	/// <returns>A failed append result, reporting no version.</returns>
	/// <remarks>
	/// Nothing was appended, so the result states no version: <see cref="NextExpectedVersion"/> is
	/// <see langword="null"/>. Use <see cref="CreateConcurrencyConflict"/> for the one failure that has a
	/// version to report.
	/// </remarks>
	public static CloudAppendResult CreateFailure(string errorMessage, double requestCharge) =>
		new(success: false, nextExpectedVersion: null, requestCharge, errorMessage: errorMessage);
}
