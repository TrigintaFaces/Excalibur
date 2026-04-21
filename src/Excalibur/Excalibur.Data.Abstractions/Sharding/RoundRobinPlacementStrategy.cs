// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Data.Abstractions.Sharding;

/// <summary>
/// Places new tenants across shards in round-robin order.
/// </summary>
/// <remarks>
/// <para>
/// Uses <see cref="Interlocked.Increment(ref int)"/> for thread-safe counter progression.
/// Simple and fair distribution when shard capacity is uniform.
/// </para>
/// </remarks>
internal sealed class RoundRobinPlacementStrategy : ITenantPlacementStrategy
{
	private int _counter;

	/// <inheritdoc />
	public string SelectShard(string tenantId, IReadOnlyCollection<string> availableShardIds)
	{
		ArgumentNullException.ThrowIfNull(tenantId);
		ArgumentNullException.ThrowIfNull(availableShardIds);

		if (availableShardIds.Count == 0)
		{
			throw new InvalidOperationException("No shards available for tenant placement.");
		}

		// Convert to array for O(1) indexing instead of O(N) enumeration
		var shards = availableShardIds as IReadOnlyList<string> ?? [.. availableShardIds];
		var index = (uint)Interlocked.Increment(ref _counter) % (uint)shards.Count;

		return shards[(int)index];
	}
}
