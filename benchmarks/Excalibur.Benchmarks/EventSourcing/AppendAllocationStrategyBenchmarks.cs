// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using BenchmarkDotNet.Attributes;

using Microsoft.Data.SqlClient;

namespace Excalibur.Benchmarks.EventSourcing;

/// <summary>
/// A/B on how a global position is ALLOCATED, isolating that decision from everything else an append
/// does.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why this exists.</b> The event store changed from an IDENTITY column to a counter row updated
/// inside the appending transaction, which is what makes the committed stream gapless. That change costs
/// something, and a whole-store benchmark cannot say how much: it measures serialization, the version
/// pre-check, the insert, the transaction and the network together, so any delta is unattributable.
/// </para>
/// <para>
/// These arms issue the SAME statements against two tables that differ ONLY in how the position is
/// produced. The difference between them is the price of the guarantee, and nothing else.
/// </para>
/// <list type="bullet">
/// <item><b>Identity</b> — the old shape: BEGIN, version check, INSERT (database assigns), COMMIT.</item>
/// <item><b>CounterRow</b> — the current shape: BEGIN, version check, UPDATE counter, INSERT, COMMIT.</item>
/// <item><b>CounterRowBatched</b> — the candidate optimisation: identical to CounterRow, but the counter
/// UPDATE and the INSERT travel in ONE command rather than two round trips.</item>
/// </list>
/// <para>
/// The third arm is the one worth acting on. If most of the counter's cost is a round trip rather than
/// lock contention, batching recovers it without weakening the guarantee at all -- and it is AOT-safe,
/// being SQL text rather than reflection or dynamic dispatch.
/// </para>
/// <para>
/// Requires <c>BENCHMARK_SQL_CONNECTIONSTRING</c>. Throws when absent rather than returning quietly: a
/// benchmark that skips reports a spectacular number for doing nothing, and these figures are meant to
/// be quoted.
/// </para>
/// </remarks>
// BOTH counts are raised, and the split between them is forced rather than chosen. These arms measure
// SERIALIZED writers queueing on one row, whose per-append time is heavy-tailed: at 16 invocations and
// 10 iterations the StdDev ran to 53% of the mean -- larger than the effect being measured -- so
// consecutive runs of the same cell disagreed by 20-70% and no ratio was quotable.
//
// Invocations and iterations reduce the error differently. Each invocation averages WriterCount appends
// WITHIN one measurement, so raising it shrinks the spread itself; iterations resample that spread, so
// raising them shrinks the standard error of the reported mean as the square root of the count. Both
// were needed.
//
// InvocationCount is capped near 24 by the IN-PROCESS toolchain, which refuses an iteration that takes
// too long: at 64 the 32-writer cell reached ~25 s per iteration and the run ABORTED mid-matrix with no
// results at all. In-process is not optional here -- a git worktree under the repository root makes
// BenchmarkDotNet's project scan ambiguous and the out-of-process toolchains fail before starting.
[MemoryDiagnoser]
[InProcess]
[WarmupCount(3)]
[IterationCount(20)]
[InvocationCount(24)]
public class AppendAllocationStrategyBenchmarks
{
	private static readonly string? ConnectionString =
		Environment.GetEnvironmentVariable("BENCHMARK_SQL_CONNECTIONSTRING");

	/// <summary>
	/// Gets or sets how many appends run concurrently in one iteration.
	/// </summary>
	[Params(1, 8, 32)]
	public int WriterCount { get; set; }

	/// <summary>
	/// Gets or sets how many events each append writes in ONE transaction.
	/// </summary>
	/// <remarks>
	/// <para>
	/// <b>This is the dimension the published figures were missing.</b> Those numbers were taken at one
	/// event per append, which is the WORST case for a counter row: the serialized allocation is paid in
	/// full by a single event. An aggregate operation routinely raises several events and appends them
	/// together, and the counter allocates ONE block for the whole batch — so the serialization should
	/// amortize across the batch while the identity column, which never serialized, has nothing to
	/// amortize.
	/// </para>
	/// <para>
	/// <b>1 is a control, not a data point.</b> It reproduces the shape already published, so a run whose
	/// single-event ratios have moved is telling you the harness or the machine changed — and its
	/// multi-event ratios should not be quoted until that is explained.
	/// </para>
	/// <para>
	/// The claim under test is falsifiable: if the counter's cost relative to identity does NOT fall as
	/// this rises, the amortization asserted in the benchmark documentation is wrong and must be
	/// withdrawn rather than repeated.
	/// </para>
	/// </remarks>
	[Params(1, 5)]
	public int EventsPerAppend { get; set; }

	[GlobalSetup]
	public void GlobalSetup()
	{
		if (string.IsNullOrWhiteSpace(ConnectionString))
		{
			throw new InvalidOperationException(
				"AppendAllocationStrategyBenchmarks needs a real SQL Server: set "
				+ "BENCHMARK_SQL_CONNECTIONSTRING.");
		}

		EnsureSchemaAsync().GetAwaiter().GetResult();
		ResetAsync().GetAwaiter().GetResult();
	}

	[Benchmark(Baseline = true, Description = "IDENTITY (old: gaps possible)")]
	public Task Identity() => RunAsync(c => AppendIdentityAsync(c, EventsPerAppend));

	[Benchmark(Description = "counter row, 2 round trips (current)")]
	public Task CounterRow() => RunAsync(c => AppendCounterAsync(c, EventsPerAppend, batched: false));

	[Benchmark(Description = "counter row, 1 round trip (candidate)")]
	public Task CounterRowBatched() => RunAsync(c => AppendCounterAsync(c, EventsPerAppend, batched: true));

	/// <summary>
	/// Empties every table and reseeds the counter, so each arm starts from the same state.
	/// </summary>
	/// <remarks>
	/// <b>This is load-bearing, not hygiene.</b> Without it the tables accumulate across arms and across
	/// runs, and the accumulation is ASYMMETRIC: two of the three arms write to the counter table and only
	/// one writes to the identity table, so the counter's table grows at twice the rate. Both carry a
	/// nonclustered index on a random GUID, so the larger table pays more page splits and a deeper index —
	/// and the A/B then measures table size as much as it measures allocation strategy, biased against the
	/// arm under test. Measured before this was added: 55,350 rows against 27,675 for the baseline.
	/// <see cref="GlobalSetup"/> runs once per arm, so truncating here gives every arm an empty table.
	/// </remarks>
	private static async Task ResetAsync()
	{
		await using var connection = new SqlConnection(ConnectionString);
		await connection.OpenAsync().ConfigureAwait(false);

		const string Sql = """
			TRUNCATE TABLE dbo.BenchIdentity;
			TRUNCATE TABLE dbo.BenchCounter;
			UPDATE dbo.BenchCounterPosition SET Value = 0 WHERE Id = 1;
			""";

		await using var command = new SqlCommand(Sql, connection);
		_ = await command.ExecuteNonQueryAsync().ConfigureAwait(false);
	}

	private Task RunAsync(Func<SqlConnection, Task> append)
	{
		var work = new Task[WriterCount];
		for (var i = 0; i < WriterCount; i++)
		{
			work[i] = Task.Run(async () =>
			{
				await using var connection = new SqlConnection(ConnectionString);
				await connection.OpenAsync().ConfigureAwait(false);
				await append(connection).ConfigureAwait(false);
			});
		}

		return Task.WhenAll(work);
	}

	// CA2100: the row list is composed from an int loop counter and a computed long. Every VALUE
	// in the statement is a bound parameter, so no string ever reaches the SQL text.
#pragma warning disable CA2100
	private static async Task AppendIdentityAsync(SqlConnection connection, int eventCount)
	{
		await using var tx = (SqlTransaction)await connection.BeginTransactionAsync().ConfigureAwait(false);

		var aggregateId = Guid.NewGuid().ToString("N");
		await VersionCheckAsync(connection, tx, "BenchIdentity", aggregateId).ConfigureAwait(false);

		// One multi-row INSERT, matching the counter arms: the database assigns every identity value.
		// There is nothing here to amortize -- this arm never serialized -- which is exactly why it is
		// the baseline for the amortization question.
		var values = new string[eventCount];
		await using (var insert = new SqlCommand { Connection = connection, Transaction = tx })
		{
			for (var i = 0; i < eventCount; i++)
			{
				values[i] = $"(@e{i}, @a, {i})";
				_ = insert.Parameters.AddWithValue($"@e{i}", Guid.NewGuid().ToString("N"));
			}

			_ = insert.Parameters.AddWithValue("@a", aggregateId);
			insert.CommandText =
				"INSERT INTO dbo.BenchIdentity (EventId, AggregateId, Version) VALUES "
				+ string.Join(", ", values) + ";";
			_ = await insert.ExecuteNonQueryAsync().ConfigureAwait(false);
		}

		await tx.CommitAsync().ConfigureAwait(false);
	}
#pragma warning restore CA2100

	// CA2100: the row list is composed from an int loop counter and a computed long. Every VALUE
	// in the statement is a bound parameter, so no string ever reaches the SQL text.
#pragma warning disable CA2100
	private static async Task AppendCounterAsync(SqlConnection connection, int eventCount, bool batched)
	{
		await using var tx = (SqlTransaction)await connection.BeginTransactionAsync().ConfigureAwait(false);

		var aggregateId = Guid.NewGuid().ToString("N");
		await VersionCheckAsync(connection, tx, "BenchCounter", aggregateId).ConfigureAwait(false);

		// ONE allocation for the whole batch, which is what the shipped store does: it advances the
		// counter by the event count and stamps positions from the block. The serialized section is
		// therefore the same width whether the append carries one event or many -- that is the
		// amortization this arm exists to measure rather than assert.
		if (batched)
		{
			await using var combined = new SqlCommand { Connection = connection, Transaction = tx };
			var rows = new string[eventCount];
			for (var i = 0; i < eventCount; i++)
			{
				rows[i] = $"(@pos + {i}, @e{i}, @a, {i})";
				_ = combined.Parameters.AddWithValue($"@e{i}", Guid.NewGuid().ToString("N"));
			}

			_ = combined.Parameters.AddWithValue("@a", aggregateId);
			_ = combined.Parameters.AddWithValue("@n", eventCount);
			combined.CommandText =
				"""
				-- SET XACT_ABORT ON mirrors the SHIPPED statement. Without it this arm measures a
				-- batch the store does not issue: a run-time error here would abort only the failing
				-- statement and leave the counter advanced, which is the defect the shipped form
				-- exists to prevent. A benchmark that drifts from the code it prices is not evidence
				-- about that code.
				SET XACT_ABORT ON;

				DECLARE @pos BIGINT;
				UPDATE dbo.BenchCounterPosition WITH (ROWLOCK)
				SET @pos = Value + 1, Value = Value + @n
				WHERE Id = 1;
				INSERT INTO dbo.BenchCounter (Position, EventId, AggregateId, Version)
				VALUES
				"""
				+ string.Join(", ", rows) + ";";
			_ = await combined.ExecuteNonQueryAsync().ConfigureAwait(false);
		}
		else
		{
			long blockEnd;
			await using (var allocate = new SqlCommand(
				"UPDATE dbo.BenchCounterPosition WITH (ROWLOCK) SET Value = Value + @n "
				+ "OUTPUT INSERTED.Value WHERE Id = 1;",
				connection,
				tx))
			{
				_ = allocate.Parameters.AddWithValue("@n", eventCount);
				blockEnd = (long)(await allocate.ExecuteScalarAsync().ConfigureAwait(false))!;
			}

			// OUTPUT INSERTED.Value is the counter AFTER the increment, so the block is
			// [blockEnd - eventCount + 1, blockEnd].
			var firstPosition = blockEnd - eventCount + 1;

			await using var insert = new SqlCommand { Connection = connection, Transaction = tx };
			var rows = new string[eventCount];
			for (var i = 0; i < eventCount; i++)
			{
				// The position is BOUND, never interpolated. An inline literal makes every INSERT a
				// textually distinct statement, so the server compiles a fresh plan per execution --
				// a cost only the counter arms would pay, because the identity arm has no position to
				// embed. That asymmetry would be charged to the arm under test.
				rows[i] = $"(@p{i}, @e{i}, @a, {i})";
				_ = insert.Parameters.AddWithValue($"@p{i}", firstPosition + i);
				_ = insert.Parameters.AddWithValue($"@e{i}", Guid.NewGuid().ToString("N"));
			}

			_ = insert.Parameters.AddWithValue("@a", aggregateId);
			insert.CommandText =
				"INSERT INTO dbo.BenchCounter (Position, EventId, AggregateId, Version) VALUES "
				+ string.Join(", ", rows) + ";";
			_ = await insert.ExecuteNonQueryAsync().ConfigureAwait(false);
		}

		await tx.CommitAsync().ConfigureAwait(false);
	}
#pragma warning restore CA2100

	/// <summary>The optimistic-concurrency pre-check both shapes perform, so it cancels out.</summary>
	private static async Task VersionCheckAsync(SqlConnection connection, SqlTransaction tx, string table, string aggregateId)
	{
#pragma warning disable CA2100 // table is one of two compile-time constants supplied by this class
		await using var check = new SqlCommand(
			$"SELECT ISNULL(MAX(Version), -1) FROM dbo.{table} WHERE AggregateId = @a;", connection, tx);
#pragma warning restore CA2100
		_ = check.Parameters.AddWithValue("@a", aggregateId);
		_ = await check.ExecuteScalarAsync().ConfigureAwait(false);
	}

	private static async Task EnsureSchemaAsync()
	{
		await using var connection = new SqlConnection(ConnectionString);
		await connection.OpenAsync().ConfigureAwait(false);

		const string Sql = """
			IF OBJECT_ID(N'dbo.BenchIdentity', 'U') IS NULL
			CREATE TABLE dbo.BenchIdentity (
				Position BIGINT IDENTITY(1,1) PRIMARY KEY,
				EventId NVARCHAR(64) NOT NULL,
				AggregateId NVARCHAR(64) NOT NULL,
				Version BIGINT NOT NULL,
				INDEX IX_BenchIdentity_Agg (AggregateId)
			);

			IF OBJECT_ID(N'dbo.BenchCounter', 'U') IS NULL
			CREATE TABLE dbo.BenchCounter (
				Position BIGINT NOT NULL PRIMARY KEY,
				EventId NVARCHAR(64) NOT NULL,
				AggregateId NVARCHAR(64) NOT NULL,
				Version BIGINT NOT NULL,
				INDEX IX_BenchCounter_Agg (AggregateId)
			);

			IF OBJECT_ID(N'dbo.BenchCounterPosition', 'U') IS NULL
			CREATE TABLE dbo.BenchCounterPosition (
				Id TINYINT NOT NULL PRIMARY KEY,
				Value BIGINT NOT NULL
			);

			IF NOT EXISTS (SELECT 1 FROM dbo.BenchCounterPosition WHERE Id = 1)
			INSERT INTO dbo.BenchCounterPosition (Id, Value)
			SELECT 1, ISNULL((SELECT MAX(Position) FROM dbo.BenchCounter), 0);
			""";

		await using var command = new SqlCommand(Sql, connection);
		_ = await command.ExecuteNonQueryAsync().ConfigureAwait(false);
	}
}
