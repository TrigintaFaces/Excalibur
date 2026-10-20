// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project

using System.Data;
using System.Text.RegularExpressions;

using Excalibur.Dispatch;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;

using Excalibur.Dispatch.Messaging;
using Excalibur.Dispatch.Serialization;
using Excalibur.Saga.SqlServer;

using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Excalibur.Benchmarks.Saga;

/// <summary>
/// Benchmarks for SqlServerSagaStore operations.
/// </summary>
/// <remarks>
/// Requires a SQL Server instance. Set the BENCHMARK_SQL_CONNECTIONSTRING environment variable
/// to enable these benchmarks. Setup fails closed when it is absent or whitespace.
/// Each case owns a unique schema built from the embedded canonical saga DDL. This repaired fixture
/// epoch preserves the original five method intents; updates carry the expected optimistic version.
/// </remarks>
[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.HostProcess)]
public partial class SqlServerSagaStoreBenchmarks
{
    private static readonly string? ConnectionString =
        Environment.GetEnvironmentVariable("BENCHMARK_SQL_CONNECTIONSTRING");

    private SqlServerSagaStore? _sagaStore;
    private Guid _preloadedSagaId;
    private Guid _iterationSagaId;
    private string _correlationId = null!;

    private string? _ownedSchema;
    private int _ownedSchemaId;
    private int _databaseId;
    private bool _ownsSchema;
    private long _expectedVersion;

    private static bool IsAvailable => !string.IsNullOrWhiteSpace(ConnectionString);

    [GlobalSetup]
    public void GlobalSetup()
    {
        if (_ownsSchema)
        {
            throw new InvalidOperationException("Saga benchmark already owns a schema; clean it up before setup.");
        }

        if (!IsAvailable)
        {
            throw new InvalidOperationException("SqlServerSagaStoreBenchmarks requires BENCHMARK_SQL_CONNECTIONSTRING.");
        }

        _ownedSchema = $"saga_benchmark_{Guid.NewGuid():N}";
        if (!OwnedSchemaPattern.IsMatch(_ownedSchema))
        {
            throw new InvalidOperationException("Invalid owned benchmark schema identifier.");
        }

        try
        {
            EnsureSchemaAsync().GetAwaiter().GetResult();
            _sagaStore = new SqlServerSagaStore(
                ConnectionString!,
                Options.Create(new SqlServerSagaStoreOptions { SchemaName = _ownedSchema, TableName = "sagas" }),
                NullLogger<SqlServerSagaStore>.Instance,
                new DispatchJsonSerializer(),
                BenchmarkTenantContext.SingleTenant);

            _preloadedSagaId = Guid.NewGuid();
            _correlationId = Guid.NewGuid().ToString();
            var sagaState = new BenchmarkSagaState
            {
                SagaId = _preloadedSagaId,
                OrderId = "order-benchmark-001",
                CorrelationId = _correlationId,
                CurrentStep = "Completed",
                Completed = false,
            };
            _sagaStore.SaveAsync(sagaState, CancellationToken.None).GetAwaiter().GetResult();
            _expectedVersion = sagaState.Version;
        }
        catch (Exception setupFailure)
        {
            try
            {
                CleanupOwnedSchemaAsync().GetAwaiter().GetResult();
            }
            catch (Exception cleanupFailure)
            {
                throw new AggregateException("Saga benchmark setup and owned cleanup both failed.", setupFailure, cleanupFailure);
            }
            throw;
        }
    }

    [GlobalCleanup]
    public void GlobalCleanup() => CleanupOwnedSchemaAsync().GetAwaiter().GetResult();

    [IterationSetup]
    public void IterationSetup()
    {
        _iterationSagaId = Guid.NewGuid();
    }

    #region Load Benchmarks

    /// <summary>
    /// Benchmark: Load an existing saga state by ID.
    /// </summary>
    [Benchmark(Baseline = true)]
    public async Task<BenchmarkSagaState?> LoadSagaState()
    {
        if (!IsAvailable)
        {
            return null;
        }

        return await _sagaStore!.LoadAsync<BenchmarkSagaState>(
            _preloadedSagaId, CancellationToken.None);
    }

    /// <summary>
    /// Benchmark: Load a non-existent saga (miss scenario).
    /// </summary>
    [Benchmark]
    public async Task<BenchmarkSagaState?> LoadSagaStateMiss()
    {
        if (!IsAvailable)
        {
            return null;
        }

        return await _sagaStore!.LoadAsync<BenchmarkSagaState>(
            Guid.NewGuid(), CancellationToken.None);
    }

    #endregion

    #region Save Benchmarks

    /// <summary>
    /// Benchmark: Save a new saga state.
    /// </summary>
    [Benchmark]
    public async Task SaveSagaState()
    {
        if (!IsAvailable)
        {
            return;
        }

        var sagaState = new BenchmarkSagaState
        {
            SagaId = _iterationSagaId,
            OrderId = $"order-{_iterationSagaId:N}",
            CorrelationId = Guid.NewGuid().ToString(),
            CurrentStep = "Started",
            Completed = false,
        };

        await _sagaStore!.SaveAsync(sagaState, CancellationToken.None);
    }

    /// <summary>
    /// Benchmark: Update an existing saga state (upsert).
    /// </summary>
    [Benchmark]
    public async Task UpdateSagaState()
    {
        if (!IsAvailable)
        {
            return;
        }

        var sagaState = new BenchmarkSagaState
        {
            SagaId = _preloadedSagaId,
            Version = _expectedVersion,
            OrderId = "order-benchmark-001",
            CorrelationId = _correlationId,
            CurrentStep = $"Updated-{DateTime.UtcNow.Ticks}",
            Completed = false,
        };

        await _sagaStore!.SaveAsync(sagaState, CancellationToken.None);
        _expectedVersion = sagaState.Version;
    }

    #endregion

    #region Round-Trip Benchmarks

    /// <summary>
    /// Benchmark: Full save-then-load round trip.
    /// </summary>
    [Benchmark]
    public async Task SaveAndLoadRoundTrip()
    {
        if (!IsAvailable)
        {
            return;
        }

        var sagaId = Guid.NewGuid();
        var sagaState = new BenchmarkSagaState
        {
            SagaId = sagaId,
            OrderId = $"order-{sagaId:N}",
            CorrelationId = Guid.NewGuid().ToString(),
            CurrentStep = "RoundTrip",
            Completed = false,
        };

        await _sagaStore!.SaveAsync(sagaState, CancellationToken.None);
        _ = await _sagaStore.LoadAsync<BenchmarkSagaState>(sagaId, CancellationToken.None);
    }

    #endregion

    #region Helpers

    private async Task EnsureSchemaAsync()
    {
        using var resource = typeof(SqlServerSagaStoreBenchmarks).Assembly.GetManifestResourceStream("BenchmarkSchema.01-SagaSchema.sql")
            ?? throw new InvalidOperationException("Canonical saga schema resource is missing.");
        using var reader = new StreamReader(resource);
        var canonical = await reader.ReadToEndAsync().ConfigureAwait(false);
        var schemaToken = CanonicalSchemaTokenPattern;
        if (!schemaToken.IsMatch(canonical))
        {
            throw new InvalidOperationException("Canonical saga schema has no expected dispatch token.");
        }
        var script = schemaToken.Replace(canonical, _ownedSchema!);
        if (schemaToken.IsMatch(script))
        {
            throw new InvalidOperationException("Canonical schema transformation left an unowned schema token.");
        }
        var batches = BatchSeparatorPattern.Split(script);

        await using var connection = new SqlConnection(ConnectionString);
        await connection.OpenAsync().ConfigureAwait(false);
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync().ConfigureAwait(false);
        await using (var guard = new SqlCommand("SELECT CONVERT(int, DB_ID()), SCHEMA_ID(@Schema);", connection, transaction))
        {
            _ = guard.Parameters.Add("@Schema", SqlDbType.NVarChar, 128).Value = _ownedSchema;
            await using var result = await guard.ExecuteReaderAsync().ConfigureAwait(false);
            if (!await result.ReadAsync().ConfigureAwait(false) || result.IsDBNull(0) || !result.IsDBNull(1))
            {
                throw new InvalidOperationException("Owned saga schema must not already exist.");
            }
            _databaseId = result.GetInt32(0);
        }
        foreach (var batch in batches)
        {
            if (string.IsNullOrWhiteSpace(batch))
            {
                continue;
            }
#pragma warning disable CA2100 // Trusted embedded canonical DDL; only replacement is the internally generated, regex-validated schema identifier.
            await using var command = new SqlCommand(batch, connection, transaction);
#pragma warning restore CA2100
            _ = await command.ExecuteNonQueryAsync().ConfigureAwait(false);
        }
        await using (var identity = new SqlCommand("SELECT SCHEMA_ID(@Schema);", connection, transaction))
        {
            _ = identity.Parameters.Add("@Schema", SqlDbType.NVarChar, 128).Value = _ownedSchema;
            _ownedSchemaId = Convert.ToInt32(await identity.ExecuteScalarAsync().ConfigureAwait(false));
            if (_ownedSchemaId <= 0)
            {
                throw new InvalidOperationException("Owned saga schema identity was not created.");
            }
        }
        await transaction.CommitAsync().ConfigureAwait(false);
        _ownsSchema = true;
    }

    private async Task CleanupOwnedSchemaAsync()
    {
        if (!_ownsSchema)
        {
            return;
        }
        if (_ownedSchema is null || !OwnedSchemaPattern.IsMatch(_ownedSchema))
        {
            throw new InvalidOperationException("Owned saga schema identity is invalid; cleanup refused.");
        }
        await using var connection = new SqlConnection(ConnectionString);
        await connection.OpenAsync().ConfigureAwait(false);
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync().ConfigureAwait(false);
        await using (var guard = new SqlCommand("""
            SELECT CASE WHEN DB_ID() = @DatabaseId AND SCHEMA_ID(@Schema) = @SchemaId
                AND OBJECT_ID(QUOTENAME(@Schema) + N'.[sagas]', N'U') IS NOT NULL
                AND NOT EXISTS (
                    SELECT 1 FROM sys.objects
                    WHERE schema_id = @SchemaId
                      AND NOT (name = N'sagas' AND type = N'U')
                      AND NOT (parent_object_id = OBJECT_ID(QUOTENAME(@Schema) + N'.[sagas]', N'U')
                               AND type IN (N'D', N'PK')))
                THEN 1 ELSE 0 END;
            """, connection, transaction))
        {
            _ = guard.Parameters.Add("@DatabaseId", SqlDbType.Int).Value = _databaseId;
            _ = guard.Parameters.Add("@SchemaId", SqlDbType.Int).Value = _ownedSchemaId;
            _ = guard.Parameters.Add("@Schema", SqlDbType.NVarChar, 128).Value = _ownedSchema;
            if (Convert.ToInt32(await guard.ExecuteScalarAsync().ConfigureAwait(false)) != 1)
            {
                throw new InvalidOperationException("Saga schema ownership or object population changed; cleanup refused.");
            }
        }
        // Only this generated schema and its one canonical table are eligible. Unexpected objects fail closed.
#pragma warning disable CA2100 // Generated schema identifier is regex-validated above and its database, identity and object ownership have been checked.
        await using (var drop = new SqlCommand($"DROP TABLE [{_ownedSchema}].[sagas]; DROP SCHEMA [{_ownedSchema}];", connection, transaction))
#pragma warning restore CA2100
        {
            _ = await drop.ExecuteNonQueryAsync().ConfigureAwait(false);
        }
        await transaction.CommitAsync().ConfigureAwait(false);
        _ownsSchema = false;
    }

    [GeneratedRegex("^saga_benchmark_[a-f0-9]{32}$", RegexOptions.CultureInvariant)]
    private static partial Regex OwnedSchemaPattern { get; }

    [GeneratedRegex(@"\bdispatch\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex CanonicalSchemaTokenPattern { get; }

    [GeneratedRegex(@"^\s*GO\s*(?:--[^\r\n]*)?$", RegexOptions.Multiline | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex BatchSeparatorPattern { get; }

    #endregion
}

/// <summary>
/// Test saga state for benchmark scenarios.
/// </summary>
public sealed class BenchmarkSagaState : SagaState
{
    public string OrderId { get; set; } = string.Empty;
    public string CorrelationId { get; set; } = string.Empty;
    public string CurrentStep { get; set; } = string.Empty;
}

