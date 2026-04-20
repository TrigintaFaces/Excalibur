// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: Apache-2.0

using Excalibur.Data.Abstractions.Sharding;

namespace MultiTenantEventSourcing;

/// <summary>
/// Static configuration-driven <see cref="ITenantShardMap"/> for the sample.
/// </summary>
/// <remarks>
/// <para>
/// Production deployments typically bind the map from <c>appsettings.json</c>
/// or a central control-plane service. This sample keeps it in-process for
/// clarity.
/// </para>
/// </remarks>
public sealed class SampleTenantShardMap : ITenantShardMap
{
	private readonly IReadOnlyDictionary<string, ShardInfo> _shards;
	private readonly IReadOnlyDictionary<string, ShardInfo> _tenantToShard;

	public SampleTenantShardMap(
		IReadOnlyDictionary<string, ShardInfo> shards,
		IReadOnlyDictionary<string, string> tenantToShardId)
	{
		ArgumentNullException.ThrowIfNull(shards);
		ArgumentNullException.ThrowIfNull(tenantToShardId);

		_shards = shards;

		var map = new Dictionary<string, ShardInfo>(
			tenantToShardId.Count, StringComparer.OrdinalIgnoreCase);

		foreach (var (tenantId, shardId) in tenantToShardId)
		{
			if (!shards.TryGetValue(shardId, out var shard))
			{
				throw new InvalidOperationException(
					$"Tenant '{tenantId}' points at unknown shard '{shardId}'.");
			}

			map[tenantId] = shard;
		}

		_tenantToShard = map;
	}

	/// <inheritdoc />
	public ShardInfo GetShardInfo(string tenantId)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);

		return _tenantToShard.TryGetValue(tenantId, out var shard)
			? shard
			: throw new TenantShardNotFoundException(
				$"No shard mapped for tenant '{tenantId}'.");
	}

	/// <inheritdoc />
	public IReadOnlyCollection<string> GetRegisteredShardIds() => (IReadOnlyCollection<string>)_shards.Keys;
}
