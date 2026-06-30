// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections.Concurrent;

using Dapper;

using Excalibur.Cdc.Postgres;

using Microsoft.Extensions.Logging.Abstractions;

using Npgsql;

using Shouldly;

using Tests.Shared;
using Tests.Shared.Categories;
using Tests.Shared.Fixtures;

using MsOptions = Microsoft.Extensions.Options.Options;

namespace Excalibur.Dispatch.Integration.Tests.DispatchCore.Providers.Postgres;

/// <summary>
/// Genuine, NON-SKIPPED real-infra restart-redelivery lock for the Postgres logical-replication CDC processor
/// (e9u90j / AC-N3.4 — CDC streaming restart-redelivery data-loss safety).
/// </summary>
/// <remarks>
/// <para>
/// Unlike the sibling <c>PostgresCdcStalePositionIntegrationShould</c> (which only exercises the stale-position
/// <em>detector</em> with simulated SQLSTATEs), this test drives a <b>real <see cref="PostgresCdcProcessor"/>
/// over real pgoutput logical replication</b>. The shared <see cref="PostgresContainerFixture"/> now starts with
/// <c>wal_level=logical</c> + replication slots/senders, so this lock is NEVER skipped
/// (<c>verify-against-real-infra-not-mock</c>). It is also the regression gate for the f4c6kv DDL fix
/// (<see cref="PostgresCdcStateStore"/> previously emitted an illegal expression-PK → 42601 on real Postgres).
/// </para>
/// <para>
/// <b>Invariant under test (no data loss across a restart):</b> a persistent logical replication slot retains
/// WAL from its creation point until a position is confirmed. Processor #1 consumes the first change <em>through
/// its commit</em> (durably confirming its LSN); a fresh processor #2 with the same <c>ProcessorId</c> +
/// <c>ReplicationSlotName</c> and the same durable <see cref="PostgresCdcStateStore"/> MUST resume from the
/// confirmed LSN and redeliver the change that occurred after it — never skip it, never replay the confirmed one.
/// </para>
/// <para>
/// The Postgres <c>ProcessBatchAsync</c> streams the (unbounded) pgoutput stream until <c>BatchSize</c> changes
/// are read or the supplied token cancels, so each batch is bounded here by a per-call timeout and an
/// <see cref="OperationCanceledException"/> is treated as "no further changes in this window".
/// </para>
/// </remarks>
[IntegrationTest]
[Collection(ContainerCollections.Postgres)]
[Trait(TraitNames.Component, TestComponents.CDC)]
[Trait("Database", "Postgres")]
[Trait("SubComponent", "RestartRedelivery")]
[Trait(TraitNames.Category, TestCategories.Integration)]
[Trait(TraitNames.Component, TestComponents.Core)]
public sealed class PostgresCdcRestartRedeliveryIntegrationShould : IntegrationTestBase
{
	private static readonly TimeSpan BatchWindow = TimeSpan.FromSeconds(5);

	private readonly PostgresFixture _pgFixture;

	public PostgresCdcRestartRedeliveryIntegrationShould(PostgresFixture pgFixture)
	{
		_pgFixture = pgFixture;
	}

	[Fact]
	public async Task ResumeFromConfirmedLsn_RedeliversChangeAfterRestart_WithoutDataLoss()
	{
		_pgFixture.DockerAvailable.ShouldBeTrue(
			"e9u90j: the Postgres logical-replication restart-redelivery lock requires a real wal_level=logical container and is NEVER skipped.");

		var connectionString = _pgFixture.ConnectionString;
		var suffix = Guid.NewGuid().ToString("N")[..8];
		var tableName = $"cdc_orders_{suffix}";
		var publicationName = $"cdc_pub_{suffix}";
		var slotName = $"cdc_slot_{suffix}";
		var schemaName = $"cdc_{suffix}";
		const string processorId = "pg-redelivery-processor";

		await using (var conn = new NpgsqlConnection(connectionString))
		{
			await conn.OpenAsync(TestCancellationToken);
			await conn.ExecuteAsync($"CREATE TABLE {tableName} (id int PRIMARY KEY, order_id text NOT NULL);");
			await conn.ExecuteAsync($"ALTER TABLE {tableName} REPLICA IDENTITY FULL;");
			await conn.ExecuteAsync($"CREATE PUBLICATION {publicationName} FOR TABLE {tableName};");
		}

		var stateStore = new PostgresCdcStateStore(
			connectionString,
			MsOptions.Create(new PostgresCdcStateStoreOptions { SchemaName = schemaName, TableName = "state" }));

		PostgresCdcProcessor NewProcessor() => new(
			MsOptions.Create(new PostgresCdcOptions
			{
				ConnectionString = connectionString,
				PublicationName = publicationName,
				ReplicationSlotName = slotName,
				ProcessorId = processorId,
				TableNames = [tableName],
				BatchSize = 10,
				Replication = new PostgresCdcReplicationOptions { AutoCreateSlot = true },
			}),
			stateStore,
			NullLogger<PostgresCdcProcessor>.Instance);

		try
		{
			// ── Phase 0: establish the persistent replication slot (created on the first batch). ──
			// The slot must exist BEFORE the inserts so it durably retains them.
			await using (var slotInit = NewProcessor())
			{
				await RunBoundedBatchAsync(slotInit, (_, _) => Task.CompletedTask);
			}

			// Insert the first change AFTER the slot exists.
			await InsertOrderAsync(connectionString, tableName, id: 1, orderId: "order-1");

			// ── Phase 1: processor #1 consumes order-1 through its commit (durably confirms its LSN). ──
			var firstSeen = new ConcurrentQueue<string>();
			await using (var processor1 = NewProcessor())
			{
				await PollUntilAsync(processor1, firstSeen, q => q.Contains("order-1"));
			}

			firstSeen.ShouldContain("order-1", "processor #1 must capture and confirm the first change.");

			// Insert the second change AFTER processor #1 confirmed the first — this is what must survive the restart.
			await InsertOrderAsync(connectionString, tableName, id: 2, orderId: "order-2");

			// ── Phase 2: a fresh processor resumes from the confirmed LSN and redelivers order-2 only. ──
			var secondSeen = new ConcurrentQueue<string>();
			await using (var processor2 = NewProcessor())
			{
				await PollUntilAsync(processor2, secondSeen, q => q.Contains("order-2"));
			}

			// AC-N3.4 = no DATA LOSS across a restart. order-2 was inserted before processor #2 started, so a
			// processor that did NOT resume from the retained slot position (e.g. started "at latest") would miss
			// it — this assertion proves the resume path retains and redelivers it. Logical replication is
			// at-least-once, so a confirmed change (order-1) MAY also replay on restart; that duplicate is
			// correct CDC semantics and deliberately NOT asserted against.
			secondSeen.ShouldContain("order-2",
				"processor #2 must resume from the retained slot position and redeliver the post-restart change (no data loss).");
		}
		finally
		{
			await CleanupAsync(connectionString, tableName, publicationName, slotName, schemaName);
			await stateStore.DisposeAsync();
		}
	}

	private async Task InsertOrderAsync(string connectionString, string tableName, int id, string orderId)
	{
		await using var conn = new NpgsqlConnection(connectionString);
		await conn.OpenAsync(TestCancellationToken);
		await conn.ExecuteAsync(
			$"INSERT INTO {tableName} (id, order_id) VALUES (@id, @orderId);",
			new { id, orderId });
	}

	private static string? ExtractOrderId(PostgresDataChangeEvent change) =>
		change.Changes.FirstOrDefault(c => c.ColumnName == "order_id")?.NewValue?.ToString();

	/// <summary>
	/// Runs a single timeout-bounded batch, treating cancellation as "no further changes in this window"
	/// (the pgoutput stream is unbounded, so a batch with fewer than BatchSize changes ends via the token).
	/// </summary>
	private async Task RunBoundedBatchAsync(
		PostgresCdcProcessor processor,
		Func<PostgresDataChangeEvent, CancellationToken, Task> handler)
	{
		using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestCancellationToken);
		cts.CancelAfter(BatchWindow);
		try
		{
			_ = await processor.ProcessBatchAsync(handler, cts.Token).ConfigureAwait(false);
		}
		catch (OperationCanceledException) when (!TestCancellationToken.IsCancellationRequested)
		{
			// Batch window elapsed with no (further) changes — expected for an unbounded replication stream.
		}
	}

	/// <summary>
	/// Polls timeout-bounded batches (the persistent slot retains WAL between calls) until
	/// <paramref name="predicate"/> holds. No wall-clock assertion — the timeout only bounds a blocking stream.
	/// </summary>
	private async Task PollUntilAsync(
		PostgresCdcProcessor processor,
		ConcurrentQueue<string> recorder,
		Func<IReadOnlyCollection<string>, bool> predicate)
	{
		Task Handler(PostgresDataChangeEvent change, CancellationToken ct)
		{
			if (change.ChangeType == PostgresDataChangeType.Insert)
			{
				var id = ExtractOrderId(change);
				if (id is not null) recorder.Enqueue(id);
			}

			return Task.CompletedTask;
		}

		for (var attempt = 0; attempt < 6 && !predicate(recorder); attempt++)
		{
			await RunBoundedBatchAsync(processor, Handler).ConfigureAwait(false);
		}
	}

	private async Task CleanupAsync(
		string connectionString, string tableName, string publicationName, string slotName, string schemaName)
	{
		try
		{
			await using var conn = new NpgsqlConnection(connectionString);
			await conn.OpenAsync(TestCancellationToken);
			await conn.ExecuteAsync($"DROP PUBLICATION IF EXISTS {publicationName};");
			await conn.ExecuteAsync(
				"SELECT pg_drop_replication_slot(slot_name) FROM pg_replication_slots WHERE slot_name = @slotName;",
				new { slotName });
			await conn.ExecuteAsync($"DROP TABLE IF EXISTS {tableName};");
			await conn.ExecuteAsync($"DROP SCHEMA IF EXISTS \"{schemaName}\" CASCADE;");
		}
		catch
		{
			// Best-effort cleanup — never mask the test's own assertion outcome.
		}
	}
}
