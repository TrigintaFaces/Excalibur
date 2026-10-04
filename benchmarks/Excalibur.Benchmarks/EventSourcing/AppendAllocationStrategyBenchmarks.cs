// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Diagnostics;
using System.Globalization;

using BenchmarkDotNet.Attributes;
using Perfolizer.Mathematics.OutlierDetection;

using Microsoft.Data.SqlClient;

namespace Excalibur.Benchmarks.EventSourcing;

/// <summary>SQL Server diagnostics for synthetic identity and transactional-counter append shapes.</summary>
/// <remarks>
/// One benchmark operation is a wave of WriterCount independent appends, including scheduling,
/// connection acquisition and cleanup. It is not individual append latency or a complete framework
/// append/subscriber protocol. These SQL shapes omit production payloads, outbox and watermark costs;
/// their differences cannot isolate the price of ordering or predict another provider's performance.
/// Full JSON retains BenchmarkDotNet iteration metadata. The separate wave CSV includes warmup and
/// measurement invocations without stage classification; it cannot establish steady-state percentiles.
/// Requires a disposable SQL Server database via BENCHMARK_SQL_CONNECTIONSTRING. Setup resets its
/// benchmark tables. Never run against a database containing valuable benchmark-table data.
/// </remarks>
// Retain long observations; they can expose contention. Invocation/iteration counts describe this
// diagnostic configuration, not statistical independence or a universal precision guarantee.
[Outliers(OutlierMode.DontRemove)]
[MemoryDiagnoser]
[InProcess]
[JsonExporterAttribute.Full]
[WarmupCount(3)]
[IterationCount(20)]
[InvocationCount(24)]
public class AppendAllocationStrategyBenchmarks
{
	internal static readonly string? ConnectionString =
		Environment.GetEnvironmentVariable("BENCHMARK_SQL_CONNECTIONSTRING");

	/// <summary>Gets or sets the number of concurrent appends in one measured wave.</summary>
	[Params(1, 8, 16, 32)]
	public int WriterCount { get; set; }

	/// <summary>Gets or sets the events inserted by each append transaction.</summary>
	[Params(1, 5)]
	public int EventsPerAppend { get; set; }

	private readonly List<WaveSample> _samples = [];

	[GlobalSetup]
	public async Task GlobalSetup()
	{
		if (string.IsNullOrWhiteSpace(ConnectionString))
		{
			throw new InvalidOperationException(
				"AppendAllocationStrategyBenchmarks needs a real SQL Server: set "
				+ "BENCHMARK_SQL_CONNECTIONSTRING.");
		}

		await EnsureSchemaAsync().ConfigureAwait(false);
		await ResetAsync().ConfigureAwait(false);
		lock (_samples)
		{
			_samples.Clear();
		}
	}

	[Benchmark(Baseline = true, Description = "SQL diagnostic: IDENTITY")]
	public Task Identity() => RunAsync(nameof(Identity), c => AppendIdentityAsync(c, EventsPerAppend));

	[Benchmark(Description = "SQL diagnostic: separate counter allocation and insert")]
	public Task CounterRow() => RunAsync(nameof(CounterRow), c => AppendCounterAsync(c, EventsPerAppend, batched: false));

	[Benchmark(Description = "SQL diagnostic: combined counter allocation and insert")]
	public Task CounterRowBatched() => RunAsync(nameof(CounterRowBatched), c => AppendCounterAsync(c, EventsPerAppend, batched: true));

	/// <summary>Writes unclassified wave observations, preserving arm, parameters and failures.</summary>
	[GlobalCleanup]
	public void WriteSamples()
	{
		var csv = CreateSamplesCsv();
		if (csv is null)
		{
			return;
		}

		var directory = Path.Combine("BenchmarkDotNet.Artifacts", "samples");
		_ = Directory.CreateDirectory(directory);
		var path = Path.Combine(directory,
			$"append-allocation-w{WriterCount}-ev{EventsPerAppend}-{DateTime.UtcNow:yyyyMMdd-HHmmssfff}-{Guid.NewGuid():N}.csv");
		File.WriteAllText(path, csv);
	}

	internal string? CreateSamplesCsv()
	{
		WaveSample[] snapshot;
		lock (_samples)
		{
			snapshot = [.. _samples];
		}

		if (snapshot.Length == 0)
		{
			return null;
		}

		var lines = new List<string>(snapshot.Length + 1)
		{
			"arm,writers,events_per_append,invocation,stage,outcome,elapsed_ms,observed_failure_type",
		};
		for (var i = 0; i < snapshot.Length; i++)
		{
			var sample = snapshot[i];
			var milliseconds = sample.Elapsed * 1000.0 / Stopwatch.Frequency;
			var outcome = sample.Failure is null ? "completed" : "failed";
			lines.Add(string.Create(CultureInfo.InvariantCulture,
				$"{CsvText(sample.Arm)},{sample.Writers},{sample.Events},{i + 1},unclassified,{outcome},{milliseconds:F4},{CsvText(sample.Failure ?? string.Empty)}"));
		}

		return string.Join(Environment.NewLine, lines) + Environment.NewLine;
	}

	/// <summary>Resets starting table contents; subsequent growth within a case remains part of the workload.</summary>
	internal static async Task ResetAsync()
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

	// CA2100: the row list is composed from an int loop counter and a computed long. Every VALUE
	// in the statement is a bound parameter, so no string ever reaches the SQL text.
#pragma warning disable CA2100
	/// <summary>
	/// Appends using the identity strategy: the position comes from the table identity, so no writer
	/// waits on a shared allocation row.
	/// </summary>
	/// <param name="connection">An open connection.</param>
	/// <param name="eventCount">How many events this append writes in one transaction.</param>
	/// <param name="holdBeforeCommit">
	/// How long to hold the transaction open before committing. Record this setting with each run.
	/// </param>
	/// <remarks>
	/// Holding a counter transaction retains its shared allocation-row dependency. Identity avoids
	/// that specific dependency, but other locks, I/O and resource contention can still affect writers.
	/// </remarks>
	internal static async Task AppendIdentityAsync(
		SqlConnection connection,
		int eventCount,
		TimeSpan holdBeforeCommit = default)
	{
		await using var tx = (SqlTransaction)await connection.BeginTransactionAsync().ConfigureAwait(false);

		var aggregateId = Guid.NewGuid().ToString("N");
		await VersionCheckAsync(connection, tx, "BenchIdentity", aggregateId).ConfigureAwait(false);

		// One multi-row INSERT, matching the counter arms: the database assigns every identity value.
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

		if (holdBeforeCommit > TimeSpan.Zero)
		{
			await Task.Delay(holdBeforeCommit).ConfigureAwait(false);
		}

		await tx.CommitAsync().ConfigureAwait(false);
	}
#pragma warning restore CA2100

	// CA2100: the row list is composed from an int loop counter and a computed long. Every VALUE
	// in the statement is a bound parameter, so no string ever reaches the SQL text.
#pragma warning disable CA2100
	/// <summary>
	/// Appends using the counter-row strategy: the position comes from a singleton counter row taken
	/// inside the appending transaction, which is what serializes concurrent writers.
	/// </summary>
	/// <param name="connection">An open connection.</param>
	/// <param name="eventCount">How many events this append writes in one transaction.</param>
	/// <param name="batched">Whether the allocation and the first insert travel in one command.</param>
	/// <param name="holdBeforeCommit">
	/// How long to hold the transaction open before committing. The hold blocks other appenders requiring
	/// this counter row; identity writers do not require it.
	/// </param>
	internal static async Task AppendCounterAsync(
		SqlConnection connection,
		int eventCount,
		bool batched,
		TimeSpan holdBeforeCommit = default)
	{
		await using var tx = (SqlTransaction)await connection.BeginTransactionAsync().ConfigureAwait(false);

		var aggregateId = Guid.NewGuid().ToString("N");
		await VersionCheckAsync(connection, tx, "BenchCounter", aggregateId).ConfigureAwait(false);

		// Allocate one block for this append. More events change insert work and may change lock-hold
		// duration; any amortization must be measured rather than inferred from the allocation count.
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

		if (holdBeforeCommit > TimeSpan.Zero)
		{
			await Task.Delay(holdBeforeCommit).ConfigureAwait(false);
		}

		await tx.CommitAsync().ConfigureAwait(false);
	}
#pragma warning restore CA2100

	internal static async Task EnsureSchemaAsync()
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

	internal async Task MeasureWaveAsync(string arm, Func<Task> operation, Func<long>? timestamp = null)
	{
		timestamp ??= Stopwatch.GetTimestamp;
		var writers = WriterCount;
		var events = EventsPerAppend;
		var started = timestamp();
		string? failure = null;
		try
		{
			await operation().ConfigureAwait(false);
		}
		catch (Exception exception)
		{
			failure = exception.GetType().FullName;
			throw;
		}
		finally
		{
			var elapsed = timestamp() - started;
			lock (_samples)
			{
				_samples.Add(new WaveSample(arm, writers, events, elapsed, failure));
			}
		}
	}

	private static string CsvText(string value) => "\"" + value.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";

	private Task RunAsync(string arm, Func<SqlConnection, Task> append) => MeasureWaveAsync(arm, async () =>
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

		// Await the entire wave, including disposal and failure drain, before recording its outcome.
		await Task.WhenAll(work).ConfigureAwait(false);
	});

	private readonly record struct WaveSample(string Arm, int Writers, int Events, long Elapsed, string? Failure);

	/// <summary>The version query performed by each synthetic arm; its cost may interact with table growth.</summary>
	private static async Task VersionCheckAsync(SqlConnection connection, SqlTransaction tx, string table, string aggregateId)
	{
#pragma warning disable CA2100 // table is one of two compile-time constants supplied by this class
		await using var check = new SqlCommand(
			$"SELECT ISNULL(MAX(Version), -1) FROM dbo.{table} WHERE AggregateId = @a;", connection, tx);
#pragma warning restore CA2100
		_ = check.Parameters.AddWithValue("@a", aggregateId);
		_ = await check.ExecuteScalarAsync().ConfigureAwait(false);
	}
}
