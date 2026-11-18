// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Collections.Concurrent;

using Excalibur.Data.Sharding;

using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Excalibur.EventSourcing.Sharding;

/// <summary>Reads each registered physical event-store shard using its provider's readiness binding.</summary>
/// <remarks>
/// All reads passing is Healthy, partial failure is Degraded, and no successful read is Unhealthy.
/// Missing readiness capabilities cannot report Healthy. This proves provider-specific read access,
/// not write access, tenant isolation, or schema integrity on an empty schemaless store.
/// </remarks>
internal sealed class TenantShardHealthCheck : IHealthCheck
{
	private readonly ITenantShardMap _shardMap;
	private readonly ITenantShardReadinessProbe? _probe;
	private readonly bool _resolverAvailable;
	private readonly int _maxConcurrentProbes;

	internal TenantShardHealthCheck(ITenantShardMap shardMap,
		ITenantStoreResolver<IEventStore>? eventStoreResolver = null, int maxConcurrentProbes = 4)
	{
		_shardMap = shardMap ?? throw new ArgumentNullException(nameof(shardMap));
		ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxConcurrentProbes);
		_probe = eventStoreResolver as ITenantShardReadinessProbe;
		_resolverAvailable = eventStoreResolver is not null;
		_maxConcurrentProbes = maxConcurrentProbes;
	}

	public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();
		var data = new Dictionary<string, object>
		{
			["shardMapType"] = _shardMap.GetType().Name,
			["probeAvailable"] = _probe is not null,
			["resolverAvailable"] = _resolverAvailable,
			["maxConcurrentProbes"] = _maxConcurrentProbes
		};
		try
		{
			var ids = _shardMap.GetRegisteredShardIds().Distinct(StringComparer.Ordinal).ToArray();
			data["registeredShards"] = ids.Length;
			if (ids.Length == 0)
			{
				return HealthCheckResult.Unhealthy("No physical shards are registered.", data: data);
			}
			if (_probe is null)
			{
				return HealthCheckResult.Unhealthy("The tenant event-store resolver supplies no physical-shard readiness probe; database reads were not verified.", data: data);
			}
			var failures = new ConcurrentDictionary<string, Exception>(StringComparer.Ordinal);
			await Parallel.ForEachAsync(ids,
				new ParallelOptions { CancellationToken = cancellationToken, MaxDegreeOfParallelism = _maxConcurrentProbes },
				async (id, token) =>
				{
					try { await _probe.ProbeShardAsync(id, token).ConfigureAwait(false); }
					catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
					catch (Exception error) { failures[id] = error; }
				}).ConfigureAwait(false);
			cancellationToken.ThrowIfCancellationRequested();
			var failed = failures.OrderBy(pair => pair.Key, StringComparer.Ordinal).ToArray();
			data["healthyShards"] = ids.Length - failed.Length;
			data["unhealthyShards"] = failed.Length;
			if (failed.Length == 0)
			{
				return HealthCheckResult.Healthy($"All {ids.Length} physical shard(s) served their readiness read.", data);
			}
			data["failedShardIds"] = string.Join(", ", failed.Select(pair => pair.Key));
			var cause = new AggregateException(failed.Select(pair =>
				new InvalidOperationException($"Physical shard '{pair.Key}' readiness read failed.", pair.Value)));
			return failed.Length == ids.Length
				? HealthCheckResult.Unhealthy($"All {ids.Length} physical shard(s) refused their readiness read.", cause, data)
				: HealthCheckResult.Degraded($"{ids.Length - failed.Length}/{ids.Length} physical shard(s) served their readiness read.", cause, data);
		}
		catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
		catch (Exception error) { return HealthCheckResult.Unhealthy("Tenant shard readiness check failed.", error, data); }
	}
}
