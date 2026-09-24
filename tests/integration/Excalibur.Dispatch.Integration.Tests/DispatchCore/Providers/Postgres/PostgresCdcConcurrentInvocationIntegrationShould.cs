// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

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
/// Real-infra locks for overlapping invocations of the singleton Postgres CDC processor, and for the
/// monotonicity of its durable checkpoint.
/// </summary>
/// <remarks>
/// <para>
/// <b>Safety property.</b> Two callers may not interleave one processor's per-stream state. The processor
/// is registered <c>TryAddSingleton</c> while the transaction identity, commit time, confirmed position and
/// replication connection are plain instance fields, and the batch API is documented for a serverless timer
/// trigger where overlapping invocations are ordinary. Interleaved, caller A's changes are stamped with
/// caller B's transaction identity, and the durable checkpoint can advance past changes whose handler never
/// ran.
/// </para>
/// <para>
/// <b>Why the overlap is forced rather than raced.</b> Two tasks started together and awaited with
/// <c>Task.WhenAll</c> would go GREEN on broken code most runs: whichever call finishes first releases the
/// state before the second touches it, so the interleaving simply does not occur, and the arm then reports
/// the absence of a schedule rather than the presence of a guarantee. These arms instead BLOCK the first
/// call inside its handler on a gate and only then start the second, so the overlap is a fact of the test
/// rather than a hope about the scheduler.
/// </para>
/// <para>
/// <b>Paired liveness.</b> "A second concurrent call is refused" is trivially satisfied by a processor that
/// refuses every call forever. The safety arm is therefore paired with an arm proving the claim is RELEASED
/// when the first call completes, and with one proving a legitimate checkpoint advance is not refused.
/// </para>
/// </remarks>
[IntegrationTest]
[Collection(ContainerCollections.Postgres)]
[Trait(TraitNames.Component, TestComponents.CDC)]
[Trait("Database", "Postgres")]
[Trait("SubComponent", "ConcurrentInvocation")]
[Trait(TraitNames.Category, TestCategories.Integration)]
[Trait(TraitNames.Component, TestComponents.Core)]
public sealed class PostgresCdcConcurrentInvocationIntegrationShould : IntegrationTestBase
{
	private static readonly TimeSpan BatchWindow = TimeSpan.FromSeconds(4);
	private static readonly TimeSpan GateWindow = TimeSpan.FromSeconds(30);

	private readonly PostgresFixture _pgFixture;

	public PostgresCdcConcurrentInvocationIntegrationShould(PostgresFixture pgFixture) => _pgFixture = pgFixture;

	/// <summary>
	/// SAFETY. While one ProcessBatchAsync is inside its replication loop, a second call on the same
	/// instance does not touch the stream — it returns 0 immediately. RED against the pre-fix processor,
	/// whose connection setup is a bare null-check: both callers proceed, enumerate the replication stream
	/// on the SAME connection, and overwrite each other's transaction identity.
	/// <para>
	/// The loser RETURNS rather than throwing, and this arm binds that deliberately. The realistic
	/// collision is one timer tick overlapping the previous under load — the shape of the published
	/// timer-trigger example — so an exception here would make that example fail under exactly the load it
	/// exists to handle, surfacing as an unobserved task exception in most hosts. An arm asserting a throw
	/// would have locked in that regression.
	/// </para>
	/// </summary>
	[Fact]
	public async Task ReturnZeroFromASecondOverlappingProcessBatchCallWithoutTouchingTheStream()
	{
		RequireRealPostgres();
		await using var env = await CreateEnvironmentAsync().ConfigureAwait(false);
		await env.EstablishSlotAsync().ConfigureAwait(false);
		await env.InsertRowAsync("first").ConfigureAwait(false);

		var handlerEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		var releaseHandler = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

		await using var processor = env.NewProcessor();

		// Caller A parks INSIDE the replication loop, so the overlap below is guaranteed, not raced.
		var callerA = Task.Run(
			async () => await env.RunBatchAsync(
				processor,
				async (_, _) =>
				{
					_ = handlerEntered.TrySetResult();
					await releaseHandler.Task.ConfigureAwait(false);
				}).ConfigureAwait(false),
			TestCancellationToken);

		var entered = await Task.WhenAny(handlerEntered.Task, Task.Delay(GateWindow, TestCancellationToken))
			.ConfigureAwait(false);
		entered.ShouldBe(
			handlerEntered.Task,
			"caller A never reached its handler, so no overlap was established and this arm would be vacuous");

		// Caller B, while A provably holds the processor. It must return without delivering anything,
		// and it must NOT throw — a throw out of a timer callback is the regression this shape avoids.
		var handledByB = 0;
		var bResult = await processor.ProcessBatchAsync(
				(_, _) => { handledByB++; return Task.CompletedTask; },
				TestCancellationToken)
			.ConfigureAwait(false);

		bResult.ShouldBe(0, "the overlapping caller must report processing nothing");
		handledByB.ShouldBe(0, "the overlapping caller must not touch the stream at all");

		_ = releaseHandler.TrySetResult();
		_ = await callerA.ConfigureAwait(false);
	}

	/// <summary>
	/// LIVENESS. The claim taken by the first call is RELEASED when it completes, so a later sequential
	/// call succeeds. Without this, a processor that refused every call after the first would satisfy the
	/// safety arm above and be indistinguishable from a correct one.
	/// </summary>
	[Fact]
	public async Task AllowASequentialCallAfterTheFirstCompletes()
	{
		RequireRealPostgres();
		await using var env = await CreateEnvironmentAsync().ConfigureAwait(false);
		await env.EstablishSlotAsync().ConfigureAwait(false);
		await env.InsertRowAsync("sequential").ConfigureAwait(false);

		await using var processor = env.NewProcessor();

		_ = await env.RunBatchAsync(processor, (_, _) => Task.CompletedTask).ConfigureAwait(false);

		// The same instance must be reusable once the first loop has returned.
		var secondCall = await Record.ExceptionAsync(
				() => env.RunBatchAsync(processor, (_, _) => Task.CompletedTask))
			.ConfigureAwait(false);

		secondCall.ShouldBeNull("the single-entry claim was not released after the first call returned");
	}

	/// <summary>
	/// SAFETY. The durable checkpoint never moves backwards: saving a position BELOW the stored one is a
	/// no-op rather than a regression. A regressed checkpoint re-delivers, and the same last-writer-wins
	/// upsert that permits it is what lets an overlapping caller confirm past another's undelivered work.
	/// </summary>
	[Fact]
	public async Task RefuseACheckpointThatWouldMoveThePositionBackwards()
	{
		RequireRealPostgres();
		await using var env = await CreateEnvironmentAsync().ConfigureAwait(false);

		await env.SavePositionAsync(new PostgresCdcPosition("0/2000000")).ConfigureAwait(false);
		await env.SavePositionAsync(new PostgresCdcPosition("0/1000000")).ConfigureAwait(false);

		var stored = await env.ReadDurablePositionAsync().ConfigureAwait(false);

		stored.LsnString.ShouldBe("0/2000000", "a lower position was allowed to regress the checkpoint");
	}

	/// <summary>
	/// LIVENESS, and the arm that catches the OBVIOUS-BUT-WRONG implementation of the arm above.
	/// <para>
	/// The column is <c>VARCHAR(32)</c> and an LSN renders as <c>%X/%X</c> with no zero padding, so a
	/// monotonicity guard written as a plain <c>EXCLUDED.position &gt; position</c> compares LEXICALLY:
	/// <c>0/9</c> sorts ABOVE <c>0/10</c> although <c>0x9</c> is numerically BELOW <c>0x10</c>. Such a
	/// guard refuses this legitimate advance and stalls the checkpoint permanently — a worse defect than
	/// the regression it was added to prevent, and one that every sequential test using tidy round numbers
	/// misses. This arm crosses a hex-digit boundary on purpose.
	/// </para>
	/// </summary>
	[Fact]
	public async Task AdvanceAcrossAHexDigitBoundaryWhereALexicalComparisonWouldRefuse()
	{
		RequireRealPostgres();
		await using var env = await CreateEnvironmentAsync().ConfigureAwait(false);

		await env.SavePositionAsync(new PostgresCdcPosition("0/9")).ConfigureAwait(false);
		await env.SavePositionAsync(new PostgresCdcPosition("0/10")).ConfigureAwait(false);

		var stored = await env.ReadDurablePositionAsync().ConfigureAwait(false);

		stored.LsnString.ShouldBe(
			"0/10",
			"0/10 is numerically above 0/9 and must advance; a lexical VARCHAR comparison refuses it");
	}

	/// <summary>
	/// SAFETY, and the direction the arm above does NOT cover. A commissioned review pointed out that my
	/// original pair tested only the case a lexical comparison wrongly REFUSES; this is the case it wrongly
	/// ACCEPTS, which is the data-loss direction.
	/// <para>
	/// Stored <c>10/0</c>, incoming <c>9/FFFFFFFF</c>. Numerically <c>0x9FFFFFFFF &lt; 0x1000000000</c>, so the
	/// write must be refused. Lexically <c>'9/FFFFFFFF' &gt; '10/0'</c> — the first character decides it — so a
	/// <c>VARCHAR</c> comparison ACCEPTS it and walks the durable checkpoint BACKWARDS onto WAL the slot has
	/// already released. The two arms are not redundant: one catches a stalled pipeline, this one catches
	/// silent loss, and a guard can be wrong in exactly one of the two directions.
	/// </para>
	/// </summary>
	[Fact]
	public async Task RefuseALowerPositionWhoseTextSortsAboveTheStoredOne()
	{
		RequireRealPostgres();
		await using var env = await CreateEnvironmentAsync().ConfigureAwait(false);

		await env.SavePositionAsync(new PostgresCdcPosition("10/0")).ConfigureAwait(false);
		await env.SavePositionAsync(new PostgresCdcPosition("9/FFFFFFFF")).ConfigureAwait(false);

		var stored = await env.ReadDurablePositionAsync().ConfigureAwait(false);

		stored.LsnString.ShouldBe(
			"10/0",
			"9/FFFFFFFF is numerically BELOW 10/0 and must be refused; a lexical VARCHAR comparison accepts it "
			+ "because '9' sorts above '1', regressing the checkpoint onto released WAL");
	}

	private void RequireRealPostgres() =>
		_pgFixture.DockerAvailable.ShouldBeTrue(
			"bd-areeqq: the overlapping-invocation and checkpoint-monotonicity locks require a real "
			+ "wal_level=logical Postgres container. The defect is a property of shared replication-stream "
			+ "state and of pg_lsn ordering in the upsert, neither of which a mock can exhibit. Never skipped.");

	private async Task<CdcEnvironment> CreateEnvironmentAsync()
	{
		var suffix = Guid.NewGuid().ToString("N")[..8];
		var env = new CdcEnvironment(
			_pgFixture.ConnectionString,
			table: $"cdc_conc_{suffix}",
			publicationName: $"cdc_conc_pub_{suffix}",
			slotName: $"cdc_conc_slot_{suffix}",
			stateSchema: $"cdc_conc_{suffix}",
			processorId: $"pg-conc-{suffix}",
			batchWindow: BatchWindow,
			testToken: TestCancellationToken);

		await env.CreateSchemaAsync().ConfigureAwait(false);
		return env;
	}

	/// <summary>Owns the per-arm Postgres objects (table, publication, slot, state store) and their cleanup.</summary>
	private sealed class CdcEnvironment(
		string connectionString,
		string table,
		string publicationName,
		string slotName,
		string stateSchema,
		string processorId,
		TimeSpan batchWindow,
		CancellationToken testToken) : IAsyncDisposable
	{
		private readonly PostgresCdcStateStore _stateStore = new(
			connectionString,
			MsOptions.Create(new PostgresCdcStateStoreOptions { SchemaName = stateSchema, TableName = "state" }));

		public async Task CreateSchemaAsync()
		{
			await using var conn = new NpgsqlConnection(connectionString);
			await conn.OpenAsync(testToken).ConfigureAwait(false);
			_ = await conn.ExecuteAsync(
					$"CREATE TABLE IF NOT EXISTS {table} (id SERIAL PRIMARY KEY, label TEXT NOT NULL);")
				.ConfigureAwait(false);
			_ = await conn.ExecuteAsync($"DROP PUBLICATION IF EXISTS {publicationName};").ConfigureAwait(false);
			_ = await conn.ExecuteAsync($"CREATE PUBLICATION {publicationName} FOR TABLE {table};")
				.ConfigureAwait(false);
		}

		public async Task InsertRowAsync(string label)
		{
			await using var conn = new NpgsqlConnection(connectionString);
			await conn.OpenAsync(testToken).ConfigureAwait(false);
			_ = await conn.ExecuteAsync($"INSERT INTO {table} (label) VALUES (@label);", new { label })
				.ConfigureAwait(false);
		}

		public PostgresCdcProcessor NewProcessor() => new(
			MsOptions.Create(new PostgresCdcOptions
			{
				ConnectionString = connectionString,
				PublicationName = publicationName,
				ReplicationSlotName = slotName,
				ProcessorId = processorId,
				TableNames = [table],
				BatchSize = 50,
				Replication = new PostgresCdcReplicationOptions { AutoCreateSlot = true },
			}),
			_stateStore,
			NullLogger<PostgresCdcProcessor>.Instance);

		/// <summary>Creates the persistent slot so it retains the WAL of everything that follows.</summary>
		public async Task EstablishSlotAsync()
		{
			await using var slotInit = NewProcessor();
			_ = await RunBatchAsync(slotInit, (_, _) => Task.CompletedTask).ConfigureAwait(false);
		}

		public Task SavePositionAsync(PostgresCdcPosition position) =>
			_stateStore.SavePositionAsync(processorId, slotName, position, testToken);

		public async Task<PostgresCdcPosition> ReadDurablePositionAsync() =>
			await _stateStore.GetLastPositionAsync(processorId, slotName, testToken).ConfigureAwait(false);

		/// <summary>
		/// Runs one timeout-bounded batch. The replication stream is unbounded, so a batch with fewer than
		/// BatchSize changes ends via the token. Returns the number of changes handed to the handler.
		/// </summary>
		public async Task<int> RunBatchAsync(
			PostgresCdcProcessor processor,
			Func<PostgresDataChangeEvent, CancellationToken, Task> handler)
		{
			using var cts = CancellationTokenSource.CreateLinkedTokenSource(testToken);
			cts.CancelAfter(batchWindow);
			try
			{
				return await processor.ProcessBatchAsync(handler, cts.Token).ConfigureAwait(false);
			}
			catch (OperationCanceledException) when (!testToken.IsCancellationRequested)
			{
				// Batch window elapsed with no (further) changes — expected for an unbounded stream.
				return 0;
			}
		}

		public async ValueTask DisposeAsync()
		{
			try
			{
				await using var conn = new NpgsqlConnection(connectionString);
				await conn.OpenAsync(testToken).ConfigureAwait(false);
				_ = await conn.ExecuteAsync(
						"SELECT pg_drop_replication_slot(slot_name) FROM pg_replication_slots WHERE slot_name = @slotName;",
						new { slotName })
					.ConfigureAwait(false);
				_ = await conn.ExecuteAsync($"DROP PUBLICATION IF EXISTS {publicationName};").ConfigureAwait(false);
				_ = await conn.ExecuteAsync($"DROP TABLE IF EXISTS {table};").ConfigureAwait(false);
				_ = await conn.ExecuteAsync($"DROP SCHEMA IF EXISTS {stateSchema} CASCADE;").ConfigureAwait(false);
			}
			catch (NpgsqlException)
			{
				// Best-effort cleanup; the container is torn down with the collection.
			}
			finally
			{
				_stateStore.Dispose();
			}
		}
	}
}
