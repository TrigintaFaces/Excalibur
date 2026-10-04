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
/// Measures the elapsed time of a concurrent append wave against a real SQL Server.
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
/// Writers append to distinct, new aggregates. The measurement includes scheduling, serialization,
/// connection pooling, network and transaction work as well as counter contention. It does not isolate
/// counter cost or establish production sustained capacity.
/// </para>
/// <para>
/// <b>Interpreting the result.</b> Report time is per invocation, and one invocation performs
/// <see cref="WriterCount"/> appends. Appends/sec is therefore
/// <c>WriterCount / (mean seconds per invocation)</c> for this closed-loop wave workload. Each append
/// writes one version-zero <see cref="TestDomainEvent"/> with fresh stream/event identities and a null
/// application payload. Persistence checks run outside timing, but affect cache state and spacing
/// between waves. This one-invocation configuration requires a fresh baseline; results from the former
/// sixteen-invocation configuration are not directly comparable. Retain the candidate revision and
/// environment with the raw BenchmarkDotNet report before publishing comparisons.
/// </para>
/// <para>
/// Requires a SQL Server instance: set <c>BENCHMARK_SQL_CONNECTIONSTRING</c>. When it is absent this
/// class THROWS rather than returning quietly. A benchmark that returns immediately reports a
/// spectacular number for having done nothing, and a throughput figure is exactly the kind of result
/// someone later quotes without re-checking how it was produced.
/// </para>
/// </remarks>
[MemoryDiagnoser]
// This is a database macrobenchmark. One wave per iteration permits persistence verification outside
// the timed interval. IterationCleanup failures propagate before BDN publishes measurements, whereas
// GlobalCleanup failures can be swallowed by the in-process toolchain (BDN 0.15.8).
// InProcess rather than the default CsProj toolchain. BenchmarkDotNet locates the benchmark project
// by scanning the repo, and a git worktree under the repo root makes that scan ambiguous
// ("found more than one matching project file"), which fails the run before it starts. In-process
// is also the honest choice here: every operation is a database round trip, so process isolation
// buys nothing that JIT noise could distort.
[InProcess]
[WarmupCount(3)]
[IterationCount(10)]
[InvocationCount(1, unrollFactor: 1)]
public class SqlServerConcurrentAppendBenchmarks
{
	private const string Schema = "dispatch";
	private const string Table = "ConcurrentAppendEvents";

	private readonly string? _connectionString;
	private readonly Func<string, Task<(long Events, long Streams, long Identities)>> _readCounts;
	private IEventStore _eventStore = null!;
	private string _aggregateType = string.Empty;
	private long _committedAppends;
	private bool _waveFailed;

	public SqlServerConcurrentAppendBenchmarks()
		: this(Environment.GetEnvironmentVariable("BENCHMARK_SQL_CONNECTIONSTRING"))
	{
	}

	internal SqlServerConcurrentAppendBenchmarks(string? connectionString)
	{
		_connectionString = connectionString;
		_readCounts = ReadCountsAsync;
	}

	internal SqlServerConcurrentAppendBenchmarks(
		IEventStore eventStore,
		Func<string, Task<(long Events, long Streams, long Identities)>> readCounts)
	{
		_eventStore = eventStore;
		_readCounts = readCounts;
	}

	/// <summary>
	/// Gets or sets how many appends run concurrently in one invocation.
	/// </summary>
	/// <remarks>
	/// One writer is the serial control for the complete append path, including the database round trip.
	/// </remarks>
	[Params(1, 2, 4, 8, 16, 32)]
	public int WriterCount { get; set; }

	[GlobalSetup]
	public async Task GlobalSetup()
	{
		if (string.IsNullOrWhiteSpace(_connectionString))
		{
			throw new InvalidOperationException(
				"SqlServerConcurrentAppendBenchmarks needs a real SQL Server: set "
				+ "BENCHMARK_SQL_CONNECTIONSTRING. This throws rather than skipping because the number it "
				+ "produces is an append-throughput figure, and a skipped benchmark would report one "
				+ "indistinguishable from an extremely fast one.");
		}

		await EnsureSchemaAsync().ConfigureAwait(false);

		// The connection-factory overload is the one that takes schema/table; the string overload is
		// hardcoded to dbo.EventStoreEvents and would silently measure a different table.
		_eventStore = new SqlServerEventStore(
			() => new SqlConnection(_connectionString),
			NullLogger<SqlServerEventStore>.Instance,
			tenantContext: BenchmarkTenantContext.SingleTenant,
			schema: Schema,
			table: Table);
	}

	[IterationSetup]
	public void IterationSetup()
	{
		ArgumentOutOfRangeException.ThrowIfNegativeOrZero(WriterCount);
		_aggregateType = $"BenchAggregate-{Guid.NewGuid():N}";
		_committedAppends = 0;
		_waveFailed = false;
	}

	[IterationCleanup]
	public async Task IterationCleanup()
	{
		if (_waveFailed || _committedAppends != WriterCount)
		{
			throw new InvalidOperationException("Invalid append measurement: a wave failed or was skipped.");
		}

		var counts = await _readCounts(_aggregateType).ConfigureAwait(false);
		if (counts.Events != _committedAppends || counts.Streams != _committedAppends || counts.Identities != _committedAppends)
		{
			throw new InvalidOperationException(
				$"Invalid append measurement: expected {_committedAppends} new events/streams/identities; found {counts}.");
		}

		Console.WriteLine($"Validated append wave: {_committedAppends} appends/events/streams; run={_aggregateType}.");
	}

	/// <summary>
	/// Appends one event to each of <see cref="WriterCount"/> DISTINCT aggregates, concurrently.
	/// </summary>
	[Benchmark]
	public async Task ConcurrentAppendToDistinctAggregates()
	{
		var appends = new Task<AppendResult>[WriterCount];
		for (var i = 0; i < WriterCount; i++)
		{
			var aggregateId = Guid.NewGuid().ToString("N");
			appends[i] = Task.Run(async () =>
				await _eventStore.AppendAsync(
						aggregateId,
						_aggregateType,
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

		try
		{
			var results = await Task.WhenAll(appends).ConfigureAwait(false);
			foreach (var result in results)
			{
				// AlreadyCommitted is a valid retry outcome, but this workload promises NEW writes.
				if (result.Outcome != AppendOutcome.Committed)
				{
					throw new InvalidOperationException($"Invalid append measurement: expected Committed, received {result.Outcome}.");
				}
			}

			_committedAppends = checked(_committedAppends + results.Length);
		}
		catch
		{
			_waveFailed = true;
			throw;
		}
	}

	private async Task<(long Events, long Streams, long Identities)> ReadCountsAsync(string aggregateType)
	{
		await using var connection = new SqlConnection(_connectionString);
		await connection.OpenAsync().ConfigureAwait(false);
		const string Sql = $"""
			SELECT COUNT_BIG(*), COUNT_BIG(DISTINCT [AggregateId]),
			       COUNT_BIG(DISTINCT CASE WHEN [Version] = 0 THEN [EventId] END)
			FROM [{Schema}].[{Table}]
			WHERE [AggregateType] = @AggregateType AND [TenantId] = @TenantId;
			""";
		await using var command = new SqlCommand(Sql, connection);
		_ = command.Parameters.AddWithValue("@AggregateType", aggregateType);
		_ = command.Parameters.AddWithValue("@TenantId", TenantDefaults.DefaultTenantId);
		await using var reader = await command.ExecuteReaderAsync().ConfigureAwait(false);
		_ = await reader.ReadAsync().ConfigureAwait(false);
		return (reader.GetInt64(0), reader.GetInt64(1), reader.GetInt64(2));
	}

	private async Task EnsureSchemaAsync()
	{
		await using var connection = new SqlConnection(_connectionString);
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
