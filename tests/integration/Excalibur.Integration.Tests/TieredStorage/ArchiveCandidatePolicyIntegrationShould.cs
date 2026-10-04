// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Data.Common;
using Dapper;
using Excalibur.EventSourcing;
using Excalibur.Integration.Tests.Data.EventStore;
using Microsoft.Data.SqlClient;
using Npgsql;
using Shouldly;
using Xunit;

namespace Excalibur.Integration.Tests.TieredStorage;

[Collection(SqlServerEventStoreTestCollection.CollectionName)]
[Trait("Category", "Integration")]
[Trait("Component", "TieredStorage")]
[Trait("Pattern", "EventSourcing")]
public sealed class SqlServerArchiveCandidatePolicyIntegrationShould(SqlServerEventStoreContainerFixture fixture)
{
	[Theory]
	[InlineData("retention")]
	[InlineData("retention-only")]
	[InlineData("global-or")]
	[InlineData("inverted-time")]
	[InlineData("batch")]
	[InlineData("markers")]
	[InlineData("keys")]
	[InlineData("unknown")]
	public async Task SelectOnlyTheSafePendingPrefix(string scenario)
	{
		fixture.DockerAvailable.ShouldBeTrue();
		await fixture.EnsureInitializedAsync();
		await using var connection = new SqlConnection(fixture.ConnectionString);
		await connection.OpenAsync();
		await ArchiveCandidatePolicyScenario.RunAsync(connection, false, scenario);
	}
}

[Collection(PostgresEventStoreTestCollection.CollectionName)]
[Trait("Category", "Integration")]
[Trait("Component", "TieredStorage")]
[Trait("Pattern", "EventSourcing")]
public sealed class PostgresArchiveCandidatePolicyIntegrationShould(PostgresEventStoreContainerFixture fixture)
{
	[Theory]
	[InlineData("retention")]
	[InlineData("retention-only")]
	[InlineData("global-or")]
	[InlineData("inverted-time")]
	[InlineData("batch")]
	[InlineData("markers")]
	[InlineData("keys")]
	[InlineData("unknown")]
	public async Task SelectOnlyTheSafePendingPrefix(string scenario)
	{
		fixture.DockerAvailable.ShouldBeTrue();
		await fixture.EnsureInitializedAsync();
		await using var connection = new NpgsqlConnection(fixture.ConnectionString);
		await connection.OpenAsync();
		await ArchiveCandidatePolicyScenario.RunAsync(connection, true, scenario);
	}
}

internal static class ArchiveCandidatePolicyScenario
{
	internal static async Task RunAsync(DbConnection connection, bool postgres, string scenario)
	{
		var now = DateTimeOffset.UtcNow;
		var old = now.AddDays(-10);
		var rows = new List<Row>();
		var policy = new ArchivePolicy { MaxAge = TimeSpan.FromDays(1) };
		var expected = new List<(string Tenant, string Id, string Type, long Ceiling, int Count)>();
		var batchSize = 100;
		Row Event(string id, long version = 0) => new("tenant", id, "Order", version, 50_000 + version, old, [1], null, "Created");
		switch (scenario)
		{
			case "retention":
				rows.AddRange(Enumerable.Range(0, 10).Select(v => Event("a", v)));
				policy.RetainRecentCount = 2;
				expected.Add(("tenant", "a", "Order", 7, 8));
				break;
			case "retention-only":
				rows.AddRange(new[] { Event("a", 0), Event("a", 5), Event("a", 9) });
				policy = new ArchivePolicy { RetainRecentCount = 1 };
				expected.Add(("tenant", "a", "Order", 5, 2));
				break;
			case "global-or":
				rows.Add(Event("a")); // Old age alone qualifies despite global position 50000.
				rows.Add(Event("b") with { Timestamp = now, Position = 99 });
				rows.Add(Event("c") with { Timestamp = now, Position = 100 });
				policy.MaxPosition = 100;
				expected.Add(("tenant", "a", "Order", 0, 1));
				expected.Add(("tenant", "b", "Order", 0, 1));
				break;
			case "inverted-time":
				rows.Add(Event("a") with { Timestamp = now });
				rows.Add(Event("a", 1));
				break;
			case "batch":
				rows.Add(Event("a") with { Timestamp = now });
				rows.Add(Event("b") with { Data = null, ArchivedAt = now });
				rows.Add(Event("c"));
				batchSize = 1;
				expected.Add(("tenant", "c", "Order", 0, 1));
				break;
			case "markers":
				rows.Add(Event("a") with { Data = null, ArchivedAt = now });
				rows.Add(Event("a", 1) with { ArchivedAt = now });
				rows.Add(Event("a", 2) with { EventType = ErasedEventMarker.EventType, ArchivedAt = now });
				rows.Add(Event("a", 3));
				rows.Add(Event("b") with { Data = null });
				rows.Add(Event("b", 1));
				expected.Add(("tenant", "a", "Order", 1, 1));
				break;
			case "keys":
				foreach (var tenant in new[] { "a", "b" })
				foreach (var type in new[] { "Invoice", "Order" })
				{
					rows.Add(Event("same") with { Tenant = tenant, Type = type });
					expected.Add((tenant, "same", type, 0, 1));
				}
				break;
			case "unknown":
				rows.Add(Event("a") with { Timestamp = null });
				rows.Add(Event("a", 1));
				break;
			default:
				throw new InvalidOperationException(scenario);
		}

		// Dedicated tables isolate each policy counterexample from unrelated fixture streams.
		// The identifier is generated solely from a fixed prefix and Guid hex, never external input.
		var table = "archive_policy_" + Guid.NewGuid().ToString("N");
		var schema = postgres ? "public" : "dbo";
		var target = schema + "." + table;
		var names = postgres
			? new[] { "tenant_id", "aggregate_id", "aggregate_type", "version", "position", "timestamp", "event_data", "archived_at", "event_type" }
			: new[] { "TenantId", "AggregateId", "AggregateType", "Version", "Position", "Timestamp", "EventData", "ArchivedAt", "EventType" };
		var textType = postgres ? "text" : "nvarchar(200)";
		var timeType = postgres ? "timestamptz" : "datetimeoffset";
		var bytesType = postgres ? "bytea" : "varbinary(max)";
#pragma warning disable CA2100 // Dedicated test table name is fixed prefix + Guid hex; all row values are parameters.
		await connection.ExecuteAsync($"CREATE TABLE {target} ({names[0]} {textType}, {names[1]} {textType}, {names[2]} {textType}, {names[3]} bigint, {names[4]} bigint, {names[5]} {timeType}, {names[6]} {bytesType}, {names[7]} {timeType}, {names[8]} {textType})");
		try
		{
			await connection.ExecuteAsync($"INSERT INTO {target} ({string.Join(",", names)}) VALUES (@Tenant,@Id,@Type,@Version,@Position,@Timestamp,@Data,@ArchivedAt,@EventType)", rows);
			var result = postgres
				? await new Excalibur.EventSourcing.Postgres.Requests.GetArchiveCandidatesRequest(policy, batchSize, now, CancellationToken.None, schema, table).ResolveAsync(connection)
				: await new Excalibur.EventSourcing.SqlServer.Requests.GetArchiveCandidatesRequest(policy, batchSize, now, CancellationToken.None, schema, table).ResolveAsync(connection);
			result.Select(c => (c.Tenant.TenantId, c.AggregateId, c.AggregateType, c.ArchivableUpToVersion, c.EventCount))
				.ShouldBe(expected);
		}
		finally
		{
			await connection.ExecuteAsync($"DROP TABLE {target}");
		}
#pragma warning restore CA2100
	}

	private sealed record Row(string Tenant, string Id, string Type, long Version, long Position,
		DateTimeOffset? Timestamp, byte[]? Data, DateTimeOffset? ArchivedAt, string EventType);
}
