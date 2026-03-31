// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Data.Abstractions.Sharding;

/// <summary>
/// Places new tenants on the shard with the fewest assigned tenants.
/// </summary>
/// <remarks>
/// <para>
/// Tracks tenant-to-shard assignments in memory. Thread-safe via a lock
/// that makes find-min + increment atomic.
/// For production durability, pair with a persistent tenant mapping store
/// that seeds the counts on startup.
/// </para>
/// </remarks>
internal sealed class LeastLoadedPlacementStrategy : ITenantPlacementStrategy
{
	private readonly Dictionary<string, int> _shardCounts = new(StringComparer.OrdinalIgnoreCase);
#pragma warning disable IDE0330 // Use 'System.Threading.Lock' -- object lock required for net8.0 compat
	private readonly object _lock = new();
#pragma warning restore IDE0330

	/// <inheritdoc />
	public string SelectShard(string tenantId, IReadOnlyCollection<string> availableShardIds)
	{
		ArgumentNullException.ThrowIfNull(tenantId);
		ArgumentNullException.ThrowIfNull(availableShardIds);

		if (availableShardIds.Count == 0)
		{
			throw new InvalidOperationException("No shards available for tenant placement.");
		}

		lock (_lock)
		{
			// Ensure all shards have an entry
			foreach (var shardId in availableShardIds)
			{
				_shardCounts.TryAdd(shardId, 0);
			}

			// Find shard with lowest count (atomic under lock)
			string? bestShard = null;
			var bestCount = int.MaxValue;

			foreach (var shardId in availableShardIds)
			{
				var count = _shardCounts[shardId];
				if (count < bestCount)
				{
					bestCount = count;
					bestShard = shardId;
				}
			}

			// Increment atomically with the selection
			_shardCounts[bestShard!]++;

			return bestShard!;
		}
	}
}
