// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Data.Abstractions.Sharding;

/// <summary>
/// Determines which shard a new tenant should be placed in.
/// </summary>
/// <remarks>
/// <para>
/// Placement strategies are consulted when a tenant is encountered for the first time
/// (i.e., not yet mapped in the <see cref="ITenantShardMap"/>). The strategy selects a
/// shard from the available pool based on its algorithm (e.g., least loaded, round-robin).
/// </para>
/// <para>
/// Implementations must be thread-safe -- placement may be called concurrently from
/// multiple requests.
/// </para>
/// </remarks>
public interface ITenantPlacementStrategy
{
	/// <summary>
	/// Selects a shard for the given tenant from the available shard IDs.
	/// </summary>
	/// <param name="tenantId">The tenant identifier being placed.</param>
	/// <param name="availableShardIds">The registered shard IDs to choose from.</param>
	/// <returns>The selected shard ID.</returns>
	/// <exception cref="InvalidOperationException">Thrown when no shards are available.</exception>
	string SelectShard(string tenantId, IReadOnlyCollection<string> availableShardIds);
}
