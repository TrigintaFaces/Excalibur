// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.EventSourcing.Abstractions;

using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Excalibur.EventSourcing.Health;

/// <summary>
/// Health check that verifies the <see cref="ISnapshotStore"/> is reachable.
/// </summary>
/// <remarks>
/// Performs a lightweight probe by attempting to retrieve a snapshot for a non-existent
/// aggregate. A successful null response proves connectivity and schema access.
/// </remarks>
public sealed class SnapshotStoreHealthCheck : IHealthCheck
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
		catch (Exception ex)
		{
			return HealthCheckResult.Unhealthy(
				"Snapshot store is unreachable.",
				ex);
		}
	}
}
