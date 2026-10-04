// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project

using System.Text;
using System.Text.Json;

using BenchmarkDotNet.Attributes;

using Excalibur.Dispatch;
using Excalibur.Domain.Model;
using Excalibur.EventSourcing;
using Excalibur.EventSourcing.SqlServer;

using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Benchmarks.EventSourcing;

/// <summary>Measures real SQL event-store operations with verified inputs and results.</summary>
/// <remarks>
/// Requires BENCHMARK_SQL_CONNECTIONSTRING and permission to create an isolated schema.
/// Uses the shipped event/snapshot DDL, with only the schema name changed. One invocation runs per
/// iteration; persistence verification happens outside timing but affects cache state and spacing.
/// Retain a fresh baseline, candidate revision and environment when publishing comparisons.
/// </remarks>
[MemoryDiagnoser]
[InProcess]
[InvocationCount(1, unrollFactor: 1)]
public class SqlServerEventStoreBenchmarks
{
    private const string AggregateType = "BenchmarkAggregate";
    private readonly string? _connectionString = Environment.GetEnvironmentVariable("BENCHMARK_SQL_CONNECTIONSTRING");
    private readonly Dictionary<string, TestDomainEvent[]> _seeds = new(StringComparer.Ordinal);
    private SqlServerEventStore _eventStore = null!;
    private SqlServerSnapshotStore _snapshotStore = null!;
    private string _aggregateWith5Events = null!;
    private string _aggregateWith50Events = null!;
    private string _aggregateWith500Events = null!;
    private string _iterationAggregateId = null!;
    private BenchmarkSnapshot _seedSnapshot = null!;
    private bool _ownsSchema;
    private string? _operation;
    private TestDomainEvent[]? _expectedEvents;
    private AppendResult? _appendResult;
    private IReadOnlyList<StoredEvent>? _loadedEvents;
    private ISnapshot? _loadedSnapshot;
    private BenchmarkSnapshot? _savedSnapshot;

    internal string SchemaName { get; } = $"Benchmark_{Guid.NewGuid():N}";

    [GlobalSetup]
    public async Task GlobalSetup()
    {
        if (string.IsNullOrWhiteSpace(_connectionString))
        {
            throw new InvalidOperationException("SQL event-store benchmarks require BENCHMARK_SQL_CONNECTIONSTRING; no measurement can run without SQL Server.");
        }

        _eventStore = new SqlServerEventStore(() => new SqlConnection(_connectionString),
            NullLogger<SqlServerEventStore>.Instance, tenantContext: BenchmarkTenantContext.SingleTenant, schema: SchemaName);
        _snapshotStore = new SqlServerSnapshotStore(() => new SqlConnection(_connectionString),
            NullLogger<SqlServerSnapshotStore>.Instance, BenchmarkTenantContext.SingleTenant, schema: SchemaName);
        await EnsureSchemaAsync().ConfigureAwait(false);
        try
        {
            _aggregateWith5Events = await CreateAggregateWithEventsAsync(5).ConfigureAwait(false);
            _aggregateWith50Events = await CreateAggregateWithEventsAsync(50).ConfigureAwait(false);
            _aggregateWith500Events = await CreateAggregateWithEventsAsync(500).ConfigureAwait(false);
            _seedSnapshot = CreateSnapshot(_aggregateWith5Events, 4);
            await _snapshotStore.SaveSnapshotAsync(_seedSnapshot, CancellationToken.None).ConfigureAwait(false);
            ValidateSnapshot(await _snapshotStore.GetLatestSnapshotAsync(_aggregateWith5Events, AggregateType, CancellationToken.None)
                .ConfigureAwait(false), _seedSnapshot);
        }
        catch (Exception setupFailure)
        {
            // BDN may never construct its engine after a failed GlobalSetup, so it cannot own cleanup.
            try
            {
                await GlobalCleanup().ConfigureAwait(false);
            }
            catch (Exception cleanupFailure)
            {
                setupFailure.Data["BenchmarkCleanupFailure"] = cleanupFailure;
            }

            throw;
        }
    }

    [IterationSetup]
    public void IterationSetup()
    {
        _iterationAggregateId = Guid.NewGuid().ToString("N");
        _operation = null;
        _expectedEvents = null;
        _appendResult = null;
        _loadedEvents = null;
        _loadedSnapshot = null;
        _savedSnapshot = null;
    }

    [Benchmark(Baseline = true)]
    public Task<AppendResult> AppendSingleEvent() => AppendAsync(1);

    [Benchmark]
    public Task<AppendResult> AppendBatchEvents() => AppendAsync(10);

    [Benchmark]
    public Task<IReadOnlyList<StoredEvent>> LoadSmallAggregate() => LoadAsync(_aggregateWith5Events);

    [Benchmark]
    public Task<IReadOnlyList<StoredEvent>> LoadMediumAggregate() => LoadAsync(_aggregateWith50Events);

    [Benchmark]
    public Task<IReadOnlyList<StoredEvent>> LoadLargeAggregate() => LoadAsync(_aggregateWith500Events);

    [Benchmark]
    public async Task SaveSnapshot()
    {
        BeginOperation(nameof(SaveSnapshot));
        _savedSnapshot = CreateSnapshot(_iterationAggregateId, 0);
        await _snapshotStore.SaveSnapshotAsync(_savedSnapshot, CancellationToken.None).ConfigureAwait(false);
    }

    [Benchmark]
    public async Task<ISnapshot?> LoadSnapshot()
    {
        BeginOperation(nameof(LoadSnapshot));
        _loadedSnapshot = await _snapshotStore.GetLatestSnapshotAsync(_aggregateWith5Events, AggregateType, CancellationToken.None)
            .ConfigureAwait(false);
        return _loadedSnapshot;
    }

    [IterationCleanup]
    public async Task IterationCleanup()
    {
        switch (_operation)
        {
            case "append":
                RequireCommitted(_appendResult);
                ValidateEvents(await _eventStore.LoadAsync(_iterationAggregateId, AggregateType, CancellationToken.None)
                    .ConfigureAwait(false), _expectedEvents!);
                break;
            case "load":
                ValidateEvents(_loadedEvents, _expectedEvents!);
                break;
            case nameof(SaveSnapshot):
                ValidateSnapshot(await _snapshotStore.GetLatestSnapshotAsync(_iterationAggregateId, AggregateType, CancellationToken.None)
                    .ConfigureAwait(false), _savedSnapshot!);
                break;
            case nameof(LoadSnapshot):
                ValidateSnapshot(_loadedSnapshot, _seedSnapshot);
                break;
            default:
                throw new InvalidOperationException("Invalid SQL measurement: no operation completed.");
        }
    }

    [GlobalCleanup]
    public async Task GlobalCleanup()
    {
        if (!_ownsSchema)
        {
            return;
        }

        // Only this instance's freshly created GUID schema is owned; never drop caller tables.
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync().ConfigureAwait(false);
#pragma warning disable CA2100 // SchemaName is generated here from a GUID, never external input.
        await using var command = new SqlCommand($"""
            DROP TABLE IF EXISTS [{SchemaName}].[EventStoreSnapshots];
            DROP TABLE IF EXISTS [{SchemaName}].[EventStoreEvents];
            DROP TABLE IF EXISTS [{SchemaName}].[EventStoreEventsPosition];
            IF SCHEMA_ID(N'{SchemaName}') IS NOT NULL EXEC(N'DROP SCHEMA [{SchemaName}]');
            """, connection);
#pragma warning restore CA2100
        _ = await command.ExecuteNonQueryAsync().ConfigureAwait(false);
        _ownsSchema = false;
    }

    private void BeginOperation(string operation)
    {
        if (_operation is not null)
        {
            throw new InvalidOperationException("Expected one benchmark invocation per iteration.");
        }

        _operation = operation;
    }

    private async Task<AppendResult> AppendAsync(int count)
    {
        BeginOperation("append");
        _expectedEvents = CreateEvents(_iterationAggregateId, count);
        _appendResult = await _eventStore.AppendAsync(_iterationAggregateId, AggregateType, _expectedEvents, -1, CancellationToken.None)
            .ConfigureAwait(false);
        return _appendResult;
    }

    private async Task<IReadOnlyList<StoredEvent>> LoadAsync(string aggregateId)
    {
        BeginOperation("load");
        _expectedEvents = _seeds[aggregateId];
        _loadedEvents = await _eventStore.LoadAsync(aggregateId, AggregateType, CancellationToken.None).ConfigureAwait(false);
        return _loadedEvents;
    }

    private static TestDomainEvent[] CreateEvents(string aggregateId, int count) => Enumerable.Range(0, count)
        .Select(version => new TestDomainEvent
        {
            EventId = Guid.NewGuid().ToString("N"), AggregateId = aggregateId, Version = version,
            OccurredAt = DateTimeOffset.UtcNow, EventType = nameof(TestDomainEvent),
            Metadata = new Dictionary<string, object> { ["UserId"] = "benchmark-user" },
            Data = $"Benchmark event data for version {version}",
        }).ToArray();

    private static BenchmarkSnapshot CreateSnapshot(string aggregateId, long version) => new()
    {
        SnapshotId = Guid.NewGuid().ToString("N"), AggregateId = aggregateId, AggregateType = AggregateType,
        TenantId = TenantDefaults.DefaultTenantId, Version = version, CreatedAt = DateTimeOffset.UtcNow,
        Data = Enumerable.Range(0, 512).Select(index => (byte)((index % 251) + 1)).ToArray(),
    };

    private static void RequireCommitted(AppendResult? result)
    {
        if (result?.Outcome != AppendOutcome.Committed)
        {
            throw new InvalidOperationException($"Invalid SQL measurement: expected a new Committed append, received {result?.Outcome}.");
        }
    }

    private static void ValidateEvents(IReadOnlyList<StoredEvent>? actual, TestDomainEvent[] expected)
    {
        if (actual is null || actual.Count != expected.Length)
        {
            throw new InvalidOperationException("Invalid SQL measurement: incorrect event count.");
        }

        for (var index = 0; index < expected.Length; index++)
        {
            var stored = actual[index];
            var submitted = expected[index];
            var payload = stored.EventData is null ? null : JsonSerializer.Deserialize<TestDomainEvent>(stored.EventData, JsonSerializerOptions.Web);
            if (stored.EventId != submitted.EventId || stored.AggregateId != submitted.AggregateId ||
                stored.AggregateType != AggregateType || stored.Version != index || stored.GlobalPosition <= 0 ||
                payload?.Data != submitted.Data || payload.EventId != submitted.EventId ||
                payload.AggregateId != submitted.AggregateId || payload.Version != index)
            {
                throw new InvalidOperationException("Invalid SQL measurement: event identity, version or payload differs from submitted data.");
            }
        }
    }

    private static void ValidateSnapshot(ISnapshot? actual, BenchmarkSnapshot expected)
    {
        if (actual is null || actual.SnapshotId != expected.SnapshotId || actual.AggregateId != expected.AggregateId ||
            actual.AggregateType != expected.AggregateType || actual.TenantId != expected.TenantId ||
            actual.Version != expected.Version || !actual.Data.Span.SequenceEqual(expected.Data.Span))
        {
            throw new InvalidOperationException("Invalid SQL measurement: missing or incorrect snapshot.");
        }
    }

    private async Task<string> CreateAggregateWithEventsAsync(int count)
    {
        var aggregateId = Guid.NewGuid().ToString("N");
        var events = CreateEvents(aggregateId, count);
        RequireCommitted(await _eventStore.AppendAsync(aggregateId, AggregateType, events, -1, CancellationToken.None).ConfigureAwait(false));
        ValidateEvents(await _eventStore.LoadAsync(aggregateId, AggregateType, CancellationToken.None).ConfigureAwait(false), events);
        _seeds.Add(aggregateId, events);
        return aggregateId;
    }

    private async Task EnsureSchemaAsync()
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync().ConfigureAwait(false);
        using var transaction = connection.BeginTransaction();
#pragma warning disable CA2100 // Generated GUID schema and embedded shipped SQL, no user-controlled SQL.
        await using var createSchema = new SqlCommand($"CREATE SCHEMA [{SchemaName}];", connection, transaction);
        _ = await createSchema.ExecuteNonQueryAsync().ConfigureAwait(false);
        foreach (var name in new[] { "001_CreateEventStoreSchema.sql", "002_CreateSnapshotSchema.sql" })
        {
            await using var stream = typeof(SqlServerEventStoreBenchmarks).Assembly.GetManifestResourceStream($"BenchmarkSchema.{name}")
                ?? throw new InvalidOperationException($"Missing shipped benchmark schema resource: {name}");
            using var reader = new StreamReader(stream);
            var batch = new StringBuilder();
            while (await reader.ReadLineAsync().ConfigureAwait(false) is { } line)
            {
                if (string.Equals(line.Trim(), "GO", StringComparison.OrdinalIgnoreCase))
                {
                    await ExecuteBatchAsync(batch.ToString()).ConfigureAwait(false);
                    batch.Clear();
                }
                else
                {
                    batch.AppendLine(line);
                }
            }

            if (batch.Length > 0)
            {
                await ExecuteBatchAsync(batch.ToString()).ConfigureAwait(false);
            }
        }

        await transaction.CommitAsync().ConfigureAwait(false);
        _ownsSchema = true;

        async Task ExecuteBatchAsync(string sql)
        {
            await using var command = new SqlCommand(sql.Replace("[dbo]", $"[{SchemaName}]", StringComparison.Ordinal), connection, transaction);
            _ = await command.ExecuteNonQueryAsync().ConfigureAwait(false);
        }
#pragma warning restore CA2100
    }
}

internal sealed class BenchmarkSnapshot : ISnapshot
{
    public string? TenantId { get; init; }
    public required string SnapshotId { get; init; }
    public required string AggregateId { get; init; }
    public required string AggregateType { get; init; }
    public required long Version { get; init; }
    public required DateTimeOffset CreatedAt { get; init; }
    public required ReadOnlyMemory<byte> Data { get; init; }
    public IDictionary<string, object>? Metadata { get; init; }
}
