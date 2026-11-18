// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using BenchmarkDotNet.Attributes;

using Microsoft.Data.SqlClient;

namespace Excalibur.Benchmarks.EventSourcing;

/// <summary>
/// Synthetic SQL comparison of staging before versus after global-position allocation.
/// </summary>
/// <remarks>
/// <para>
/// These synthetic SQL arms vary staging order within one transaction. Matched repetitions can
/// investigate effects associated with lock duration; environment, scheduling and database state
/// can also affect results. This does not isolate a universal cost of gapless ordering or establish
/// a production defect.
/// </para>
/// <para>
/// This fixture includes outbox statements; <c>AppendAllocationStrategyBenchmarks</c> does not.
/// Keep their workloads, configurations and results separate.
/// </para>
/// <para>
/// Requires <c>BENCHMARK_SQL_CONNECTIONSTRING</c>. Throws when absent rather than returning quietly: a
/// benchmark that skips reports a spectacular number for doing nothing, and these figures are meant to
/// be quoted.
/// </para>
/// </remarks>
[MemoryDiagnoser]
[InProcess]
[WarmupCount(3)]
[IterationCount(10)]
[InvocationCount(16)]
public class OutboxStagingWindowBenchmarks
{
	private static readonly string? ConnectionString =
		Environment.GetEnvironmentVariable("BENCHMARK_SQL_CONNECTIONSTRING");

	/// <summary>
	/// Gets or sets how many appends run concurrently in one iteration.
	/// </summary>
	/// <remarks>
	/// 1 is the control: with no contention the two arms issue identical statements in a different order
	/// without competing writers in this fixture. Single-writer differences also require investigation;
	/// do not automatically attribute them to noise or extrapolate them to contended workloads.
	/// </remarks>
	[Params(1, 8, 32)]
	public int WriterCount { get; set; }

	/// <summary>
	/// Gets or sets how many outbox rows each append stages, i.e. how many integration events the
	/// aggregate emitted.
	/// </summary>
	[Params(1, 3)]
	public int StagedMessageCount { get; set; }

	[GlobalSetup]
	public void GlobalSetup()
	{
		if (string.IsNullOrWhiteSpace(ConnectionString))
		{
			throw new InvalidOperationException(
				"OutboxStagingWindowBenchmarks needs a real SQL Server: set BENCHMARK_SQL_CONNECTIONSTRING.");
		}

		EnsureSchemaAsync().GetAwaiter().GetResult();
		ResetAsync().GetAwaiter().GetResult();
	}

	[Benchmark(Baseline = true, Description = "stage INSIDE the lock (old order)")]
	public Task StageInsideLock() => RunAsync(stageBeforeAllocation: false);

	[Benchmark(Description = "stage OUTSIDE the lock (new order)")]
	public Task StageOutsideLock() => RunAsync(stageBeforeAllocation: true);

	/// <summary>
	/// Empties every table and reseeds the counter, so each arm starts from the same state.
	/// </summary>
	/// <remarks>
	/// Without this the tables accumulate across arms, across parameter combinations and across runs, and
	/// a later arm is then measured against a larger, more fragmented table than an earlier one — so the
	/// benchmark reports table growth as if it were the effect under test.
	/// <see cref="GlobalSetup"/> runs once per arm, so truncating here gives every arm an empty table.
	/// </remarks>
	private static async Task ResetAsync()
	{
		await using var connection = new SqlConnection(ConnectionString);
		await connection.OpenAsync().ConfigureAwait(false);

		const string Sql = """
			TRUNCATE TABLE dbo.StageBenchEvents;
			TRUNCATE TABLE dbo.StageBenchOutbox;
			UPDATE dbo.StageBenchPosition SET Value = 0 WHERE Id = 1;
			""";

		await using var command = new SqlCommand(Sql, connection);
		_ = await command.ExecuteNonQueryAsync().ConfigureAwait(false);
	}

	private Task RunAsync(bool stageBeforeAllocation)
	{
		var work = new Task[WriterCount];
		for (var i = 0; i < WriterCount; i++)
		{
			work[i] = Task.Run(async () =>
			{
				await using var connection = new SqlConnection(ConnectionString);
				await connection.OpenAsync().ConfigureAwait(false);
				await AppendAsync(connection, stageBeforeAllocation).ConfigureAwait(false);
			});
		}

		return Task.WhenAll(work);
	}

	private async Task AppendAsync(SqlConnection connection, bool stageBeforeAllocation)
	{
		await using var tx = (SqlTransaction)await connection.BeginTransactionAsync().ConfigureAwait(false);

		var aggregateId = Guid.NewGuid().ToString("N");

		// The optimistic-concurrency pre-check, identical in both arms and outside the lock in both.
		await using (var check = new SqlCommand(
			"SELECT ISNULL(MAX(Version), -1) FROM dbo.StageBenchEvents WHERE AggregateId = @a;", connection, tx))
		{
			_ = check.Parameters.AddWithValue("@a", aggregateId);
			_ = await check.ExecuteScalarAsync().ConfigureAwait(false);
		}

		if (stageBeforeAllocation)
		{
			await StageAsync(connection, tx).ConfigureAwait(false);
		}

		// Allocate. From here to COMMIT every other appender is blocked on this row.
		long position;
		await using (var allocate = new SqlCommand(
			"UPDATE dbo.StageBenchPosition WITH (ROWLOCK) SET Value = Value + 1 OUTPUT INSERTED.Value WHERE Id = 1;",
			connection,
			tx))
		{
			position = (long)(await allocate.ExecuteScalarAsync().ConfigureAwait(false))!;
		}

		await using (var insert = new SqlCommand(
			"INSERT INTO dbo.StageBenchEvents (Position, EventId, AggregateId, Version) VALUES (@p, @e, @a, 0);",
			connection,
			tx))
		{
			_ = insert.Parameters.AddWithValue("@p", position);
			_ = insert.Parameters.AddWithValue("@e", Guid.NewGuid().ToString("N"));
			_ = insert.Parameters.AddWithValue("@a", aggregateId);
			_ = await insert.ExecuteNonQueryAsync().ConfigureAwait(false);
		}

		if (!stageBeforeAllocation)
		{
			await StageAsync(connection, tx).ConfigureAwait(false);
		}

		await tx.CommitAsync().ConfigureAwait(false);
	}

	/// <summary>
	/// Stages the outbox rows the way the store does: one round trip per integration event, on the
	/// append's own transaction. The per-row loop is the point — it is what makes the placement matter.
	/// </summary>
	private async Task StageAsync(SqlConnection connection, SqlTransaction tx)
	{
		for (var i = 0; i < StagedMessageCount; i++)
		{
			await using var stage = new SqlCommand(
				"INSERT INTO dbo.StageBenchOutbox (Id, Payload) VALUES (@id, @p);", connection, tx);
			_ = stage.Parameters.AddWithValue("@id", Guid.NewGuid().ToString("N"));
			_ = stage.Parameters.AddWithValue("@p", "payload");
			_ = await stage.ExecuteNonQueryAsync().ConfigureAwait(false);
		}
	}

	private static async Task EnsureSchemaAsync()
	{
		await using var connection = new SqlConnection(ConnectionString);
		await connection.OpenAsync().ConfigureAwait(false);

		const string Sql = """
			IF OBJECT_ID(N'dbo.StageBenchEvents', 'U') IS NULL
			CREATE TABLE dbo.StageBenchEvents (
				Position BIGINT NOT NULL PRIMARY KEY,
				EventId NVARCHAR(64) NOT NULL,
				AggregateId NVARCHAR(64) NOT NULL,
				Version BIGINT NOT NULL,
				INDEX IX_StageBenchEvents_Agg (AggregateId)
			);

			IF OBJECT_ID(N'dbo.StageBenchOutbox', 'U') IS NULL
			CREATE TABLE dbo.StageBenchOutbox (
				Id NVARCHAR(64) NOT NULL PRIMARY KEY,
				Payload NVARCHAR(128) NOT NULL
			);

			IF OBJECT_ID(N'dbo.StageBenchPosition', 'U') IS NULL
			CREATE TABLE dbo.StageBenchPosition (
				Id TINYINT NOT NULL PRIMARY KEY,
				Value BIGINT NOT NULL
			);

			IF NOT EXISTS (SELECT 1 FROM dbo.StageBenchPosition WHERE Id = 1)
			INSERT INTO dbo.StageBenchPosition (Id, Value)
			SELECT 1, ISNULL((SELECT MAX(Position) FROM dbo.StageBenchEvents), 0);
			""";

		await using var command = new SqlCommand(Sql, connection);
		_ = await command.ExecuteNonQueryAsync().ConfigureAwait(false);
	}
}
