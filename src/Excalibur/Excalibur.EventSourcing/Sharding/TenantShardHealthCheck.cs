// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.Abstractions.Sharding;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Excalibur.EventSourcing.Sharding;

/// <summary>
/// Health check that verifies connectivity to all configured tenant data shards.
/// </summary>
/// <remarks>
/// <para>
/// Reports <see cref="HealthStatus.Healthy"/> when all shards are reachable,
/// <see cref="HealthStatus.Degraded"/> when some shards are unreachable,
/// and <see cref="HealthStatus.Unhealthy"/> when no shards are reachable.
/// </para>
/// </remarks>
internal sealed class TenantShardHealthCheck : IHealthCheck
{
	private readonly ITenantShardMap _shardMap;
	private readonly ITenantStoreResolver<Abstractions.IEventStore>? _eventStoreResolver;

	internal TenantShardHealthCheck(
		ITenantShardMap shardMap,
		ITenantStoreResolver<Abstractions.IEventStore>? eventStoreResolver = null)
	{
		ArgumentNullException.ThrowIfNull(shardMap);
		_shardMap = shardMap;
		_eventStoreResolver = eventStoreResolver;
	}

	/// <inheritdoc />
	public Task<HealthCheckResult> CheckHealthAsync(
		HealthCheckContext context,
		CancellationToken cancellationToken = default)
	{
		var data = new Dictionary<string, object>
		{
			["shardMapType"] = _shardMap.GetType().Name,
			["resolverAvailable"] = _eventStoreResolver is not null
		};

		try
		{
			var shardIds = _shardMap.GetRegisteredShardIds();
			data["registeredShards"] = shardIds.Count;

			if (_eventStoreResolver is null)
			{
				// No resolver available -- we can only verify the shard map itself
				data["detail"] = "No ITenantStoreResolver registered; shard map enumeration only.";
				return Task.FromResult(HealthCheckResult.Healthy(
					$"Shard map operational with {shardIds.Count} shard(s) (resolver not configured).", data));
			}

			if (shardIds.Count == 0)
			{
				return Task.FromResult(HealthCheckResult.Unhealthy(
					"No shards registered in the shard map.", data: data));
			}

			// Probe each shard by resolving its event store through the resolver.
			// ShardInfo.ShardId is used as the tenantId probe because providers
			// typically map shard IDs directly for self-check purposes.
			var healthy = new List<string>();
			var unhealthy = new List<string>();

			foreach (var shardId in shardIds)
			{
				try
				{
					// Attempt to resolve the event store for this shard.
					// This validates that the shard's connection/configuration is functional.
					_eventStoreResolver.Resolve(shardId);
					healthy.Add(shardId);
				}
#pragma warning disable CA1031 // Health check must report per-shard failures, not throw
				catch (Exception)
#pragma warning restore CA1031
				{
					unhealthy.Add(shardId);
				}
			}

			data["healthyShards"] = healthy.Count;
			data["unhealthyShards"] = unhealthy.Count;

			if (unhealthy.Count > 0)
			{
				data["failedShardIds"] = string.Join(", ", unhealthy);
			}

			if (unhealthy.Count == shardIds.Count)
			{
				return Task.FromResult(HealthCheckResult.Unhealthy(
					$"All {shardIds.Count} shard(s) are unreachable.", data: data));
			}

			if (unhealthy.Count > 0)
			{
				return Task.FromResult(HealthCheckResult.Degraded(
					$"{healthy.Count}/{shardIds.Count} shard(s) reachable, {unhealthy.Count} unreachable.", data: data));
			}

			return Task.FromResult(HealthCheckResult.Healthy(
				$"All {shardIds.Count} shard(s) are reachable.", data));
		}
#pragma warning disable CA1031 // Health checks must not throw
		catch (Exception ex)
#pragma warning restore CA1031
		{
			return Task.FromResult(HealthCheckResult.Unhealthy(
				"Tenant shard health check failed.", ex, data));
		}
	}
}
