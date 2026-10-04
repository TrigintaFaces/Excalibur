// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch;
using Excalibur.EventSourcing;
using Excalibur.EventSourcing.Postgres;

using Microsoft.Extensions.Logging.Abstractions;

using Npgsql;

using Shouldly;

using Testcontainers.PostgreSql;

using Tests.Shared.Helpers;

using Xunit;

namespace Excalibur.Integration.Tests.Data.EventStore;

[Trait("Category", "Integration")]
[Trait("Component", "Core")]
[Trait("Pattern", "Integration")]
[Trait("Database", "Postgres")]
public sealed class PostgresAuthoritativeStandbyShould
{
	[Fact]
	public async Task RejectPresentAndAbsentEventsOnARealStaleStandby()
	{
		using var deadline = new CancellationTokenSource(TimeSpan.FromMinutes(3));
		var token = deadline.Token;
		await using var primary = new PostgreSqlBuilder()
			.WithImage("postgres:16-alpine")
			.WithDatabase("authoritative_test")
			.WithUsername("postgres")
			.WithPassword("standby_test_password")
			.WithEnvironment("PGPASSWORD", "standby_test_password")
			.WithPortBinding(5433, true)
			.Build();
		await primary.StartAsync(token).ConfigureAwait(false);
		await using var primarySource = NpgsqlDataSource.Create(primary.GetConnectionString());
		await using (var setup = await primarySource.OpenConnectionAsync(token).ConfigureAwait(false))
		{
			foreach (var script in ShippedSchemaScript.ReadAll(
				"src/Excalibur/Excalibur.EventSourcing.Postgres/Scripts/002_CreateEventStoreSchema.sql"))
			{
#pragma warning disable CA2100 // Fixed shipped schema file; no caller-supplied SQL or identifiers.
				await using var command = new NpgsqlCommand(script, setup);
#pragma warning restore CA2100
				_ = await command.ExecuteNonQueryAsync(token).ConfigureAwait(false);
			}
		}
		var tenant = KeyedTenantPartition.FromStoredValue("standby-test");
		var store = new PostgresEventStore(primarySource, NullLogger<PostgresEventStore>.Instance,
			new FixedTenant(tenant.TenantId));
		var original = new Created(Guid.NewGuid().ToString("N"), 0);
		(await store.AppendAsync(original.AggregateId, "Order", [original], -1, token)
			.ConfigureAwait(false)).Success.ShouldBeTrue();
		var seeded = (await store.LoadAsync(original.AggregateId, "Order", token).ConfigureAwait(false)).Single();
		seeded.EventType.ShouldNotBeNullOrWhiteSpace();
		seeded.EventType.ShouldNotBe(ErasedEventMarker.EventType);

		// A second server process in the disposable container is a physical standby, with its own
		// data directory and port. No host filesystem paths or persistent resources are modified.
		var backup = await primary.ExecAsync(
			["su-exec", "postgres", "pg_basebackup", "-h", "127.0.0.1", "-U", "postgres",
			 "-D", "/tmp/authoritative-standby", "-R", "-X", "stream", "--checkpoint=fast"], token)
			.ConfigureAwait(false);
		backup.ExitCode.ShouldBe(0, backup.Stderr);
		var start = await primary.ExecAsync(
			["su-exec", "postgres", "pg_ctl", "-D", "/tmp/authoritative-standby",
			 "-o", "-p 5433 -c listen_addresses=*", "-l", "/tmp/authoritative-standby.log", "-w", "start"], token)
			.ConfigureAwait(false);
		start.ExitCode.ShouldBe(0, start.Stderr);
		var standbyConnectionString = new NpgsqlConnectionStringBuilder(primary.GetConnectionString())
		{
			Port = primary.GetMappedPublicPort(5433),
		};
		await using var standbySource = NpgsqlDataSource.Create(standbyConnectionString.ConnectionString);
		await using var standby = await standbySource.OpenConnectionAsync(token).ConfigureAwait(false);
		await using (var role = new NpgsqlCommand("SELECT pg_is_in_recovery()", standby))
		{
			(await role.ExecuteScalarAsync(token).ConfigureAwait(false)).ShouldBe(true);
		}
		await using (var pause = new NpgsqlCommand("SELECT pg_wal_replay_pause()", standby))
		{
			_ = await pause.ExecuteNonQueryAsync(token).ConfigureAwait(false);
		}
		await using (var paused = new NpgsqlCommand("SELECT pg_get_wal_replay_pause_state()", standby))
		{
			while (!string.Equals((string?)await paused.ExecuteScalarAsync(token).ConfigureAwait(false), "paused", StringComparison.Ordinal))
			{
				await Task.Delay(TimeSpan.FromMilliseconds(50), token).ConfigureAwait(false);
			}
		}
		(await store.EraseEventsAsync(original.AggregateId, "Order", Guid.NewGuid(), token)
			.ConfigureAwait(false)).ShouldBe(1);
		await using (var stale = new NpgsqlCommand("SELECT event_type FROM public.events WHERE event_id = @id", standby))
		{
			stale.Parameters.AddWithValue("id", original.EventId);
			var staleType = (string?)await stale.ExecuteScalarAsync(token).ConfigureAwait(false);
			staleType.ShouldBe(seeded.EventType,
				"the standby must contain the seeded live event, not merely lack an erased value");
		}
		var primaryReader = store.GetService(typeof(IEventStoreAuthoritativeReader)).ShouldBeAssignableTo<IEventStoreAuthoritativeReader>();
		var current = await primaryReader.ReadCurrentAsync(tenant, original.AggregateId, "Order", original.EventId, 0, token)
			.ConfigureAwait(false);
		current.ShouldNotBeNull();
		current.EventType.ShouldBe(ErasedEventMarker.EventType);
		var replicaStore = new PostgresEventStore(standbySource, NullLogger<PostgresEventStore>.Instance, new FixedTenant(tenant.TenantId));
		var replicaReader = replicaStore.GetService(typeof(IEventStoreAuthoritativeReader)).ShouldBeAssignableTo<IEventStoreAuthoritativeReader>();
		foreach (var eventId in new[] { original.EventId, Guid.NewGuid().ToString("N") })
		{
			var error = await Should.ThrowAsync<InvalidOperationException>(async () =>
				await replicaReader.ReadCurrentAsync(tenant, original.AggregateId, "Order", eventId, 0, token));
			error.Message.ShouldContain("replica");
		}
	}

	private sealed class FixedTenant(string tenantId) : ITenantContext
	{
		public string? TenantId { get; } = tenantId;
		public bool HasTenant => true;
	}

	[MessageName("Test.AuthoritativeStandby.Created")]
	private sealed record Created(string AggregateId, long Version) : IDomainEvent
	{
		public string EventId { get; init; } = Guid.NewGuid().ToString("N");
		public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;
		public IDictionary<string, object>? Metadata { get; init; }
	}
}
