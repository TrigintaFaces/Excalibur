// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.Abstractions.Sharding;

namespace Excalibur.EventSourcing.Sharding;

/// <summary>
/// In-memory dictionary-based implementation of <see cref="ITenantShardMap"/>.
/// </summary>
/// <remarks>
/// <para>
/// Shard resolution is O(1) dictionary lookup -- well under the &lt; 1us target.
/// </para>
/// <para>
/// When <see cref="ShardMapOptions.DefaultShardId"/> is configured, unknown tenants
/// route to the default shard. When null, unknown tenants throw
/// <see cref="TenantShardNotFoundException"/>.
/// </para>
/// </remarks>
internal sealed class InMemoryTenantShardMap : ITenantShardMap
{
	private readonly Dictionary<string, ShardInfo> _tenantToShard;
	private readonly Dictionary<string, ShardInfo> _shards;
	private readonly ShardInfo? _defaultShard;

	internal InMemoryTenantShardMap(
		Dictionary<string, ShardInfo> shards,
		Dictionary<string, string> tenantMappings,
		ShardMapOptions options)
	{
		ArgumentNullException.ThrowIfNull(shards);
		ArgumentNullException.ThrowIfNull(tenantMappings);
		ArgumentNullException.ThrowIfNull(options);

		_shards = shards;

		// Build tenant -> shard lookup
		_tenantToShard = new Dictionary<string, ShardInfo>(
			tenantMappings.Count, StringComparer.OrdinalIgnoreCase);

		foreach (var (tenantId, shardId) in tenantMappings)
		{
			if (!shards.TryGetValue(shardId, out var shardInfo))
			{
				throw new InvalidOperationException(
					$"Tenant '{tenantId}' is mapped to shard '{shardId}' which does not exist.");
			}

			_tenantToShard[tenantId] = shardInfo;
		}

		// Resolve default shard
		if (options.DefaultShardId is not null)
		{
			if (!shards.TryGetValue(options.DefaultShardId, out var defaultShard))
			{
				throw new InvalidOperationException(
					$"Default shard '{options.DefaultShardId}' does not exist in the shard map.");
			}

			_defaultShard = defaultShard;
		}
	}

	/// <inheritdoc />
	public ShardInfo GetShardInfo(string tenantId)
	{
		ArgumentNullException.ThrowIfNull(tenantId);

		if (_tenantToShard.TryGetValue(tenantId, out var shardInfo))
		{
			return shardInfo;
		}

		if (_defaultShard is not null)
		{
			return _defaultShard;
		}

		throw new TenantShardNotFoundException(tenantId);
	}

	/// <inheritdoc />
	public IReadOnlyCollection<string> GetRegisteredShardIds() =>
		_shards.Keys.ToArray();
}
