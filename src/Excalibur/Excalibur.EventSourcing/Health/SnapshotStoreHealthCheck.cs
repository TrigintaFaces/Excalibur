// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Data.Sharding;
using Excalibur.Dispatch;

using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Excalibur.EventSourcing.Health;

/// <summary>
/// Health check that verifies the snapshot store can actually serve a read — not merely that its database
/// answers a connection.
/// </summary>
/// <remarks>
/// <para>
/// The probe retrieves the latest snapshot for a reserved, never-written aggregate. A relational provider
/// validates a statement's select list at <i>parse</i> time, so this detects a snapshot table that is
/// absent, or present but missing a column the provider's read statement binds, <b>on an empty database
/// with zero rows</b>.
/// </para>
/// <para>
/// <b>What it does NOT establish.</b> The snapshot store emits distinct statements for save, delete and
/// prune, each with its own column list. A green result here means the latest-snapshot read binds; it is
/// not a verdict on the whole snapshot schema. Schema correctness is a deployment concern and belongs at
/// startup, where a mis-provisioned host can refuse to start rather than serve traffic while reporting
/// itself reachable.
/// </para>
/// <para>
/// <b>Status semantics.</b> <see cref="HealthStatus.Unhealthy" /> means the store refused a read it should
/// have served. <see cref="HealthStatus.Degraded" /> means the probe could not be taken at all — a
/// configuration state, not a store fault — reported rather than silently passed, because a check that
/// cannot distinguish "did not run" from "passed" is not a check. Cancellation propagates instead of being
/// reported as a fault, so a host shutting down does not raise a false alarm.
/// </para>
/// </remarks>
internal sealed class SnapshotStoreHealthCheck : IHealthCheck
{
	/// <summary>
	/// Sentinel aggregate ID used for probing. Intentionally non-existent.
	/// </summary>
	internal const string ProbeAggregateId = "__health_probe__";

	/// <summary>
	/// Sentinel aggregate type used for probing.
	/// </summary>
	internal const string ProbeAggregateType = "__health__";

	private readonly ISnapshotStore _snapshotStore;

	/// <summary>
	/// Initializes a new instance of the <see cref="SnapshotStoreHealthCheck"/> class.
	/// </summary>
	/// <param name="snapshotStore">The snapshot store to probe.</param>
	public SnapshotStoreHealthCheck(ISnapshotStore snapshotStore)
	{
		_snapshotStore = snapshotStore ?? throw new ArgumentNullException(nameof(snapshotStore));
	}

	/// <inheritdoc />
	public async Task<HealthCheckResult> CheckHealthAsync(
		HealthCheckContext context,
		CancellationToken cancellationToken)
	{
		try
		{
			var snapshot = await _snapshotStore.GetLatestSnapshotAsync(
				ProbeAggregateId,
				ProbeAggregateType,
				cancellationToken).ConfigureAwait(false);

			return HealthCheckResult.Healthy(
				snapshot is null
					? "Snapshot store is reachable (no probe snapshot found, as expected)."
					: "Snapshot store is reachable.");
		}
		catch (OperationCanceledException)
		{
			// A host shutting down is not an unhealthy store. Let the framework observe the cancellation.
			throw;
		}
		catch (Exception ex) when (ex is TenantRequiredException or TenantShardNotFoundException)
		{
			// Configuration, not health: no tenant is resolvable on a health-check scope. Reporting
			// Unhealthy would mark a working store broken; reporting Healthy would claim a verification
			// that never happened.
			return HealthCheckResult.Degraded(
				"Snapshot store could not be probed because no tenant or shard is resolvable on a "
				+ "health-check scope. This is a configuration state, not a store fault, and nothing about "
				+ $"the store's schema was verified: {ex.Message}",
				exception: ex);
		}
		catch (Exception ex)
		{
			return HealthCheckResult.Unhealthy(
				"Snapshot store refused a read it should have served. Its table may be absent, or present "
				+ $"but missing a column the provider's read statement binds: {ex.Message}",
				exception: ex);
		}
	}
}
