// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

namespace Excalibur.EventSourcing.Sharding;

/// <summary>Serves a bounded native read against an exact registered physical event-store shard.</summary>
/// <remarks>
/// Implement this on the same resolver that serves tenant operations, using the same cached physical
/// binding. No tenant ID, ambient tenant, primary-store fallback or automatic provisioning is used.
/// Relational readers bind their event read columns; an empty schemaless namespace establishes read
/// access only, not event-field integrity or write permissions. Failures propagate to the health check.
/// </remarks>
public interface ITenantShardReadinessProbe
{
	/// <summary>Attempts a native read of at most one record on the named physical shard.</summary>
	/// <param name="shardId">The registered physical shard identifier, never a tenant identifier.</param>
	/// <param name="cancellationToken">Cancellation of the readiness operation.</param>
	/// <returns>A task that completes only after the provider served the read.</returns>
	Task ProbeShardAsync(string shardId, CancellationToken cancellationToken);
}
