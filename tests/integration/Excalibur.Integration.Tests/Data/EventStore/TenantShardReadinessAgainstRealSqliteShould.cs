// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Data.Sharding;
using Excalibur.EventSourcing;
using Excalibur.EventSourcing.Queries;
using Excalibur.EventSourcing.Sharding;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace Excalibur.Integration.Tests.Data.EventStore;

/// <summary>Actual two-database read/counterexample proof; this is not a shipped SQLite shard adapter.</summary>
[Trait("Category", "Integration")]
[Trait("Component", "EventSourcing")]
[Trait("Database", "Sqlite")]
[Trait("Pattern", "Regression")]
public sealed class TenantShardReadinessAgainstRealSqliteShould
{
	[Fact]
	public async Task IdentifyTheBrokenPhysicalShardDespiteAHealthyDefaultAndRecoverAfterRepair()
	{
		var goodPath = Path.Combine(Path.GetTempPath(), $"shard-good-{Guid.NewGuid():N}.db");
		var badPath = Path.Combine(Path.GetTempPath(), $"shard-bad-{Guid.NewGuid():N}.db");
		var good = new ShardInfo("physical-good", $"Data Source={goodPath}");
		var bad = new ShardInfo("physical-bad", $"Data Source={badPath}");
		try
		{
			await using (var database = new SqliteConnection(bad.ConnectionString))
			{
				await database.OpenAsync(TestContext.Current.CancellationToken);
				await using var command = database.CreateCommand();
				// Existing table deliberately lacks Timestamp; absent-table auto-provisioning is not the oracle.
				command.CommandText = """
					CREATE TABLE Events (GlobalPosition INTEGER PRIMARY KEY, EventId TEXT NOT NULL,
					AggregateId TEXT NOT NULL, AggregateType TEXT NOT NULL, EventType TEXT NOT NULL,
					EventData BLOB, Metadata BLOB, Version INTEGER NOT NULL, TenantId TEXT NOT NULL);
				""";
				_ = await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
			}
			await using var goodProvider = CreateProvider(good.ConnectionString);
			await using var badProvider = CreateProvider(bad.ConnectionString);
			var goodReader = goodProvider.GetRequiredService<IGlobalStreamQuery>();
			// SQLite's existing reader creates a missing table. Do that setup before the health observation.
			(await goodReader.ReadAllAsync(GlobalStreamPosition.Start, 1, TestContext.Current.CancellationToken)).ShouldBeEmpty();
			var map = new Map(good, bad);
			var resolver = new Resolver(map, new Dictionary<string, ServiceProvider>
			{
				[good.ShardId] = goodProvider, [bad.ShardId] = badProvider
			});
			var services = new ServiceCollection();
			services.AddSingleton<ITenantShardMap>(map);
			services.AddSingleton<ITenantStoreResolver<IEventStore>>(resolver);
			services.AddHealthChecks().AddTenantShardHealthCheck();
			await using var provider = services.BuildServiceProvider();
			var registration = provider.GetRequiredService<IOptions<HealthCheckServiceOptions>>().Value.Registrations.Single(item => item.Name == "tenant-shard");
			var check = registration.Factory(provider);
			var context = new HealthCheckContext { Registration = registration };
			var failed = await check.CheckHealthAsync(context, TestContext.Current.CancellationToken);
			failed.Status.ShouldBe(HealthStatus.Degraded);
			failed.Data["healthyShards"].ShouldBe(1);
			failed.Data["unhealthyShards"].ShouldBe(1);
			failed.Data["failedShardIds"].ShouldBe(bad.ShardId);
			failed.Exception.ShouldNotBeNull().ToString().ShouldContain("Timestamp");
			map.TenantLookups.ShouldBe(0);
			resolver.TenantResolutions.ShouldBe(0);
			await using (var database = new SqliteConnection(bad.ConnectionString))
			{
				await database.OpenAsync(TestContext.Current.CancellationToken);
				await using var command = database.CreateCommand();
				command.CommandText = "ALTER TABLE Events ADD COLUMN Timestamp TEXT NOT NULL DEFAULT '2026-01-01T00:00:00Z';";
				_ = await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
			}
			var repaired = await check.CheckHealthAsync(context, TestContext.Current.CancellationToken);
			repaired.Status.ShouldBe(HealthStatus.Healthy);
			repaired.Data["healthyShards"].ShouldBe(2);
			map.TenantLookups.ShouldBe(0);
		}
		finally
		{
			SqliteConnection.ClearAllPools();
			File.Delete(goodPath); File.Delete(badPath);
		}
	}

	private static ServiceProvider CreateProvider(string connectionString)
	{
		var services = new ServiceCollection(); services.AddLogging();
		services.AddExcalibur(excalibur => excalibur.AddEventSourcing(events => events.UseSqlite(options => options.ConnectionString = connectionString)));
		return services.BuildServiceProvider();
	}

	private sealed class Map(ShardInfo good, ShardInfo bad) : ITenantShardMap
	{
		internal int TenantLookups;
		public ShardInfo GetShardInfo(string tenantId) { Interlocked.Increment(ref TenantLookups); return good; }
		public ShardInfo GetShardById(string id) => id == good.ShardId ? good : id == bad.ShardId ? bad : throw new KeyNotFoundException(id);
		public IReadOnlyCollection<string> GetRegisteredShardIds() => [good.ShardId, bad.ShardId];
	}
	private sealed class Resolver(Map map, Dictionary<string, ServiceProvider> providers) : ITenantStoreResolver<IEventStore>, ITenantShardReadinessProbe
	{
		internal int TenantResolutions;
		public IEventStore Resolve(string tenantId)
		{
			Interlocked.Increment(ref TenantResolutions);
			return providers[map.GetShardInfo(tenantId).ShardId].GetRequiredService<IEventStore>();
		}
		public async Task ProbeShardAsync(string shardId, CancellationToken token)
		{
			var binding = providers[map.GetShardById(shardId).ShardId];
			_ = await binding.GetRequiredService<IGlobalStreamQuery>().ReadAllAsync(GlobalStreamPosition.Start, 1, token);
		}
	}
}
