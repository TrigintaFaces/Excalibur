// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Data.Common;
using Dapper;
using Excalibur.Dispatch;
using Excalibur.Domain.Model;
using Microsoft.Extensions.Logging.Abstractions;
using Excalibur.EventSourcing;
using Excalibur.EventSourcing.Postgres;
using Excalibur.EventSourcing.SqlServer;
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
public sealed class SqlServerArchiveScannerFairnessShould(SqlServerEventStoreContainerFixture fixture)
{
	[Fact]
	public async Task UseTheCapturedStoreClockAndPreserveCaseDistinctTenants()
	{
		fixture.DockerAvailable.ShouldBeTrue();
		await fixture.EnsureInitializedAsync();
		await ArchiveScannerCapabilityScenario.RunAsync((tenant, clock) =>
			new SqlServerEventStore(fixture.ConnectionString, NullLogger<SqlServerEventStore>.Instance, tenant) { TimeProvider = clock });
	}

	[Fact]
	public async Task BoundTheRoundAndAdvanceAcrossEveryExaminedStream()
	{
		fixture.DockerAvailable.ShouldBeTrue();
		await fixture.EnsureInitializedAsync();
		await using var connection = new SqlConnection(fixture.ConnectionString);
		await connection.OpenAsync();
		await ArchiveScannerScenario.RunAsync(connection, false, table =>
			new SqlServerArchiveScanner(() => new SqlConnection(fixture.ConnectionString), "dbo", table));
	}
}

[Collection(PostgresEventStoreTestCollection.CollectionName)]
[Trait("Category", "Integration")]
[Trait("Component", "TieredStorage")]
[Trait("Pattern", "EventSourcing")]
public sealed class PostgresArchiveScannerFairnessShould(PostgresEventStoreContainerFixture fixture)
{
	[Fact]
	public async Task UseTheCapturedStoreClockAndPreserveCaseDistinctTenants()
	{
		fixture.DockerAvailable.ShouldBeTrue();
		await fixture.EnsureInitializedAsync();
		await using var source = NpgsqlDataSource.Create(fixture.ConnectionString);
		await ArchiveScannerCapabilityScenario.RunAsync((tenant, clock) =>
			new PostgresEventStore(source, NullLogger<PostgresEventStore>.Instance, tenant) { TimeProvider = clock });
	}

	[Fact]
	public async Task BoundTheRoundAndAdvanceAcrossEveryExaminedStream()
	{
		fixture.DockerAvailable.ShouldBeTrue();
		await fixture.EnsureInitializedAsync();
		await using var source = NpgsqlDataSource.Create(fixture.ConnectionString);
		await using var connection = await source.OpenConnectionAsync();
		await ArchiveScannerScenario.RunAsync(connection, true, table => new PostgresArchiveScanner(source, "public", table));
	}
}

internal static class ArchiveScannerScenario
{
	internal static async Task RunAsync(DbConnection connection, bool postgres, Func<string, IEventStoreArchiveScanner> create)
	{
		using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(2));
		var table = "archive_scan_" + Guid.NewGuid().ToString("N");
		var target = (postgres ? "public." : "dbo.") + table;
		var names = postgres
			? new[] { "tenant_id", "aggregate_id", "aggregate_type", "version", "position", "timestamp", "event_data", "archived_at", "event_type" }
			: new[] { "TenantId", "AggregateId", "AggregateType", "Version", "Position", "Timestamp", "EventData", "ArchivedAt", "EventType" };
		var textType = postgres ? "text" : "nvarchar(200)";
		var timeType = postgres ? "timestamptz" : "datetimeoffset";
		var bytesType = postgres ? "bytea" : "varbinary(max)";
		var old = DateTimeOffset.UtcNow.AddDays(-10);
		var position = 0L;
#pragma warning disable CA2100 // Table name is fixed prefix + Guid hex; values are parameters.
		await connection.ExecuteAsync($"CREATE TABLE {target} ({names[0]} {textType}, {names[1]} {textType}, {names[2]} {textType}, {names[3]} bigint, {names[4]} bigint, {names[5]} {timeType}, {names[6]} {bytesType}, {names[7]} {timeType}, {names[8]} {textType})");
		async Task Insert(string tenant, string id, string type, long version, DateTimeOffset timestamp)
		{
			await connection.ExecuteAsync($"INSERT INTO {target} ({string.Join(",", names)}) VALUES (@tenant,@id,@type,@version,@position,@timestamp,@data,NULL,'Created')",
				new { tenant, id, type, version, position = ++position, timestamp, data = new byte[] { 1 } });
		}
		try
		{
			await Insert("a", "empty", "Order", 0, DateTimeOffset.UtcNow);
			foreach (var type in new[] { "Invoice", "Order" })
			foreach (var version in new long[] { 0, 1 })
				await Insert("b", "same", type, version, old);
			foreach (var version in new long[] { 0, 1 })
				await Insert("z", "same", "Order", version, old);

			var scanner = create(table);
			var policy = new ArchivePolicy { MaxAge = TimeSpan.FromDays(1), RetainRecentCount = 1 };
			var first = await scanner.ScanArchiveCandidatesAsync(policy, 1, null, timeout.Token);
			first.Candidates.ShouldBeEmpty();
			first.Continuation.ShouldNotBeNull();
			await Should.ThrowAsync<ArgumentException>(async () =>
				await create(table).ScanArchiveCandidatesAsync(policy, 1, first.Continuation, timeout.Token));

			// Neither mutable options nor newly inserted keys can restart or extend this round.
			policy.MaxAge = TimeSpan.FromDays(36500);
			policy.RetainRecentCount = 100;
			foreach (var tenant in new[] { "a", "zz" })
			foreach (var version in new long[] { 0, 1 })
				await Insert(tenant, "new", "Order", version, old);
			// A post-horizon append to an EXISTING stream must participate in retention ranking.
			await Insert("b", "same", "Invoice", 2, old);
			var results = new List<ArchiveCandidate>();
			var cursor = first.Continuation;
			var pages = 0;
			while (cursor is not null)
			{
				(++pages).ShouldBeLessThanOrEqualTo(4);
				var page = await scanner.ScanArchiveCandidatesAsync(policy, 1, cursor, timeout.Token);
				page.Candidates.Count.ShouldBeLessThanOrEqualTo(1);
				results.AddRange(page.Candidates);
				cursor = page.Continuation;
			}
			results.Select(c => (c.Tenant.TenantId, c.AggregateType, c.ArchivableUpToVersion, c.EventCount))
				.ShouldBe(new[] { ("b", "Invoice", 1L, 2), ("b", "Order", 0L, 1), ("z", "Order", 0L, 1) });

			// A new round honors changed options; with retention 100 all streams are ineligible.
			var changed = await scanner.ScanArchiveCandidatesAsync(policy, 100, null, timeout.Token);
			changed.Candidates.ShouldBeEmpty();
			policy.MaxAge = TimeSpan.FromDays(1);
			policy.RetainRecentCount = 1;
			var next = await scanner.ScanArchiveCandidatesAsync(policy, 100, null, timeout.Token);
			next.Candidates.Count.ShouldBe(5);
			next.Candidates.Count(c => c.AggregateId == "new").ShouldBe(2);
		}
		finally
		{
			await connection.ExecuteAsync($"DROP TABLE {target}");
		}
#pragma warning restore CA2100
	}
}

internal static class ArchiveScannerCapabilityScenario
{
	internal static async Task RunAsync(Func<ITenantContext, TimeProvider, IEventStore> create)
	{
		using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(2));
		var clock = new FixedClock(DateTimeOffset.UtcNow.AddDays(30));
		var context = new MutableTenant();
		var store = create(context, clock);
		var scanner = (store.GetService(typeof(IEventStoreArchiveScanner)) as IEventStoreArchiveScanner).ShouldNotBeNull();
		store.GetService(typeof(IEventStoreArchiveScanner)).ShouldBeSameAs(scanner);
		var id = Guid.NewGuid().ToString("N");
		var prefix = "scan-" + id;
		var tenants = new[] { prefix + "A", prefix + "a" };
		foreach (var tenant in tenants)
		{
			context.TenantId = tenant;
			var events = Enumerable.Range(0, 2).Select(v => new ScanEvent(id, v)
			{
				OccurredAt = clock.GetUtcNow().AddDays(-10),
			}).ToArray();
			(await store.AppendAsync(id, "ScanClock", events, -1, timeout.Token)).Success.ShouldBeTrue();
		}
		context.TenantId = "unrelated-scanner-host";
		var results = new List<ArchiveCandidate>();
		ArchiveScanCursor? cursor = null;
		var pages = 0;
		do
		{
			(++pages).ShouldBeLessThanOrEqualTo(100);
			var page = await scanner.ScanArchiveCandidatesAsync(new ArchivePolicy { MaxAge = TimeSpan.FromDays(1), RetainRecentCount = 1 }, 20, cursor, timeout.Token);
			results.AddRange(page.Candidates.Where(c => c.AggregateId == id));
			cursor = page.Continuation;
		} while (cursor is not null);
		results.Count.ShouldBe(2);
		results.Select(c => c.Tenant.TenantId).Order(StringComparer.Ordinal).ShouldBe(tenants.Order(StringComparer.Ordinal));
		results.ShouldAllBe(c => c.ArchivableUpToVersion == 0 && c.EventCount == 1);
	}

	private sealed class MutableTenant : ITenantContext
	{
		public string? TenantId { get; set; }
		public bool HasTenant => TenantId is not null;
	}

	private sealed class FixedClock(DateTimeOffset now) : TimeProvider
	{
		public override DateTimeOffset GetUtcNow() => now;
	}

	[MessageName("Test.ArchiveScan.Event")]
	private sealed record ScanEvent(string AggregateId, long Version) : IDomainEvent
	{
		public string EventId { get; init; } = Guid.NewGuid().ToString("N");
		public DateTimeOffset OccurredAt { get; init; }
		public IDictionary<string, object>? Metadata { get; init; }
	}
}
