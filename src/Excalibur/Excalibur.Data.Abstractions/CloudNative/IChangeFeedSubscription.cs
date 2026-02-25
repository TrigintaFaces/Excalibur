// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Data.Abstractions.CloudNative;

/// <summary>
/// Defines the types of changes that can occur in a change feed.
/// </summary>
public enum ChangeFeedEventType
{
	/// <summary>
	/// A new document was created.
	/// </summary>
	Created = 0,

	/// <summary>
	/// An existing document was updated.
	/// </summary>
	Updated = 1,

	/// <summary>
	/// A document was deleted.
	/// </summary>
	/// <remarks>
	/// Not all providers support delete detection. Check provider documentation.
	/// </remarks>
	Deleted = 2,

	/// <summary>
	/// A document was replaced (full document update).
	/// </summary>
	Replaced = 3
}

/// <summary>
/// Defines the starting position for a change feed subscription.
/// </summary>
public enum ChangeFeedStartPosition
{
	/// <summary>
	/// Start from the beginning of the change feed history.
	/// </summary>
	Beginning = 0,

	/// <summary>
	/// Start from now (only process new changes).
	/// </summary>
	Now = 1,

	/// <summary>
	/// Resume from a specific continuation token.
	/// </summary>
	FromContinuationToken = 2,

	/// <summary>
	/// Start from a specific timestamp.
	/// </summary>
	FromTimestamp = 3
}

/// <summary>
/// Represents a change in a cloud-native document database.
/// </summary>
/// <typeparam name="TDocument">The type of the document that changed.</typeparam>
public interface IChangeFeedEvent<out TDocument>
{
	/// <summary>
	/// Gets the type of change that occurred.
	/// </summary>
	ChangeFeedEventType EventType { get; }

	/// <summary>
	/// Gets the document after the change (null for deletes).
	/// </summary>
	TDocument? Document { get; }

	/// <summary>
	/// Gets the document ID.
	/// </summary>
	string DocumentId { get; }

	/// <summary>
	/// Gets the partition key of the document.
	/// </summary>
	IPartitionKey PartitionKey { get; }

	/// <summary>
	/// Gets the timestamp when the change occurred.
	/// </summary>
	DateTimeOffset Timestamp { get; }

	/// <summary>
	/// Gets the continuation token for resuming the feed from this point.
	/// </summary>
	string ContinuationToken { get; }

	/// <summary>
	/// Gets the sequence number or version of this change.
	/// </summary>
	long SequenceNumber { get; }
}

/// <summary>
/// Represents a subscription to a cloud-native database change feed.
/// </summary>
/// <remarks>
/// <para>
/// Change feeds provide real-time streaming of document changes for:
/// <list type="bullet">
/// <item>Cosmos DB: Change Feed</item>
/// <item>DynamoDB: DynamoDB Streams</item>
/// <item>Firestore: Real-time listeners / Cloud Functions triggers</item>
/// </list>
/// </para>
/// <para>
/// This abstraction enables event-driven architectures including:
/// <list type="bullet">
/// <item>Event sourcing subscriptions</item>
/// <item>Materialized view updates</item>
/// <item>Outbox pattern processing</item>
/// <item>Real-time notifications</item>
/// </list>
/// </para>
/// </remarks>
/// <typeparam name="TDocument">The type of documents in the feed.</typeparam>
public interface IChangeFeedSubscription<TDocument> : IAsyncDisposable
{
	/// <summary>
	/// Gets the subscription identifier.
	/// </summary>
	string SubscriptionId { get; }

	/// <summary>
	/// Gets a value indicating whether the subscription is active.
	/// </summary>
	bool IsActive { get; }

	/// <summary>
	/// Gets the current continuation token for checkpointing.
	/// </summary>
	string? CurrentContinuationToken { get; }

	/// <summary>
	/// Starts the subscription and begins receiving changes.
	/// </summary>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>A task representing the subscription startup.</returns>
	Task StartAsync(CancellationToken cancellationToken);

	/// <summary>
	/// Stops the subscription gracefully.
	/// </summary>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>A task representing the graceful shutdown.</returns>
	Task StopAsync(CancellationToken cancellationToken);

	/// <summary>
	/// Gets the async enumerable of change events.
	/// </summary>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>An async enumerable of change feed events.</returns>
	IAsyncEnumerable<IChangeFeedEvent<TDocument>> ReadChangesAsync(CancellationToken cancellationToken);
}

/// <summary>
/// Defines options for configuring a change feed subscription.
/// </summary>
public interface IChangeFeedOptions
{
	/// <summary>
	/// Gets the starting position for the subscription.
	/// </summary>
	ChangeFeedStartPosition StartPosition { get; }

	/// <summary>
	/// Gets the continuation token to resume from (if resuming).
	/// </summary>
	string? ContinuationToken { get; }

	/// <summary>
	/// Gets the specific timestamp to start from (if using StartFromTimestamp).
	/// </summary>
	DateTimeOffset? StartTimestamp { get; }

	/// <summary>
	/// Gets the maximum number of items to process in a batch.
	/// </summary>
	int MaxBatchSize { get; }

	/// <summary>
	/// Gets the polling interval for providers that use polling.
	/// </summary>
	TimeSpan PollingInterval { get; }

	/// <summary>
	/// Gets the partition key to filter changes (null for all partitions).
	/// </summary>
	IPartitionKey? PartitionKeyFilter { get; }
}

/// <summary>
/// Default implementation of <see cref="IChangeFeedOptions"/>.
/// </summary>
public sealed class ChangeFeedOptions : IChangeFeedOptions
{
	/// <summary>
	/// Gets the default change feed options (start from now, batch size 100).
	/// </summary>
	public static readonly ChangeFeedOptions Default = new();

	/// <summary>
	/// Gets options to start from the beginning of the feed.
	/// </summary>
	public static readonly ChangeFeedOptions FromBeginning = new() { StartPosition = ChangeFeedStartPosition.Beginning };

	/// <inheritdoc/>
	public ChangeFeedStartPosition StartPosition { get; init; } = ChangeFeedStartPosition.Now;

	/// <inheritdoc/>
	public string? ContinuationToken { get; init; }

	/// <inheritdoc/>
	public DateTimeOffset? StartTimestamp { get; init; }

	/// <inheritdoc/>
	public int MaxBatchSize { get; init; } = 100;

	/// <inheritdoc/>
	public TimeSpan PollingInterval { get; init; } = TimeSpan.FromSeconds(1);

	/// <inheritdoc/>
	public IPartitionKey? PartitionKeyFilter { get; init; }

	/// <summary>
	/// Creates options to resume from a continuation token.
	/// </summary>
	/// <param name="continuationToken">The continuation token to resume from.</param>
	/// <returns>Change feed options configured to resume from the token.</returns>
	public static ChangeFeedOptions FromContinuation(string continuationToken) =>
		new() { StartPosition = ChangeFeedStartPosition.FromContinuationToken, ContinuationToken = continuationToken };

	/// <summary>
	/// Creates options to start from a specific timestamp.
	/// </summary>
	/// <param name="timestamp">The timestamp to start from.</param>
	/// <returns>Change feed options configured to start from the timestamp.</returns>
	public static ChangeFeedOptions FromTimestamp(DateTimeOffset timestamp) =>
		new() { StartPosition = ChangeFeedStartPosition.FromTimestamp, StartTimestamp = timestamp };
}
