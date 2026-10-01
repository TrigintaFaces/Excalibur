// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0


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
	/// <remarks>
	/// The default implementation answers for any capability this instance itself implements. A leaf store
	/// need not override it. A store whose capability is conditional on its configuration -- a change feed
	/// that needs a stream client, for example -- overrides it to answer null while that condition is
	/// unmet, and a decorator overrides it to defer unknown capabilities to the store it wraps.
	/// </remarks>
	/// <exception cref="ArgumentNullException"> Thrown when <paramref name="serviceType"/> is null. </exception>
	object? GetService(Type serviceType)
	{
		ArgumentNullException.ThrowIfNull(serviceType);

		return serviceType.IsInstanceOfType(this) ? this : null;
	}
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
	private CloudAppendResult(
		CloudAppendOutcome outcome,
		long? nextExpectedVersion,
		double requestCharge,
		string? sessionToken = null,
		string? errorMessage = null,
		MessageFailureKind? failureKind = null)
	{
		Outcome = outcome;
		NextExpectedVersion = nextExpectedVersion;
		RequestCharge = requestCharge;
		SessionToken = sessionToken;
		ErrorMessage = errorMessage;
		FailureKind = failureKind;
	}

	/// <summary>
	/// Gets what the append actually did.
	/// </summary>
	/// <remarks>
	/// <see cref="Success"/> and <see cref="IsConcurrencyConflict"/> are derived from this, so a caller
	/// that only asks "did it work" needs no change. Read this when the difference between an append
	/// written by <em>this</em> call and one recognised as already durable matters — see
	/// <see cref="CloudAppendOutcome.AlreadyCommitted"/>.
	/// </remarks>
	/// <value>The single outcome that holds for this result.</value>
	public CloudAppendOutcome Outcome { get; }

	/// <summary>
	/// Gets a value indicating whether the append operation succeeded.
	/// </summary>
	/// <remarks>
	/// True for <see cref="CloudAppendOutcome.Committed"/> and for
	/// <see cref="CloudAppendOutcome.AlreadyCommitted"/> alike: in both the events the caller asked to
	/// append are durable, which is what this property has always meant.
	/// </remarks>
	public bool Success => Outcome is CloudAppendOutcome.Committed or CloudAppendOutcome.AlreadyCommitted;

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
	/// A concurrency conflict is the one failure that <em>can</em> state a version, and it states one only
	/// when it measured one: where the store read the stream's actual version in order to detect the
	/// conflict it reports that measured value here — including a genuine <c>-1</c> when the conflict is
	/// that the stream does not exist at all. Where the store detected the conflict by another route and
	/// the version read did not succeed, it reports <see langword="null"/>.
	/// </para>
	/// <para>
	/// Every value this property carries is one the store <em>measured</em>. It is never the caller's own
	/// expected version echoed back, never a bound, and never derived from anything but a read. That is
	/// what makes the two meanings of <c>-1</c> separable: a <c>-1</c> here is always "the stream
	/// measurably does not exist", and "not measured" is <see langword="null"/> instead. A caller may pass
	/// any non-null value straight back as the next expected version; on <see langword="null"/> it must
	/// reload.
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
	public bool IsConcurrencyConflict => Outcome is CloudAppendOutcome.ConcurrencyConflict;

	/// <summary>
	/// Gets the failure's classification (transient vs. permanent vs. poison), or <see langword="null"/>
	/// when this result is not a <see cref="CreateFailure"/> outcome (success, or a concurrency conflict,
	/// which is classified by <see cref="IsConcurrencyConflict"/> instead).
	/// </summary>
	/// <remarks>
	/// Required on every <see cref="CreateFailure"/> call so an unclassified failure is a compile error,
	/// not a silently-defaulted retry-forever or a silently-discarded piece of recoverable work — the
	/// resilience pipeline consuming this result needs to know which one it is looking at.
	/// </remarks>
	public MessageFailureKind? FailureKind { get; }


	/// <summary>
	/// Creates a successful append result for events this call wrote.
	/// </summary>
	/// <param name="nextExpectedVersion">The next expected version.</param>
	/// <param name="requestCharge">The request charge consumed.</param>
	/// <param name="sessionToken">The session token for consistency.</param>
	/// <returns>A successful append result.</returns>
	public static CloudAppendResult CreateSuccess(
		long nextExpectedVersion,
		double requestCharge,
		string? sessionToken = null) =>
		new(CloudAppendOutcome.Committed, nextExpectedVersion, requestCharge, sessionToken);

	/// <summary>
	/// Creates a successful append result for events that were <em>already</em> durably present, which the
	/// store recognised as its own by identity rather than writing again.
	/// </summary>
	/// <param name="nextExpectedVersion">The version the recognised events reached.</param>
	/// <param name="requestCharge">The request charge consumed.</param>
	/// <param name="sessionToken">The session token for consistency.</param>
	/// <returns>A successful append result carrying <see cref="CloudAppendOutcome.AlreadyCommitted"/>.</returns>
	/// <remarks>
	/// Every committed-append identity probe reports through here rather than through
	/// <see cref="CreateSuccess"/>, so the distinction survives the hop to the provider-neutral append
	/// result the repository reads.
	/// </remarks>
	public static CloudAppendResult CreateAlreadyCommitted(
		long nextExpectedVersion,
		double requestCharge,
		string? sessionToken = null) =>
		new(CloudAppendOutcome.AlreadyCommitted, nextExpectedVersion, requestCharge, sessionToken);

	/// <summary>
	/// Creates a failed append result due to version mismatch.
	/// </summary>
	/// <param name="expectedVersion">The version the append required the stream to be at.</param>
	/// <param name="actualVersion">
	/// The stream's current version <em>as the store read it</em>, or <see langword="null"/> when the store
	/// detected the conflict without being able to read one.
	/// </param>
	/// <param name="requestCharge">The request charge consumed.</param>
	/// <returns>A failed append result indicating concurrency conflict.</returns>
	/// <remarks>
	/// <para>
	/// <paramref name="actualVersion"/> takes only what a read returned. Pass the read's own result
	/// straight through — never <paramref name="expectedVersion"/>, never a bound, never a value derived
	/// from anything but a read. The parameter is <see cref="long"/>? precisely so that a store which did
	/// not measure has no way to <em>type</em> a number here: the violation is inexpressible rather than
	/// merely discouraged.
	/// </para>
	/// <para>
	/// A proven <em>bound</em> is not a measurement and does not belong here either, however soundly it was
	/// derived. This value is the version the stream is now at, and the value the next append must pass; a
	/// lower bound sent back in its place makes the caller reload to a point that may still be behind the
	/// tail, conflict again, and loop. Report <see langword="null"/> and let the caller reload.
	/// </para>
	/// <para>
	/// When <paramref name="actualVersion"/> is <see langword="null"/> the message states that the version
	/// could not be determined rather than naming one, so the string a consumer reads never claims a
	/// measurement that was not taken.
	/// </para>
	/// </remarks>
	public static CloudAppendResult CreateConcurrencyConflict(
		long expectedVersion,
		long? actualVersion,
		double requestCharge) =>
		new(
			CloudAppendOutcome.ConcurrencyConflict,
			actualVersion,
			requestCharge,
			errorMessage: actualVersion is { } measured
				? $"Concurrency conflict: expected version {expectedVersion} but current version is {measured}"
				: $"Concurrency conflict: expected version {expectedVersion}; the store could not determine "
					+ "the stream's current version");

	/// <summary>
	/// Creates a failed append result with custom error.
	/// </summary>
	/// <param name="errorMessage">The error message.</param>
	/// <param name="requestCharge">The request charge consumed.</param>
	/// <param name="failureKind">
	/// The failure's classification (transient vs. permanent vs. poison), from
	/// <see cref="IMessageFailureClassifier.Classify"/> (narrowed first against the provider's own
	/// known-transient signals, e.g. Cosmos 429, DynamoDB ProvisionedThroughputExceeded, Firestore
	/// RESOURCE_EXHAUSTED, since the shared classifier does not itself recognise provider-specific SDK
	/// exceptions). Required, not optional: an unclassified failure must be a compile error, not a
	/// silently-invented default — defaulting to transient invents infinite retry, defaulting to fatal
	/// discards recoverable work.
	/// </param>
	/// <returns>A failed append result, reporting no version.</returns>
	/// <remarks>
	/// Nothing was appended, so the result states no version: <see cref="NextExpectedVersion"/> is
	/// <see langword="null"/>. Use <see cref="CreateConcurrencyConflict"/> for the one failure that has a
	/// version to report.
	/// </remarks>
	public static CloudAppendResult CreateFailure(string errorMessage, double requestCharge, MessageFailureKind failureKind) =>
		new(CloudAppendOutcome.Failed, nextExpectedVersion: null, requestCharge, errorMessage: errorMessage, failureKind: failureKind);
}

/// <summary>
/// What a cloud-native append actually did, as a single named outcome rather than a combination of flags.
/// </summary>
/// <remarks>
/// The provider-neutral twin of this enumeration lives beside the event-sourcing append result. It is
/// restated here rather than shared because the dependency runs the other way — the event-sourcing
/// abstractions reference this assembly, not the reverse — and a four-member value type is cheaper to
/// restate than a namespace every consumer of the core append result would have to import.
/// </remarks>
public enum CloudAppendOutcome
{
	/// <summary>
	/// This call wrote the events and the store acknowledged the write.
	/// </summary>
	Committed = 0,

	/// <summary>
	/// The events were already durably present when this call ran, and the store recognised them as its
	/// own by identity rather than writing them a second time.
	/// </summary>
	/// <remarks>
	/// This is a success — the append is durable — but it is distinct from <see cref="Committed"/>: the
	/// rows were written by an earlier call whose acknowledgement was lost, and <em>present</em> does not
	/// imply <em>retrievable</em>. A caller still holding the live payloads must not assume they are what
	/// the store would now return.
	/// </remarks>
	AlreadyCommitted = 1,

	/// <summary>
	/// The append lost its version precondition to another writer. Nothing was written.
	/// </summary>
	ConcurrencyConflict = 2,

	/// <summary>
	/// The append failed for its own reasons. Nothing was written.
	/// </summary>
	Failed = 3
}
