// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using BenchmarkDotNet.Attributes;

using Excalibur.Dispatch;
using Excalibur.EventSourcing;
using Excalibur.EventSourcing.SqlServer;

using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Benchmarks.EventSourcing;

/// <summary>
/// Measures sustained append throughput under CONCURRENCY against a real SQL Server.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why this exists, and why the existing append benchmark cannot replace it.</b> Global positions are
/// allocated from a single counter row inside the appending transaction, whose exclusive lock is released
/// only at COMMIT. That is what makes the committed stream gapless, and it means concurrent appends
/// SERIALIZE on that row. A single-threaded append benchmark sees only one extra round trip and reports
/// essentially no change -- it cannot observe the ceiling, so measuring only that would produce a
/// reassuring number about the wrong thing.
/// </para>
/// <para>
/// Writers append to DISTINCT aggregates, so nothing here contends on the stream uniqueness key or on
/// optimistic concurrency. The only thing being measured is the counter row. Sweeping the writer count
/// shows the shape: throughput should rise with concurrency until the counter saturates and then flatten,
/// and the level at which it flattens IS the store's sustained append ceiling.
/// </para>
/// <para>
/// <b>Interpreting the result.</b> Report time is per ITERATION, and one iteration performs
/// <see cref="WriterCount"/> appends. Appends/sec is therefore
/// <c>WriterCount / (mean seconds per iteration)</c>. That flattening level is the number the event
/// sourcing guarantee contract should quote -- it currently carries a reasoned estimate, not a
/// measurement.
/// </para>
/// <para>
/// Requires a SQL Server instance: set <c>BENCHMARK_SQL_CONNECTIONSTRING</c>. When it is absent this
/// class THROWS rather than returning quietly. A benchmark that returns immediately reports a
/// spectacular number for having done nothing, and a throughput figure is exactly the kind of result
/// someone later quotes without re-checking how it was produced.
/// </para>
/// </remarks>
[MemoryDiagnoser]
// Iteration counts are pinned rather than left to the default: six parameter values against a real
// database otherwise runs for hours, and the figure being sought is a throughput CEILING, which is
// stable well before BenchmarkDotNet's default statistical rigour is reached.
// InProcess rather than the default CsProj toolchain. BenchmarkDotNet locates the benchmark project
// by scanning the repo, and a git worktree under the repo root makes that scan ambiguous
// ("found more than one matching project file"), which fails the run before it starts. In-process
// is also the honest choice here: every operation is a database round trip, so process isolation
// buys nothing that JIT noise could distort.
[InProcess]
[WarmupCount(3)]
[IterationCount(10)]
[InvocationCount(16)]
public class SqlServerConcurrentAppendBenchmarks
{
	private const string Schema = "dispatch";
	private const string Table = "ConcurrentAppendEvents";

	private static readonly string? ConnectionString =
		Environment.GetEnvironmentVariable("BENCHMARK_SQL_CONNECTIONSTRING");

	private SqlServerEventStore _eventStore = null!;

	/// <summary>
	/// Gets or sets how many appends run concurrently in one iteration.
	/// </summary>
	/// <remarks>
	/// 1 is the control: it measures the counter row's cost with no contention at all, so the difference
	/// between 1 and the rest separates "the extra statement" from "the serialization".
	/// </remarks>
	[Params(1, 2, 4, 8, 16, 32)]
	public int WriterCount { get; set; }

	[GlobalSetup]
	public void GlobalSetup()
	{
		if (string.IsNullOrWhiteSpace(ConnectionString))
		{
			throw new InvalidOperationException(
				"SqlServerConcurrentAppendBenchmarks needs a real SQL Server: set "
				+ "BENCHMARK_SQL_CONNECTIONSTRING. This throws rather than skipping because the number it "
				+ "produces is an append-throughput figure, and a skipped benchmark would report one "
				+ "indistinguishable from an extremely fast one.");
		}

		EnsureSchemaAsync().GetAwaiter().GetResult();

		// The connection-factory overload is the one that takes schema/table; the string overload is
		// hardcoded to dbo.EventStoreEvents and would silently measure a different table.
		_eventStore = new SqlServerEventStore(
			() => new SqlConnection(ConnectionString),
			NullLogger<SqlServerEventStore>.Instance,
			tenantContext: BenchmarkTenantContext.SingleTenant,
			schema: Schema,
			table: Table);
	}

	/// <summary>
	/// Appends one event to each of <see cref="WriterCount"/> DISTINCT aggregates, concurrently.
	/// </summary>
	[Benchmark]
	public async Task ConcurrentAppendToDistinctAggregates()
	{
		var appends = new Task[WriterCount];
		for (var i = 0; i < WriterCount; i++)
		{
			var aggregateId = Guid.NewGuid().ToString("N");
			appends[i] = Task.Run(async () =>
				await _eventStore.AppendAsync(
						aggregateId,
						"BenchAggregate",
						new IDomainEvent[]
						{
							new TestDomainEvent
							{
								EventId = Guid.NewGuid().ToString("N"),
								AggregateId = aggregateId,
								Version = 0,
								OccurredAt = DateTimeOffset.UtcNow,
								EventType = nameof(TestDomainEvent),
							},
						},
						-1,
						CancellationToken.None)
					.ConfigureAwait(false));
		}

		await Task.WhenAll(appends).ConfigureAwait(false);
	}

	private static async Task EnsureSchemaAsync()
	{
		await using var connection = new SqlConnection(ConnectionString);
		await connection.OpenAsync().ConfigureAwait(false);

		// Mirrors the shipped schema in the shape that matters here: Position is NOT an identity column,
		// and the counter row it is allocated from exists and is seeded.
		const string Sql = $"""
			IF SCHEMA_ID(N'{Schema}') IS NULL EXEC(N'CREATE SCHEMA [{Schema}]');

			IF OBJECT_ID(N'[{Schema}].[{Table}]', 'U') IS NULL
			CREATE TABLE [{Schema}].[{Table}] (
				[Position]      BIGINT NOT NULL CONSTRAINT [PK_{Table}] PRIMARY KEY CLUSTERED,
				[EventId]       NVARCHAR(255) NOT NULL,
				[AggregateId]   NVARCHAR(255) NOT NULL,
				[AggregateType] NVARCHAR(255) NOT NULL,
				[EventType]     NVARCHAR(255) NOT NULL,
				[EventData]     VARBINARY(MAX) NULL,
				[Metadata]      VARBINARY(MAX) NULL,
				[Version]       BIGINT NOT NULL,
				[Timestamp]     DATETIMEOFFSET NOT NULL,
				[ArchivedAt]    DATETIMEOFFSET NULL,
				[TenantId]      NVARCHAR(64) COLLATE Latin1_General_BIN2 NOT NULL
					CONSTRAINT [DF_{Table}_TenantId] DEFAULT '__untenanted__',
				CONSTRAINT [UQ_{Table}_Stream] UNIQUE ([AggregateId], [AggregateType], [Version], [TenantId])
			);

			IF OBJECT_ID(N'[{Schema}].[{Table}Position]', 'U') IS NULL
			CREATE TABLE [{Schema}].[{Table}Position] (
				[Id]    TINYINT NOT NULL CONSTRAINT [PK_{Table}Position] PRIMARY KEY,
				[Value] BIGINT  NOT NULL,
				CONSTRAINT [CK_{Table}Position_Singleton] CHECK ([Id] = 1)
			);

			IF NOT EXISTS (SELECT 1 FROM [{Schema}].[{Table}Position] WHERE [Id] = 1)
			INSERT INTO [{Schema}].[{Table}Position] ([Id], [Value])
			SELECT 1, ISNULL((SELECT MAX([Position]) FROM [{Schema}].[{Table}]), 0);
			""";

#pragma warning disable CA2100 // Schema and table are compile-time constants in this benchmark
		await using var command = new SqlCommand(Sql, connection);
#pragma warning restore CA2100
		_ = await command.ExecuteNonQueryAsync().ConfigureAwait(false);
	}
}
