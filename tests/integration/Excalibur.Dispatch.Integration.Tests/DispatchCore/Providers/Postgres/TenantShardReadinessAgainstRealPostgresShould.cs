// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Diagnostics.CodeAnalysis;
using System.Runtime.ExceptionServices;
using Excalibur.Data.Sharding;
using Excalibur.EventSourcing;
using Excalibur.EventSourcing.Postgres;
using Excalibur.EventSourcing.Queries;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Npgsql;
using Tests.Shared.Fixtures;

namespace Excalibur.Dispatch.Integration.Tests.DispatchCore.Providers.Postgres;

[Collection(ContainerCollections.Postgres)]
[Trait("Category", "Integration")]
[Trait("Component", "EventSourcing")]
[Trait("Database", "Postgres")]
[Trait("Pattern", "Regression")]
public sealed class TenantShardReadinessAgainstRealPostgresShould(PostgresContainerFixture fixture)
{
	[Fact]
	public async Task ProbeTheActualNondefaultSchemaInsteadOfTheHealthyPrimaryAndDefault()
	{
		var suffix = Guid.NewGuid().ToString("N");
		var primary = "health_primary_" + suffix;
		var good = "health_good_" + suffix;
		var bad = "health_bad_" + suffix;
		var errors = new List<Exception>();
		ServiceProvider? provider = null;
		try
		{
			await CreateTableAsync(primary, missingArchivedAt: false);
			await CreateTableAsync(good, missingArchivedAt: false);
			await CreateTableAsync(bad, missingArchivedAt: true);
			var map = new PhysicalMap(fixture.ConnectionString, good, bad);
			var services = new ServiceCollection();
			services.AddLogging();
			services.AddSingleton<ITenantShardMap>(map);
			services.AddExcalibur(excalibur => excalibur.AddEventSourcing(events =>
			{
				events.UsePostgres(pg => pg.ConnectionString(fixture.ConnectionString).EventStoreSchema(primary));
				events.UsePostgresTenantEventStore();
			}));
			services.AddHealthChecks().AddTenantShardHealthCheck();
			provider = services.BuildServiceProvider();
			// Prove an unrelated primary/default read succeeds while the actual bad shard cannot.
			(await provider.GetRequiredService<IGlobalStreamQuery>().ReadAllAsync(
				GlobalStreamPosition.Start, 1, TestContext.Current.CancellationToken)).ShouldBeEmpty();
			var registration = provider.GetRequiredService<IOptions<HealthCheckServiceOptions>>().Value
				.Registrations.Single(item => item.Name == "tenant-shard");
			var context = new HealthCheckContext { Registration = registration };
			var check = registration.Factory(provider);
			var failed = await check.CheckHealthAsync(context, TestContext.Current.CancellationToken);
			failed.Status.ShouldBe(HealthStatus.Degraded);
			failed.Data["healthyShards"].ShouldBe(1);
			failed.Data["unhealthyShards"].ShouldBe(1);
			failed.Data["failedShardIds"].ShouldBe("physical-bad");
			var cause = failed.Exception.ShouldBeOfType<AggregateException>().InnerExceptions.Single()
				.InnerException.ShouldBeOfType<PostgresException>();
			cause.SqlState.ShouldBe("42703");
			cause.MessageText.ShouldContain("archived_at");
			map.TenantLookups.ShouldBe(0);
			await ExecuteAsync($"ALTER TABLE \"{bad}\".events ADD archived_at timestamptz NULL;",
				TestContext.Current.CancellationToken);
			var repaired = await check.CheckHealthAsync(context, TestContext.Current.CancellationToken);
			repaired.Status.ShouldBe(HealthStatus.Healthy);
			repaired.Data["healthyShards"].ShouldBe(2);
			map.TenantLookups.ShouldBe(0);
		}
		catch (Exception error) { errors.Add(error); }
		finally
		{
			// Cleanup does not reuse a canceled test token, and one cleanup failure does not mask
			// the original assertion or prevent cleanup attempts for the remaining schemas.
			if (provider is not null)
			{
				try { await provider.DisposeAsync(); }
				catch (Exception error) { errors.Add(error); }
			}
			foreach (var schema in new[] { primary, good, bad })
			{
				try
				{
					using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(30));
					await ExecuteAsync($"DROP SCHEMA IF EXISTS \"{schema}\" CASCADE;", cleanup.Token);
				}
				catch (Exception error) { errors.Add(error); }
			}
		}
		if (errors.Count == 1) { ExceptionDispatchInfo.Capture(errors[0]).Throw(); }
		if (errors.Count > 1) { throw new AggregateException(errors); }
	}

	private async Task CreateTableAsync(string schema, bool missingArchivedAt)
	{
		await ExecuteAsync($"CREATE SCHEMA \"{schema}\";", TestContext.Current.CancellationToken);
		await ExecuteAsync($"""
		CREATE TABLE "{schema}".events (
			position bigint NOT NULL PRIMARY KEY, event_id varchar(255) NOT NULL,
			aggregate_id varchar(255) NOT NULL, aggregate_type varchar(255) NOT NULL,
			event_type varchar(255) NOT NULL, event_data bytea NULL, metadata bytea NULL,
			version bigint NOT NULL, timestamp timestamptz NOT NULL, tenant_id varchar(450) NULL
			{(missingArchivedAt ? "" : ", archived_at timestamptz NULL")});
		""", TestContext.Current.CancellationToken);
	}

	[SuppressMessage("Security", "CA2100:Review SQL queries for security vulnerabilities",
		Justification = "Private callers supply fixed DDL and locally generated GUID schema identifiers only.")]
	private async Task ExecuteAsync(string text, CancellationToken cancellationToken)
	{
		await using var connection = new NpgsqlConnection(fixture.ConnectionString);
		await connection.OpenAsync(cancellationToken);
		await using var command = connection.CreateCommand();
		command.CommandText = text;
		command.CommandTimeout = 30;
		_ = await command.ExecuteNonQueryAsync(cancellationToken);
	}

	private sealed class PhysicalMap(string connection, string goodSchema, string badSchema) : ITenantShardMap
	{
		internal int TenantLookups;
		public ShardInfo GetShardInfo(string tenantId)
		{
			Interlocked.Increment(ref TenantLookups);
			return new("physical-good", connection, SchemaName: goodSchema);
		}
		public ShardInfo GetShardById(string id) => id switch
		{
			"physical-good" => new(id, connection, SchemaName: goodSchema),
			"physical-bad" => new(id, connection, SchemaName: badSchema),
			_ => throw new KeyNotFoundException(id)
		};
		public IReadOnlyCollection<string> GetRegisteredShardIds() => ["physical-good", "physical-bad"];
	}
}
