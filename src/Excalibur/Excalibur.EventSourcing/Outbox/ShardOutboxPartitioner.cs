// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.Abstractions.Sharding;

namespace Excalibur.EventSourcing.Outbox;

/// <summary>
/// Partitions outbox messages by tenant shard. Each shard ID maps to a partition.
/// </summary>
internal sealed class ShardOutboxPartitioner : IOutboxPartitioner
{
	private readonly ITenantShardMap _shardMap;
	private readonly Dictionary<string, int> _shardToPartition;
	private readonly int _partitionCount;

	internal ShardOutboxPartitioner(ITenantShardMap shardMap, IReadOnlyList<string> shardIds)
	{
		ArgumentNullException.ThrowIfNull(shardMap);
		ArgumentNullException.ThrowIfNull(shardIds);

		_shardMap = shardMap;
		_partitionCount = shardIds.Count;
		_shardToPartition = new Dictionary<string, int>(
			shardIds.Count, StringComparer.OrdinalIgnoreCase);

		for (var i = 0; i < shardIds.Count; i++)
		{
			_shardToPartition[shardIds[i]] = i;
		}
	}

	/// <inheritdoc />
	public int GetPartition(string tenantId)
	{
		var shardInfo = _shardMap.GetShardInfo(tenantId);
		return _shardToPartition.TryGetValue(shardInfo.ShardId, out var partition)
			? partition
			: 0;
	}

	/// <inheritdoc />
	public int PartitionCount => _partitionCount;
}
