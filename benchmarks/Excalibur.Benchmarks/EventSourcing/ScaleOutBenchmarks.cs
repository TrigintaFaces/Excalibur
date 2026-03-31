// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Reflection;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;

using Excalibur.Data.Abstractions.Sharding;
using Excalibur.Dispatch.Abstractions;
using Excalibur.EventSourcing.Abstractions;

namespace Excalibur.Benchmarks.EventSourcing;

/// <summary>
/// T.15 (1ngqu5): BenchmarkDotNet benchmarks for scale-out features --
/// shard resolution and auto-snapshot policy evaluation.
/// Internal types accessed via reflection for setup; hot paths measured directly.
/// </summary>
[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.HostProcess)]
public class ScaleOutBenchmarks
{
	private ITenantShardMap _shardMap = null!;
	private object _snapshotPolicy = null!;
	private MethodInfo _shouldSnapshotMethod = null!;
	private AutoSnapshotOptions _snapshotOptions = null!;
	private SnapshotDecisionContext _snapshotContext = null!;

	[GlobalSetup]
	public void GlobalSetup()
	{
		// Shard map: 100 tenants across 10 shards via reflection
		var shards = new Dictionary<string, ShardInfo>();
		var tenantMappings = new Dictionary<string, string>();
		for (var i = 0; i < 10; i++)
			shards[$"shard-{i}"] = new ShardInfo($"shard-{i}", $"Server=shard{i};");
		for (var i = 0; i < 100; i++)
			tenantMappings[$"tenant-{i}"] = $"shard-{i % 10}";

		var esAssembly = typeof(Excalibur.EventSourcing.Projections.MultiStreamProjection<>).Assembly;
		var shardMapType = esAssembly.GetType("Excalibur.EventSourcing.Sharding.InMemoryTenantShardMap")!;
		_shardMap = (ITenantShardMap)Activator.CreateInstance(
			shardMapType, shards, tenantMappings, new ShardMapOptions())!;

		// AutoSnapshotPolicy via reflection
		_snapshotOptions = new AutoSnapshotOptions { EventCountThreshold = 100 };
		_snapshotContext = new SnapshotDecisionContext(
			"agg-1", "Order", 150, 50, DateTimeOffset.UtcNow.AddMinutes(-30), 100);

		var policyType = esAssembly.GetType("Excalibur.EventSourcing.Snapshots.AutoSnapshotPolicy")!;
		_shouldSnapshotMethod = policyType.GetMethod("ShouldSnapshot", BindingFlags.Static | BindingFlags.NonPublic)!;
	}

	/// <summary>
	/// InMemoryTenantShardMap lookup -- target &lt; 1us (S6 decision).
	/// </summary>
	[Benchmark(Baseline = true)]
	public ShardInfo ShardMapLookup()
	{
		return _shardMap.GetShardInfo("tenant-42");
	}

	/// <summary>
	/// Auto-snapshot policy evaluation -- pure arithmetic, zero allocation.
	/// </summary>
	[Benchmark]
	public object? SnapshotPolicyEval()
	{
		return _shouldSnapshotMethod.Invoke(null, [_snapshotOptions, _snapshotContext]);
	}
}
