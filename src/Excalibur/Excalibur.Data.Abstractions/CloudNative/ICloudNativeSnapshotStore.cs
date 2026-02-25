// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Data.Abstractions.CloudNative;

/// <summary>
/// Defines snapshot operations optimized for cloud-native document databases.
/// </summary>
/// <remarks>
/// <para>
/// This interface provides snapshot storage with cloud-native optimizations:
/// <list type="bullet">
/// <item>Partition-aware snapshot storage</item>
/// <item>Point-in-time reads for historical snapshots</item>
/// <item>Cost-optimized queries (single document reads)</item>
/// <item>TTL-based automatic cleanup</item>
/// </list>
/// </para>
/// <para>
/// <strong>Document Model:</strong>
/// <code>
/// Document ID: {aggregateId}:snapshot
/// Partition Key: {tenantId} or {aggregateId}
/// </code>
/// </para>
/// <para>
/// <strong>Retention Strategy:</strong>
/// Snapshots can be configured with TTL for automatic cleanup,
/// or managed explicitly via <see cref="DeleteOldSnapshotsAsync"/>.
/// </para>
/// </remarks>
public interface ICloudNativeSnapshotStore
{
	/// <summary>
	/// Gets the underlying cloud provider type.
	/// </summary>
	CloudProviderType ProviderType { get; }

	/// <summary>
	/// Gets the latest snapshot for an aggregate.
	/// </summary>
	/// <param name="aggregateId">The aggregate identifier.</param>
	/// <param name="aggregateType">The aggregate type name.</param>
	/// <param name="partitionKey">The partition key containing the aggregate.</param>
	/// <param name="consistencyOptions">Consistency options for the read.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>The latest snapshot with cost information, or null if none exists.</returns>
	Task<CloudSnapshotResult?> GetLatestSnapshotAsync(
		string aggregateId,
		string aggregateType,
		IPartitionKey partitionKey,
		IConsistencyOptions? consistencyOptions,
		CancellationToken cancellationToken);

	/// <summary>
	/// Gets a snapshot at a specific version.
	/// </summary>
	/// <param name="aggregateId">The aggregate identifier.</param>
	/// <param name="aggregateType">The aggregate type name.</param>
	/// <param name="partitionKey">The partition key containing the aggregate.</param>
	/// <param name="version">The specific version to retrieve.</param>
	/// <param name="consistencyOptions">Consistency options for the read.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>The snapshot at the version, or null if not found.</returns>
	Task<CloudSnapshotResult?> GetSnapshotAtVersionAsync(
		string aggregateId,
		string aggregateType,
		IPartitionKey partitionKey,
		long version,
		IConsistencyOptions? consistencyOptions,
		CancellationToken cancellationToken);

	/// <summary>
	/// Saves a snapshot for an aggregate.
	/// </summary>
	/// <param name="snapshot">The snapshot to save.</param>
	/// <param name="partitionKey">The partition key for the aggregate.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>The operation result with cost information.</returns>
	Task<CloudOperationResult<CloudSnapshot>> SaveSnapshotAsync(
		CloudSnapshot snapshot,
		IPartitionKey partitionKey,
		CancellationToken cancellationToken);

	/// <summary>
	/// Deletes all snapshots for an aggregate.
	/// </summary>
	/// <param name="aggregateId">The aggregate identifier.</param>
	/// <param name="aggregateType">The aggregate type name.</param>
	/// <param name="partitionKey">The partition key containing the aggregate.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>The cleanup result with cost information.</returns>
	Task<CloudCleanupResult> DeleteSnapshotsAsync(
		string aggregateId,
		string aggregateType,
		IPartitionKey partitionKey,
		CancellationToken cancellationToken);

	/// <summary>
	/// Deletes snapshots older than a specified version.
	/// </summary>
	/// <param name="aggregateId">The aggregate identifier.</param>
	/// <param name="aggregateType">The aggregate type name.</param>
	/// <param name="partitionKey">The partition key containing the aggregate.</param>
	/// <param name="olderThanVersion">Delete snapshots with version less than this value.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>The cleanup result with cost information.</returns>
	Task<CloudCleanupResult> DeleteOldSnapshotsAsync(
		string aggregateId,
		string aggregateType,
		IPartitionKey partitionKey,
		long olderThanVersion,
		CancellationToken cancellationToken);
}

/// <summary>
/// Represents a snapshot in a cloud-native database.
/// </summary>
public sealed record CloudSnapshot
{
	/// <summary>
	/// Gets the aggregate identifier.
	/// </summary>
	public required string AggregateId { get; init; }

	/// <summary>
	/// Gets the aggregate type name.
	/// </summary>
	public required string AggregateType { get; init; }

	/// <summary>
	/// Gets the snapshot version (matches the aggregate version at snapshot time).
	/// </summary>
	public required long Version { get; init; }

	/// <summary>
	/// Gets the serialized snapshot state.
	/// </summary>
	public required byte[] State { get; init; }

	/// <summary>
	/// Gets the snapshot metadata.
	/// </summary>
	public IDictionary<string, string>? Metadata { get; init; }

	/// <summary>
	/// Gets when the snapshot was created.
	/// </summary>
	public required DateTimeOffset CreatedAt { get; init; }

	/// <summary>
	/// Gets the partition key value for the snapshot.
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
	/// Gets the TTL in seconds for automatic cleanup (null for no expiration).
	/// </summary>
	public int? TimeToLiveSeconds { get; init; }
}

/// <summary>
/// Represents the result of retrieving a snapshot.
/// </summary>
public sealed class CloudSnapshotResult
{
	/// <summary>
	/// Initializes a new instance of the <see cref="CloudSnapshotResult"/> class.
	/// </summary>
	public CloudSnapshotResult(CloudSnapshot snapshot, double requestCharge, string? sessionToken = null)
	{
		Snapshot = snapshot;
		RequestCharge = requestCharge;
		SessionToken = sessionToken;
	}

	/// <summary>
	/// Gets the retrieved snapshot.
	/// </summary>
	public CloudSnapshot Snapshot { get; }

	/// <summary>
	/// Gets the request charge (RUs for Cosmos DB, RCUs for DynamoDB).
	/// </summary>
	public double RequestCharge { get; }

	/// <summary>
	/// Gets the session token for session consistency.
	/// </summary>
	public string? SessionToken { get; }
}
