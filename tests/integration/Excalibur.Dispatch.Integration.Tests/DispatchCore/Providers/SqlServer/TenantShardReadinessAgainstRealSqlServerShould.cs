// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Diagnostics.CodeAnalysis;
using Excalibur.Data.Sharding;
using Excalibur.EventSourcing;
using Excalibur.EventSourcing.Queries;
using Excalibur.EventSourcing.SqlServer;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Tests.Shared.Fixtures;

namespace Excalibur.Dispatch.Integration.Tests.DispatchCore.Providers.SqlServer;

[Collection(ContainerCollections.SqlServer)]
[Trait("Category", "Integration")]
[Trait("Component", "EventSourcing")]
[Trait("Database", "SqlServer")]
[Trait("Pattern", "Regression")]
public sealed class TenantShardReadinessAgainstRealSqlServerShould(SqlServerContainerFixture fixture)
{
	[Fact]
	public async Task ProbeTheActualNondefaultSchemaInsteadOfTheHealthyPrimaryAndDefault()
	{
		var suffix = Guid.NewGuid().ToString("N");
		var primary = "HealthPrimary_" + suffix;
		var good = "HealthGood_" + suffix;
		var bad = "HealthBad_" + suffix;
		await CreateTableAsync(primary, missingArchivedAt: false);
		await CreateTableAsync(good, missingArchivedAt: false);
		await CreateTableAsync(bad, missingArchivedAt: true);
		try
		{
			var map = new PhysicalMap(fixture.ConnectionString, good, bad);
			var services = new ServiceCollection(); services.AddLogging();
			services.AddSingleton<ITenantShardMap>(map);
			services.AddExcalibur(excalibur => excalibur.AddEventSourcing(events =>
			{
				events.UseSqlServer(sql => sql.ConnectionString(fixture.ConnectionString).EventStoreSchema(primary));
				events.UseSqlServerTenantEventStore();
			}));
			services.AddHealthChecks().AddTenantShardHealthCheck();
			await using var provider = services.BuildServiceProvider();
			// A false implementation which reads this primary for every shard would remain green.
			(await provider.GetRequiredService<IGlobalStreamQuery>().ReadAllAsync(GlobalStreamPosition.Start, 1, TestContext.Current.CancellationToken)).ShouldBeEmpty();
			var registration = provider.GetRequiredService<IOptions<HealthCheckServiceOptions>>().Value.Registrations.Single(item => item.Name == "tenant-shard");
			var context = new HealthCheckContext { Registration = registration };
			var check = registration.Factory(provider);
			var failed = await check.CheckHealthAsync(context, TestContext.Current.CancellationToken);
			failed.Status.ShouldBe(HealthStatus.Degraded);
			failed.Data["healthyShards"].ShouldBe(1);
			failed.Data["unhealthyShards"].ShouldBe(1);
			failed.Data["failedShardIds"].ShouldBe("physical-bad");
			failed.Exception.ShouldNotBeNull().ToString().ShouldContain("ArchivedAt");
			map.TenantLookups.ShouldBe(0);
			await ExecuteAsync($"ALTER TABLE [{bad}].[EventStoreEvents] ADD ArchivedAt datetimeoffset NULL;");
			var repaired = await check.CheckHealthAsync(context, TestContext.Current.CancellationToken);
			repaired.Status.ShouldBe(HealthStatus.Healthy);
			repaired.Data["healthyShards"].ShouldBe(2);
			map.TenantLookups.ShouldBe(0);
		}
		finally
		{
			foreach (var schema in new[] { primary, good, bad })
			{
				await ExecuteAsync($"DROP TABLE [{schema}].[EventStoreEvents]; DROP SCHEMA [{schema}];");
			}
		}
	}

	private async Task CreateTableAsync(string schema, bool missingArchivedAt)
	{
		await ExecuteAsync($"CREATE SCHEMA [{schema}];");
		await ExecuteAsync($"""
		CREATE TABLE [{schema}].[EventStoreEvents](
			Position bigint NOT NULL PRIMARY KEY, EventId nvarchar(64) NOT NULL,
			AggregateId nvarchar(256) NOT NULL, AggregateType nvarchar(256) NOT NULL,
			EventType nvarchar(256) NOT NULL, EventData varbinary(max) NULL,
			Metadata varbinary(max) NULL, Version bigint NOT NULL,
			Timestamp datetimeoffset NOT NULL, TenantId nvarchar(256) NULL
			{(missingArchivedAt ? "" : ", ArchivedAt datetimeoffset NULL")});
		""");
	}

	[SuppressMessage("Security", "CA2100:Review SQL queries for security vulnerabilities", Justification = "Private callers provide fixed DDL and locally generated GUID schema names only.")]
	private async Task ExecuteAsync(string text)
	{
		await using var connection = new SqlConnection(fixture.ConnectionString);
		await connection.OpenAsync(TestContext.Current.CancellationToken);
		await using var command = connection.CreateCommand(); command.CommandText = text;
		command.CommandTimeout = 30;
		_ = await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
	}

	private sealed class PhysicalMap(string connection, string goodSchema, string badSchema) : ITenantShardMap
	{
		internal int TenantLookups;
		public ShardInfo GetShardInfo(string tenantId) { Interlocked.Increment(ref TenantLookups); return new("physical-good", connection, SchemaName: goodSchema); }
		public ShardInfo GetShardById(string id) => id switch
		{
			"physical-good" => new(id, connection, SchemaName: goodSchema),
			"physical-bad" => new(id, connection, SchemaName: badSchema),
			_ => throw new KeyNotFoundException(id)
		};
		public IReadOnlyCollection<string> GetRegisteredShardIds() => ["physical-good", "physical-bad"];
	}
}
