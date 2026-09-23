// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Collections.Concurrent;

using Dapper;

using Excalibur.Cdc.Postgres;

using Microsoft.Extensions.Logging.Abstractions;

using Npgsql;

using Shouldly;

using Tests.Shared;
using Tests.Shared.Categories;
using Tests.Shared.Fixtures;
using Tests.Shared.Infrastructure;

using MsOptions = Microsoft.Extensions.Options.Options;

namespace Excalibur.Dispatch.Integration.Tests.DispatchCore.Providers.Postgres;

/// <summary>
/// Genuine, NON-SKIPPED real-infra locks for multi-table <c>TRUNCATE</c> over Postgres logical replication
/// (bd-jvta8r — CDC multi-relation truncate data loss).
/// </summary>
/// <remarks>
/// <para>
/// <b>Why real infrastructure:</b> the defect is a property of the pgoutput <em>protocol encoding</em> —
/// PostgreSQL represents <c>TRUNCATE a, b</c> as a SINGLE replication message naming every truncated
/// relation. A mocked message source returns whatever it was told to return and therefore cannot exhibit
/// the encoding at all. These arms drive a real <see cref="PostgresCdcProcessor"/> against real
/// <c>wal_level=logical</c> replication on the shared <see cref="PostgresContainerFixture"/>, and are
/// NEVER skip-gated (<c>verify-against-real-infra-not-mock</c>).
/// </para>
/// <para>
/// <b>Safety property under test:</b> <em>no durable checkpoint may advance past an unhandled relation.</em>
/// Before the fix, the processor emitted one change for <c>Relations[0]</c> only and then confirmed the
/// transaction's commit — checkpointing past every later relation. When only a later relation matched the
/// configured table filter, the single emitted change was filtered out and NO truncate was delivered at all,
/// while the checkpoint still advanced.
/// </para>
/// <para>
/// <b>Paired liveness:</b> "the checkpoint never advances past unhandled work" is trivially satisfied by a
/// processor that never checkpoints and never delivers anything. Every safety arm below is therefore paired
/// with a liveness assertion that the changes ARE delivered and the durable position DOES advance once all
/// relations resolve (<c>testing-patterns §3</c>).
/// </para>
/// </remarks>
[IntegrationTest]
[Collection(ContainerCollections.Postgres)]
[Trait(TraitNames.Component, TestComponents.CDC)]
[Trait("Database", "Postgres")]
[Trait("SubComponent", "MultiTableTruncate")]
[Trait(TraitNames.Category, TestCategories.Integration)]
[Trait(TraitNames.Component, TestComponents.Core)]
public sealed class PostgresCdcMultiTableTruncateIntegrationShould : IntegrationTestBase
{
	private static readonly TimeSpan BatchWindow = TimeSpan.FromSeconds(4);

	private readonly PostgresFixture _pgFixture;

	public PostgresCdcMultiTableTruncateIntegrationShould(PostgresFixture pgFixture)
	{
		_pgFixture = pgFixture;
	}

	// Several bounded replication windows per arm; the default integration timeout is too tight.
	protected override TimeSpan TestTimeout => TimeSpan.FromMinutes(4);

	/// <summary>
	/// Multi-table TRUNCATE, batch path: every truncated relation must be delivered — not only the first
	/// relation named in the message — and the durable position must advance once they all resolve.
	/// </summary>
	[Fact]
	public async Task DeliverATruncateChangeForEveryRelationOfAMultiTableTruncate_OnTheBatchPath()
	{
		RequireRealPostgres();

		var env = await CreateEnvironmentAsync(tables: 2, configured: null);
		try
		{
			await env.EstablishSlotAsync();
			await env.ExecuteAsync($"INSERT INTO {env.Tables[0]} (id, payload) VALUES (1, 'seed');");
			await env.DrainAsync();

			var confirmedBeforeTruncate = await env.ReadDurablePositionAsync();

			await env.ExecuteAsync($"TRUNCATE {env.Tables[0]}, {env.Tables[1]};");

			var recorder = new TruncateRecorder();
			await env.PollUntilAsync(recorder, r => r.Tables.Count >= 2);

			// The defect: only Relations[0] was ever emitted, so this saw exactly one table.
			recorder.Tables.ShouldBe(
				[$"public.{env.Tables[0]}", $"public.{env.Tables[1]}"],
				ignoreOrder: true,
				"every relation named in a multi-table TRUNCATE must be delivered as its own truncate change.");

			// Transaction identity and commit time are preserved across the fan-out: one transactional act.
			recorder.TransactionIds.Distinct().Count().ShouldBe(1,
				"all relations of one TRUNCATE belong to a single transaction and must carry its id.");
			recorder.TransactionIds[0].ShouldNotBe(0u, "the transaction id must come from the BEGIN, not a default.");
			recorder.CommitTimes.Distinct().Count().ShouldBe(1,
				"all relations of one TRUNCATE share the transaction commit time.");
			recorder.Schemas.Distinct().ShouldBe(["public"], "the schema of each relation must be preserved.");

			// Liveness partner: the checkpoint MUST advance once every relation resolved — otherwise the
			// safety assertion above would also be satisfied by a processor that never checkpoints.
			var confirmedAfter = await env.ReadDurablePositionAsync();
			confirmedAfter.ShouldBeGreaterThan(confirmedBeforeTruncate,
				"with every relation handled, the durable checkpoint must advance past the truncate transaction.");
		}
		finally
		{
			await env.DisposeAsync();
		}
	}

	/// <summary>
	/// The half of the defect that delivered NOTHING: when the only configured table is named after the
	/// first relation, filtering the single emitted change dropped the truncate entirely.
	/// </summary>
	[Fact]
	public async Task DeliverTheTruncate_WhenOnlyARelationAfterTheFirstMatchesTheConfiguredTables()
	{
		RequireRealPostgres();

		// Both tables are published (so the message names both) but only the SECOND is configured.
		var env = await CreateEnvironmentAsync(tables: 2, configured: t => [t[1]]);
		try
		{
			await env.EstablishSlotAsync();
			await env.ExecuteAsync($"INSERT INTO {env.Tables[1]} (id, payload) VALUES (1, 'seed');");
			await env.DrainAsync();

			var confirmedBeforeTruncate = await env.ReadDurablePositionAsync();

			await env.ExecuteAsync($"TRUNCATE {env.Tables[0]}, {env.Tables[1]};");

			var recorder = new TruncateRecorder();
			await env.PollUntilAsync(recorder, r => r.Tables.Count >= 1);

			// The defect delivered zero truncate changes here: Relations[0] was the UNCONFIGURED table, the
			// filter rejected it, and the commit still advanced the checkpoint past the configured one.
			recorder.Tables.ShouldBe([$"public.{env.Tables[1]}"],
				"a truncate must be delivered for a configured table even when it is not the first relation named.");

			var confirmedAfter = await env.ReadDurablePositionAsync();
			confirmedAfter.ShouldBeGreaterThan(confirmedBeforeTruncate,
				"liveness: the checkpoint must still advance once the configured relation was handled.");
		}
		finally
		{
			await env.DisposeAsync();
		}
	}

	/// <summary>
	/// CASCADE reaches tables the statement never named; they arrive in the SAME truncate message and must
	/// each be delivered.
	/// </summary>
	[Fact]
	public async Task DeliverATruncateChangeForEveryRelationReachedByCascade()
	{
		RequireRealPostgres();

		var env = await CreateEnvironmentAsync(tables: 2, configured: null);
		try
		{
			// tables[1] references tables[0]; truncating the parent with CASCADE truncates the child too,
			// and pgoutput names both relations in one message.
			await env.ExecuteAsync(
				$"ALTER TABLE {env.Tables[1]} ADD COLUMN parent_id int REFERENCES {env.Tables[0]} (id);");

			await env.EstablishSlotAsync();
			await env.ExecuteAsync($"INSERT INTO {env.Tables[0]} (id, payload) VALUES (1, 'seed');");
			await env.DrainAsync();

			var confirmedBeforeTruncate = await env.ReadDurablePositionAsync();

			// Only the PARENT is named; the child is reached by CASCADE.
			await env.ExecuteAsync($"TRUNCATE {env.Tables[0]} CASCADE;");

			var recorder = new TruncateRecorder();
			await env.PollUntilAsync(recorder, r => r.Tables.Count >= 2);

			recorder.Tables.ShouldBe(
				[$"public.{env.Tables[0]}", $"public.{env.Tables[1]}"],
				ignoreOrder: true,
				"a CASCADE truncate must deliver a change for every relation it reached, not only the named one.");

			var confirmedAfter = await env.ReadDurablePositionAsync();
			confirmedAfter.ShouldBeGreaterThan(confirmedBeforeTruncate,
				"liveness: the checkpoint must advance once every cascaded relation was handled.");
		}
		finally
		{
			await env.DisposeAsync();
		}
	}

	/// <summary>
	/// The invariant itself: a handler that fails on a LATER relation must leave the durable checkpoint
	/// behind the truncate, and a restart from that checkpoint must redeliver every relation — none skipped.
	/// </summary>
	[Fact]
	public async Task NotAdvanceTheDurableCheckpointWhenALaterRelationFails_AndRedeliverEveryRelationOnRestart()
	{
		RequireRealPostgres();

		var env = await CreateEnvironmentAsync(tables: 2, configured: null);
		try
		{
			await env.EstablishSlotAsync();
			await env.ExecuteAsync($"INSERT INTO {env.Tables[0]} (id, payload) VALUES (1, 'seed');");
			await env.DrainAsync();

			var confirmedBeforeTruncate = await env.ReadDurablePositionAsync();
			confirmedBeforeTruncate.IsValid.ShouldBeTrue(
				"the seed insert must have produced a durable checkpoint to compare against.");

			await env.ExecuteAsync($"TRUNCATE {env.Tables[0]}, {env.Tables[1]};");

			// ── Phase 1: fail on the SECOND truncate change handed over. ──
			var seenBeforeFailure = new ConcurrentQueue<string>();
			var failure = await Should.ThrowAsync<InvalidOperationException>(async () =>
			{
				await using var processor = env.NewProcessor();
				await env.RunBatchAsync(processor, (change, _) =>
				{
					if (change.ChangeType != PostgresDataChangeType.Truncate)
					{
						return Task.CompletedTask;
					}

					seenBeforeFailure.Enqueue(change.FullTableName);
					return seenBeforeFailure.Count >= 2
						? throw new InvalidOperationException("handler failed on a later truncate relation")
						: Task.CompletedTask;
				});
			});

			failure.Message.ShouldContain("later truncate relation");
			seenBeforeFailure.Count.ShouldBe(2,
				"the processor must have reached the second relation — otherwise this arm proves nothing about it.");

			// SAFETY: the transaction was never confirmed, so the durable checkpoint is still behind it.
			var confirmedAfterFailure = await env.ReadDurablePositionAsync();
			confirmedAfterFailure.ShouldBe(confirmedBeforeTruncate,
				"a handler failure on a later relation must NOT advance the durable checkpoint past the truncate.");

			// ── Phase 2: restart from the confirmed position — nothing may be skipped. ──
			var recorder = new TruncateRecorder();
			await env.PollUntilAsync(recorder, r => r.Tables.Count >= 2);

			recorder.Tables.ShouldBe(
				[$"public.{env.Tables[0]}", $"public.{env.Tables[1]}"],
				ignoreOrder: true,
				"a restart from the confirmed position must redeliver every relation of the failed truncate.");

			// LIVENESS: having now resolved every relation, the checkpoint must move forward.
			var confirmedAfterRecovery = await env.ReadDurablePositionAsync();
			confirmedAfterRecovery.ShouldBeGreaterThan(confirmedBeforeTruncate,
				"once every relation resolved, the durable checkpoint must advance — the stream must make progress.");
		}
		finally
		{
			await env.DisposeAsync();
		}
	}

	/// <summary>
	/// The continuous <c>StartAsync</c> path carries the same fan-out as the batch path — the two loops must
	/// not diverge.
	/// </summary>
	[Fact]
	public async Task DeliverATruncateChangeForEveryRelation_OnTheContinuousPath()
	{
		RequireRealPostgres();

		var env = await CreateEnvironmentAsync(tables: 2, configured: null);
		try
		{
			await env.EstablishSlotAsync();
			await env.ExecuteAsync($"INSERT INTO {env.Tables[0]} (id, payload) VALUES (1, 'seed');");
			await env.DrainAsync();

			var confirmedBeforeTruncate = await env.ReadDurablePositionAsync();

			await env.ExecuteAsync($"TRUNCATE {env.Tables[0]}, {env.Tables[1]};");

			var recorder = new TruncateRecorder();
			using var runCts = CancellationTokenSource.CreateLinkedTokenSource(TestCancellationToken);
			runCts.CancelAfter(TimeSpan.FromSeconds(30));

			bool progressed;
			await using (var processor = env.NewProcessor())
			{
				var streaming = processor.StartAsync(
					(change, cancellation) =>
					{
						// Cancellation is driven by runCts — the very token handed to StartAsync — so the
						// per-change token this lambda receives is not consulted here.
						_ = cancellation;

						if (change.ChangeType == PostgresDataChangeType.Truncate)
						{
							recorder.Record(change);
						}

						return Task.CompletedTask;
					},
					runCts.Token);

				// Stop only once BOTH relations arrived AND the checkpoint moved. The transaction's COMMIT
				// message follows the truncate message and the durable confirm happens there, so cancelling
				// the instant the last relation is handed over would abort before the confirm — a race in
				// the test, not in the processor. This polls a condition; it is never a wall-clock wait.
				progressed = await WaitHelpers.WaitUntilAsync(
					async () => recorder.Tables.Count >= 2
						&& await env.ReadDurablePositionAsync().ConfigureAwait(false) > confirmedBeforeTruncate,
					TimeSpan.FromSeconds(25),
					TimeSpan.FromMilliseconds(250),
					TestCancellationToken).ConfigureAwait(false);

				await runCts.CancelAsync();
				try
				{
					await streaming.ConfigureAwait(false);
				}
				catch (OperationCanceledException)
				{
					// Expected: the continuous loop ends only by cancellation.
				}
			}

			progressed.ShouldBeTrue(
				"the continuous path must deliver every relation AND durably confirm the truncate transaction.");

			recorder.Tables.ShouldBe(
				[$"public.{env.Tables[0]}", $"public.{env.Tables[1]}"],
				ignoreOrder: true,
				"the continuous StartAsync path must deliver every relation of a multi-table TRUNCATE too.");

			var confirmedAfter = await env.ReadDurablePositionAsync();
			confirmedAfter.ShouldBeGreaterThan(confirmedBeforeTruncate,
				"liveness: the continuous path must also advance the durable checkpoint once all relations resolved.");
		}
		finally
		{
			await env.DisposeAsync();
		}
	}

	private void RequireRealPostgres() =>
		_pgFixture.DockerAvailable.ShouldBeTrue(
			"bd-jvta8r: the multi-table TRUNCATE locks require a real wal_level=logical Postgres container "
			+ "(the defect lives in the pgoutput message encoding, which no mock can exhibit) and are NEVER skipped.");

	private async Task<TruncateEnvironment> CreateEnvironmentAsync(
		int tables,
		Func<IReadOnlyList<string>, string[]>? configured)
	{
		var suffix = Guid.NewGuid().ToString("N")[..8];
		var names = Enumerable.Range(0, tables).Select(i => $"cdc_trunc_{suffix}_{i}").ToArray();
		var env = new TruncateEnvironment(
			_pgFixture.ConnectionString,
			names,
			publicationName: $"cdc_trunc_pub_{suffix}",
			slotName: $"cdc_trunc_slot_{suffix}",
			stateSchema: $"cdc_trunc_{suffix}",
			processorId: $"pg-truncate-{suffix}",
			configuredTables: configured?.Invoke(names) ?? names,
			batchWindow: BatchWindow,
			testToken: TestCancellationToken);

		await env.CreateSchemaAsync();
		return env;
	}

	/// <summary>Records the truncate changes handed to the event handler.</summary>
	private sealed class TruncateRecorder
	{
		private readonly List<string> _tables = [];
		private readonly List<uint> _transactionIds = [];
		private readonly List<DateTimeOffset> _commitTimes = [];
		private readonly List<string> _schemas = [];

		public IReadOnlyList<string> Tables => _tables;

		public IReadOnlyList<uint> TransactionIds => _transactionIds;

		public IReadOnlyList<DateTimeOffset> CommitTimes => _commitTimes;

		public IReadOnlyList<string> Schemas => _schemas;

		public void Record(PostgresDataChangeEvent change)
		{
			// Logical replication is at-least-once: a redelivered relation is correct, not a defect. Record
			// the DISTINCT set so a duplicate never manufactures a pass for a relation that was never sent.
			if (_tables.Contains(change.FullTableName, StringComparer.Ordinal))
			{
				return;
			}

			_tables.Add(change.FullTableName);
			_transactionIds.Add(change.TransactionId);
			_commitTimes.Add(change.CommitTime);
			_schemas.Add(change.SchemaName);
		}
	}

	/// <summary>Owns the per-arm Postgres objects (tables, publication, slot, state store) and their cleanup.</summary>
	private sealed class TruncateEnvironment(
		string connectionString,
		string[] tables,
		string publicationName,
		string slotName,
		string stateSchema,
		string processorId,
		string[] configuredTables,
		TimeSpan batchWindow,
		CancellationToken testToken) : IAsyncDisposable
	{
		private readonly PostgresCdcStateStore _stateStore = new(
			connectionString,
			MsOptions.Create(new PostgresCdcStateStoreOptions { SchemaName = stateSchema, TableName = "state" }));

		public IReadOnlyList<string> Tables => tables;

		public async Task CreateSchemaAsync()
		{
			await using var conn = new NpgsqlConnection(connectionString);
			await conn.OpenAsync(testToken);

			foreach (var table in tables)
			{
				await conn.ExecuteAsync($"CREATE TABLE {table} (id int PRIMARY KEY, payload text NOT NULL);");
				await conn.ExecuteAsync($"ALTER TABLE {table} REPLICA IDENTITY FULL;");
			}

			// Every table is PUBLISHED so the truncate message names them all; the processor's TableNames
			// filter is what varies per arm.
			await conn.ExecuteAsync($"CREATE PUBLICATION {publicationName} FOR TABLE {string.Join(", ", tables)};");
		}

		public async Task ExecuteAsync(string sql)
		{
			await using var conn = new NpgsqlConnection(connectionString);
			await conn.OpenAsync(testToken);
			await conn.ExecuteAsync(sql);
		}

		public PostgresCdcProcessor NewProcessor() => new(
			MsOptions.Create(new PostgresCdcOptions
			{
				ConnectionString = connectionString,
				PublicationName = publicationName,
				ReplicationSlotName = slotName,
				ProcessorId = processorId,
				TableNames = configuredTables,
				BatchSize = 50,
				Replication = new PostgresCdcReplicationOptions { AutoCreateSlot = true },
			}),
			_stateStore,
			NullLogger<PostgresCdcProcessor>.Instance);

		/// <summary>Creates the persistent slot so it retains the WAL of everything that follows.</summary>
		public async Task EstablishSlotAsync()
		{
			await using var slotInit = NewProcessor();
			await RunBatchAsync(slotInit, (_, _) => Task.CompletedTask, swallowCancellation: true);
		}

		/// <summary>Consumes and confirms everything currently pending, so a later position comparison is meaningful.</summary>
		public async Task DrainAsync()
		{
			for (var attempt = 0; attempt < 3; attempt++)
			{
				await using var processor = NewProcessor();
				await RunBatchAsync(processor, (_, _) => Task.CompletedTask, swallowCancellation: true);
			}
		}

		public async Task<PostgresCdcPosition> ReadDurablePositionAsync() =>
			await _stateStore.GetLastPositionAsync(processorId, slotName, testToken).ConfigureAwait(false);

		/// <summary>
		/// Runs one timeout-bounded batch. The pgoutput stream is unbounded, so a batch with fewer than
		/// BatchSize changes ends via the token; a handler fault propagates (it is the subject of an arm).
		/// </summary>
		public async Task RunBatchAsync(
			PostgresCdcProcessor processor,
			Func<PostgresDataChangeEvent, CancellationToken, Task> handler,
			bool swallowCancellation = true)
		{
			using var cts = CancellationTokenSource.CreateLinkedTokenSource(testToken);
			cts.CancelAfter(batchWindow);
			try
			{
				_ = await processor.ProcessBatchAsync(handler, cts.Token).ConfigureAwait(false);
			}
			catch (OperationCanceledException) when (swallowCancellation && !testToken.IsCancellationRequested)
			{
				// Batch window elapsed with no (further) changes — expected for an unbounded stream.
			}
		}

		/// <summary>Polls bounded batches (the persistent slot retains WAL between calls) until the predicate holds.</summary>
		public async Task PollUntilAsync(TruncateRecorder recorder, Func<TruncateRecorder, bool> predicate)
		{
			Task Handler(PostgresDataChangeEvent change, CancellationToken ct)
			{
				if (change.ChangeType == PostgresDataChangeType.Truncate)
				{
					recorder.Record(change);
				}

				return Task.CompletedTask;
			}

			for (var attempt = 0; attempt < 6 && !predicate(recorder); attempt++)
			{
				await using var processor = NewProcessor();
				await RunBatchAsync(processor, Handler).ConfigureAwait(false);
			}
		}

		public async ValueTask DisposeAsync()
		{
			try
			{
				await using var conn = new NpgsqlConnection(connectionString);
				await conn.OpenAsync(testToken);
				await conn.ExecuteAsync($"DROP PUBLICATION IF EXISTS {publicationName};");
				await conn.ExecuteAsync(
					"SELECT pg_drop_replication_slot(slot_name) FROM pg_replication_slots WHERE slot_name = @slotName;",
					new { slotName });

				foreach (var table in tables)
				{
					await conn.ExecuteAsync($"DROP TABLE IF EXISTS {table} CASCADE;");
				}

				await conn.ExecuteAsync($"DROP SCHEMA IF EXISTS \"{stateSchema}\" CASCADE;");
			}
			catch
			{
				// Best-effort cleanup — never mask the arm's own assertion outcome.
			}

			await _stateStore.DisposeAsync().ConfigureAwait(false);
		}
	}
}
