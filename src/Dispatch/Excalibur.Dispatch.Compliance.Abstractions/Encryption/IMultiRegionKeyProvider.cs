// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Compliance;

/// <summary>
/// Extends <see cref="IKeyManagementProvider"/> with multi-region disaster recovery capabilities.
/// </summary>
/// <remarks>
/// <para>
/// Multi-region key management provides:
/// </para>
/// <list type="bullet">
///   <item><description>Active-passive failover between primary and secondary regions</description></item>
///   <item><description>Automatic health monitoring and failover detection</description></item>
///   <item><description>Geographic key replication for disaster recovery</description></item>
///   <item><description>RPO/RTO optimization through configurable sync strategies</description></item>
/// </list>
/// <para>
/// <strong>Architecture Decision:</strong> Active-passive is chosen over active-active to:
/// </para>
/// <list type="number">
///   <item><description>Simplify consistency model (single writer)</description></item>
///   <item><description>Avoid split-brain scenarios</description></item>
///   <item><description>Align with cloud provider capabilities</description></item>
///   <item><description>Maintain clear data residency boundaries</description></item>
/// </list>
/// </remarks>
public interface IMultiRegionKeyProvider : IKeyManagementProvider, IDisposable
{
	/// <summary>
	/// Gets the identifier of the currently active region.
	/// </summary>
	/// <value>
	/// The region identifier (e.g., "westeurope", "us-east-1") of the region
	/// currently handling key management operations.
	/// </value>
	string ActiveRegionId { get; }

	/// <summary>
	/// Gets a value indicating whether the provider is currently operating in failover mode.
	/// </summary>
	/// <value>
	/// <c>true</c> if the secondary region is active due to primary failure;
	/// <c>false</c> if the primary region is active.
	/// </value>
	bool IsInFailoverMode { get; }

	/// <summary>
	/// Gets the health status for the primary region.
	/// </summary>
	/// <param name="cancellationToken">A token to cancel the operation.</param>
	/// <returns>The current health status of the primary region.</returns>
	Task<RegionHealth> GetPrimaryHealthAsync(CancellationToken cancellationToken);

	/// <summary>
	/// Gets the health status for the secondary region.
	/// </summary>
	/// <param name="cancellationToken">A token to cancel the operation.</param>
	/// <returns>The current health status of the secondary region.</returns>
	Task<RegionHealth> GetSecondaryHealthAsync(CancellationToken cancellationToken);

	/// <summary>
	/// Gets the current replication status between regions.
	/// </summary>
	/// <param name="cancellationToken">A token to cancel the operation.</param>
	/// <returns>The replication status including lag and pending keys.</returns>
	/// <remarks>
	/// Use this to monitor whether RPO (Recovery Point Objective) targets are being met.
	/// </remarks>
	Task<ReplicationStatus> GetReplicationStatusAsync(CancellationToken cancellationToken);

	/// <summary>
	/// Manually triggers failover to the secondary region.
	/// </summary>
	/// <param name="reason">The reason for manual failover (for audit purposes).</param>
	/// <param name="cancellationToken">A token to cancel the operation.</param>
	/// <returns>A task representing the asynchronous failover operation.</returns>
	/// <remarks>
	/// <para>
	/// Use this for planned failover during maintenance or when automatic
	/// failover thresholds have not been met but manual intervention is needed.
	/// </para>
	/// <para>
	/// This operation is audited via <c>FailoverInitiatedEvent</c>.
	/// </para>
	/// </remarks>
	/// <exception cref="InvalidOperationException">
	/// Thrown when already in failover mode or secondary is unhealthy.
	/// </exception>
	Task ForceFailoverAsync(string reason, CancellationToken cancellationToken);

	/// <summary>
	/// Returns operations to the primary region after failover.
	/// </summary>
	/// <param name="reason">The reason for failback (for audit purposes).</param>
	/// <param name="cancellationToken">A token to cancel the operation.</param>
	/// <returns>A task representing the asynchronous failback operation.</returns>
	/// <remarks>
	/// <para>
	/// Failback is always manual to prevent split-brain scenarios where
	/// a recovering primary might conflict with the active secondary.
	/// </para>
	/// <para>
	/// Before failback, keys created during failover are synchronized
	/// from secondary to primary.
	/// </para>
	/// <para>
	/// This operation is audited via <c>FailbackCompletedEvent</c>.
	/// </para>
	/// </remarks>
	/// <exception cref="InvalidOperationException">
	/// Thrown when not in failover mode or primary is unhealthy.
	/// </exception>
	Task FailbackToPrimaryAsync(string reason, CancellationToken cancellationToken);

	/// <summary>
	/// Replicates keys from the primary region to the secondary region.
	/// </summary>
	/// <param name="keyId">
	/// Optional specific key to replicate. If null, replicates all keys.
	/// </param>
	/// <param name="cancellationToken">A token to cancel the operation.</param>
	/// <returns>A task representing the asynchronous replication operation.</returns>
	/// <remarks>
	/// <para>
	/// Most cloud providers handle replication automatically. This method
	/// is primarily for manual replication scenarios or after key imports.
	/// </para>
	/// <para>
	/// For providers with automatic replication (Azure KV Premium, AWS KMS MRKs),
	/// this may be a no-op or trigger an immediate sync.
	/// </para>
	/// </remarks>
	Task ReplicateKeysAsync(string? keyId, CancellationToken cancellationToken);
}

/// <summary>
/// Represents the health status of a region.
/// </summary>
/// <param name="RegionId">The identifier of the region.</param>
/// <param name="IsHealthy">Whether the region is responding to health checks.</param>
/// <param name="Latency">The measured latency to the region.</param>
/// <param name="LastChecked">When the last health check was performed.</param>
/// <param name="ConsecutiveFailures">Number of consecutive health check failures.</param>
/// <param name="ErrorMessage">Error message if unhealthy; null otherwise.</param>
/// <param name="Diagnostics">Additional diagnostic information.</param>
public sealed record RegionHealth(
	string RegionId,
	bool IsHealthy,
	TimeSpan Latency,
	DateTimeOffset LastChecked,
	int ConsecutiveFailures,
	string? ErrorMessage = null,
	IReadOnlyDictionary<string, string>? Diagnostics = null);

/// <summary>
/// Represents the replication status between primary and secondary regions.
/// </summary>
/// <param name="ReplicationLag">
/// The time since the last successful replication. Compare against RPO target.
/// </param>
/// <param name="PendingKeys">Number of keys awaiting replication.</param>
/// <param name="LastSuccessfulSync">Timestamp of the last successful synchronization.</param>
/// <param name="SyncInProgress">Whether a sync operation is currently running.</param>
/// <param name="ReplicationMode">The configured replication mode.</param>
public sealed record ReplicationStatus(
	TimeSpan ReplicationLag,
	int PendingKeys,
	DateTimeOffset? LastSuccessfulSync,
	bool SyncInProgress,
	ReplicationMode ReplicationMode);

/// <summary>
/// Specifies the replication mode for multi-region key synchronization.
/// </summary>
public enum ReplicationMode
{
	/// <summary>
	/// Keys are replicated synchronously. Provides zero RPO but higher latency.
	/// </summary>
	Synchronous,

	/// <summary>
	/// Keys are replicated asynchronously at configured intervals. Default mode.
	/// </summary>
	Asynchronous,

	/// <summary>
	/// Keys are only replicated on explicit request via <see cref="IMultiRegionKeyProvider.ReplicateKeysAsync"/>.
	/// </summary>
	Manual
}
