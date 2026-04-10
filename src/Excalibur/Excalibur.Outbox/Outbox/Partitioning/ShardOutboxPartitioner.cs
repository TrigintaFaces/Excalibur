// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.Abstractions.Sharding;

using Microsoft.Extensions.Logging;

namespace Excalibur.Outbox.Partitioning;

/// <summary>
/// Partitions outbox messages by tenant shard. Each shard ID maps to a partition.
/// </summary>
internal sealed partial class ShardOutboxPartitioner : IOutboxPartitioner
{
	private readonly ITenantShardMap _shardMap;
	private readonly Dictionary<string, int> _shardToPartition;
	private readonly int _partitionCount;
	private readonly ILogger<ShardOutboxPartitioner> _logger;

	internal ShardOutboxPartitioner(ITenantShardMap shardMap, IReadOnlyList<string> shardIds, ILogger<ShardOutboxPartitioner> logger)
	{
		ArgumentNullException.ThrowIfNull(shardMap);
		ArgumentNullException.ThrowIfNull(shardIds);
		ArgumentNullException.ThrowIfNull(logger);

		_shardMap = shardMap;
		_partitionCount = shardIds.Count;
		_logger = logger;
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
		if (_shardToPartition.TryGetValue(shardInfo.ShardId, out var partition))
		{
			return partition;
		}

		LogUnknownShardFallback(shardInfo.ShardId, tenantId);
		return 0;
	}

	[LoggerMessage(Level = LogLevel.Warning,
		Message = "Shard '{ShardId}' for tenant '{TenantId}' is not in the configured shard map. Falling back to partition 0.")]
	private partial void LogUnknownShardFallback(string shardId, string tenantId);

	/// <inheritdoc />
	public int PartitionCount => _partitionCount;
}
