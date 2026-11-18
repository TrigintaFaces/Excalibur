// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Runtime.CompilerServices;
using System.Text.Json;
using Excalibur.Cdc;
using Excalibur.Dispatch;
using Excalibur.Cdc.SqlServer;
using Excalibur.Data.DataProcessing;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using MsOptions = Microsoft.Extensions.Options.Options;

namespace Excalibur.Dispatch.Aot.Sample;

/// <summary>Native SQL controls for a disposable root-owned local SQL Server database.</summary>
internal static class NativeSqlPathsSmoke
{
    internal static async Task RunAsync()
    {
        Require(!RuntimeFeature.IsDynamicCodeSupported && !JsonSerializer.IsReflectionEnabledByDefault, "Run the actual native executable with reflection JSON disabled.");
        var text = Environment.GetEnvironmentVariable("ARB_NATIVE_SQL") ?? throw new InvalidOperationException("ARB_NATIVE_SQL must identify a root-owned disposable SQL container database.");
        var builder = new SqlConnectionStringBuilder(text);
        var database = builder.InitialCatalog;
        Require(Environment.GetEnvironmentVariable("ARB_NATIVE_DISPOSABLE") == "1", "Explicit disposable environment acknowledgment is required.");
        Require(database.StartsWith("ARBNative_", StringComparison.Ordinal) && Guid.TryParseExact(database[10..], "N", out _), "Database must be uniquely named ARBNative_<32 hexadecimal digits>.");
        var server = builder.DataSource.StartsWith("tcp:", StringComparison.OrdinalIgnoreCase) ? builder.DataSource[4..] : builder.DataSource;
        Require(server.Split(',')[0] is "localhost" or "127.0.0.1", "Only a local disposable container endpoint is accepted.");
        using var deadline = new CancellationTokenSource(TimeSpan.FromMinutes(2));
        var token = deadline.Token;
        var schema = $"Native_{Guid.NewGuid():N}";
        var capture = $"native_{Guid.NewGuid():N}";
        await using var sql = new SqlConnection(text);
        await sql.OpenAsync(token);
        await ExecuteAsync(sql, $"""
            CREATE SCHEMA [{schema}];
            """, token);
        await ExecuteAsync(sql, $"""
            CREATE TABLE [{schema}].[State](
                MessageId bigint IDENTITY PRIMARY KEY,
                DatabaseConnectionIdentifier nvarchar(128) NOT NULL,
                DatabaseName nvarchar(128) NOT NULL, TableName nvarchar(128) NOT NULL,
                LastProcessedLsn binary(10) NOT NULL, LastProcessedSequenceValue binary(10) NULL,
                LastCommitTime datetime2 NULL, FencingToken bigint NULL,
                ProcessedAt datetimeoffset NOT NULL DEFAULT SYSDATETIMEOFFSET(),
                UNIQUE(DatabaseConnectionIdentifier, DatabaseName, TableName));
            CREATE TABLE [{schema}].[Tasks](
                DataTaskId uniqueidentifier PRIMARY KEY, CreatedAt datetimeoffset NOT NULL,
                RecordType nvarchar(256) NOT NULL, Attempts int NOT NULL DEFAULT 0,
                MaxAttempts int NOT NULL, CompletedCount bigint NOT NULL DEFAULT 0,
                FetchCursor nvarchar(max) NULL, ProcessedCursor nvarchar(max) NULL);
            CREATE TABLE [{schema}].[Source](Id int PRIMARY KEY, Payload nvarchar(64) NULL);
            """, token);
        await VerifyStateAsync(text, schema, token);
        Console.WriteLine("PASS native checkpoint state, fencing and generic adapters.");
        await VerifyManagerAsync(text, schema, sql, token);
        Console.WriteLine("PASS native orchestration failure, durable progress, scoped disposal and recovery.");
        // CDC enablement is deliberately confined to the guarded disposable database.
        await ExecuteAsync(sql, "EXEC sys.sp_cdc_enable_db;", token);
        await ExecuteAsync(sql, $"""
            EXEC sys.sp_cdc_enable_table @source_schema=N'{schema}', @source_name=N'Source',
                @role_name=NULL, @capture_instance=N'{capture}', @supports_net_changes=0;
            INSERT [{schema}].[Source](Id,Payload) VALUES(7,N'雪-native');
            """, token);
        await VerifyRepositoryAsync(text, capture, token);
        Console.WriteLine("PASS native SQL public CDC repository/state and orchestration controls.");
        Console.WriteLine("COVERAGE LIMIT: internal legacy DeleteDataTask/UpdateDataTaskAttempts/UpdateDataTaskCompletedCount are not reached by the public manager; their direct controls remain separate.");
        // No destructive cleanup: the root owns and removes this entire unique disposable database/container.
    }

    private static async Task VerifyStateAsync(string text, string schema, CancellationToken token)
    {
        await using var store = new CdcStateStore(() => new SqlConnection(text), MsOptions.Create(new SqlServerCdcStateStoreOptions { SchemaName = schema, TableName = "State" }));
        var lsn = new byte[10]; lsn[9] = 7;
        var sequence = new byte[10]; sequence[9] = 3;
        Require(await store.UpdateLastProcessedPositionAsync("read", "source", "logical", lsn, sequence, null, 9, token) == 1, "Initial fenced checkpoint did not advance.");
        var later = (byte[])lsn.Clone(); later[9] = 8;
        Require(await store.UpdateLastProcessedPositionAsync("read", "source", "logical", later, null, null, 8, token) == 0, "Stale fencing token was accepted.");
        var row = (await store.GetLastProcessedPositionAsync("read", "source", token)).Single();
        Require(row.LastProcessedLsn.SequenceEqual(lsn) && row.LastProcessedSequenceValue!.SequenceEqual(sequence), "Refused checkpoint mutated state.");
        Require(row.LastCommitTime == default && row.ProcessedAt != default, "Nullable commit time/canonical datetimeoffset mapping failed.");
        var commit = new DateTime(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc);
        Require(await store.UpdateLastProcessedPositionAsync("read", "source", "logical", later, null, commit, 10, token) == 1, "Newer fenced checkpoint failed.");
        row = (await store.GetLastProcessedPositionAsync("read", "source", token)).Single();
        Require(row.LastProcessedLsn.SequenceEqual(later) && row.LastProcessedSequenceValue is null && row.LastCommitTime == commit, "Checkpoint read/write mapping changed.");
        ICdcStateStore generic = store;
        Require(await generic.GetPositionAsync("consumer", token) is null, "Absent position must remain absent.");
        Require(await generic.SavePositionAsync("consumer", new CdcPosition(lsn, sequence).ToChangePosition(), token) == CdcPositionSaveOutcome.Advanced, "Generic position save failed.");
        var roundtrip = CdcPosition.FromChangePosition(await generic.GetPositionAsync("consumer", token));
        Require(roundtrip.Lsn.SequenceEqual(lsn) && roundtrip.SequenceValue!.SequenceEqual(sequence), "Generic checkpoint roundtrip changed bytes.");
        var positions = new List<string>();
        await foreach (var entry in generic.GetAllPositionsAsync(token)) { positions.Add(entry.ConsumerId); }
        Require(positions.SequenceEqual(new[] { "consumer" }), "Generic sentinel enumeration leaked provider rows.");
        Require(await generic.DeletePositionAsync("consumer", token) && !await generic.DeletePositionAsync("consumer", token), "Generic delete cardinality changed.");
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Security", "CA2100:Review SQL queries for security vulnerabilities",
        Justification = "The schema is generated locally from a GUID; task values are parameterized by the production manager.")]
    private static async Task VerifyManagerAsync(string text, string schema, SqlConnection sql, CancellationToken token)
    {
        var progress = new NativeProgress();
        await using var provider = new ServiceCollection()
            .AddTransient<NativeProcessor>(_ => new NativeProcessor(progress))
            .BuildServiceProvider();
        var manager = new DataOrchestrationManager(() => new SqlConnection(text), new NativeRegistry(), provider,
            MsOptions.Create(new DataProcessingOptions { SchemaName = schema, TableName = "Tasks" }), NullLogger<DataOrchestrationManager>.Instance);
        var id = await manager.AddDataTaskForRecordTypeAsync("native", token);
        Require(id != Guid.Empty, "Enqueue returned an empty identifier.");
        var failureObserved = false;
        try
        {
            await manager.ProcessDataTasksAsync(token);
        }
        catch (AggregateException error) when (error.InnerExceptions.Count == 1 &&
            error.InnerException is InvalidOperationException { Message: "native-planned-failure" })
        {
            failureObserved = true;
        }
        Require(failureObserved, "The planned processing failure was not surfaced to the caller.");
        await using (var observer = sql.CreateCommand())
        {
            observer.CommandText = $"SELECT Attempts, CompletedCount, ProcessedCursor FROM [{schema}].[Tasks] WHERE DataTaskId=@id;";
            observer.Parameters.Add(new SqlParameter("@id", System.Data.SqlDbType.UniqueIdentifier) { Value = id });
            observer.CommandTimeout = 15;
            await using var reader = await observer.ExecuteReaderAsync(token);
            Require(await reader.ReadAsync(token), "Failed task disappeared before recovery.");
            Require(reader.GetInt32(0) == 1 && reader.GetInt64(1) == 1 && reader.GetString(2) == "native-cursor-雪", "Failure lost the durable attempt/count/cursor checkpoint.");
            Require(!await reader.ReadAsync(token), "Enqueue produced duplicate task rows.");
        }
        await manager.ProcessDataTasksAsync(token);
        Require(progress.Calls == 2 && progress.Disposals == 2, "Recovery did not execute and dispose both processor instances.");
        await using var command = sql.CreateCommand();
        command.CommandText = $"SELECT COUNT(*) FROM [{schema}].[Tasks];";
        command.CommandTimeout = 15;
        Require((int)(await command.ExecuteScalarAsync(token))! == 0, "Completed task was not removed.");
    }

    private static async Task VerifyRepositoryAsync(string text, string capture, CancellationToken token)
    {
        await using var repository = new CdcRepository(new SqlConnection(text));
        byte[] from;
        byte[] to;
        while (true)
        {
            token.ThrowIfCancellationRequested();
            from = await repository.GetMinPositionAsync(capture, token);
            to = await repository.GetMaxPositionAsync(token);
            if (from is { Length: 10 } && to is { Length: 10 } && from.Any(b => b != 0) && to.Any(b => b != 0) &&
                await repository.ChangesExistAsync(from, to, new[] { capture }, token)) { break; }
            await Task.Delay(TimeSpan.FromMilliseconds(250), token);
        }
        var rows = (await repository.FetchChangesAsync(capture, 8, from, to, null, (CdcOperationCodes)0, token)).ToArray();
        Require(rows.Length == 1 && rows[0].TableName == capture && (int)rows[0].OperationCode == 2, "Native CDC row identity/operation mapping changed.");
        Require((int)rows[0].Changes["Id"] == 7 && (string)rows[0].Changes["Payload"] == "雪-native", "Native CDC payload mapping changed.");
        Require(rows[0].DataTypes["Id"] == typeof(int) && rows[0].DataTypes["Payload"] == typeof(string), "Native CDC static schema mapping changed.");
        var next = await repository.GetNextLsnAsync(new byte[10], token);
        Require(next.Length == 10, "Increment-LSN scalar mapping changed.");
        var first = await repository.GetNextLsnAsync(capture, new byte[10], token);
        Require(first is not null && first.SequenceEqual(rows[0].Lsn), "Capture-instance next-LSN query changed.");
        var commit = await repository.GetLsnToTimeAsync(rows[0].Lsn, token);
        Require(commit.HasValue, "LSN-to-time scalar mapping failed.");
        Require(await repository.GetTimeToLsnAsync(commit!.Value, "smallest greater than or equal", token) is not null, "Time-to-LSN scalar mapping failed.");
        var valid = await repository.GetNextValidLsnAsync(new byte[10], token);
        Require(valid is { Length: 10 } && valid.Any(b => b != 0), "Next valid LSN mapping returned no usable position.");
        var resumed = await repository.FetchChangesAsync(capture, 8, rows[0].Lsn, to, rows[0].SeqVal, rows[0].OperationCode, token);
        Require(!resumed.Any(), "CDC resume replayed the already-consumed insert.");
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Security", "CA2100:Review SQL queries for security vulnerabilities",
        Justification = "Private callers provide fixed smoke SQL and locally generated GUID schema/capture names in a guarded disposable database.")]
    private static async Task ExecuteAsync(SqlConnection sql, string text, CancellationToken token)
    {
        await using var command = sql.CreateCommand();
        command.CommandText = text;
        command.CommandTimeout = 30;
        await command.ExecuteNonQueryAsync(token);
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) { throw new InvalidOperationException(message); }
    }

    private sealed class NativeProgress
    {
        internal int Calls;
        internal int Disposals;
    }

    private sealed class NativeRegistry : IDataProcessorRegistry
    {
        public bool TryGetFactory(string recordType, out Func<IServiceProvider, IDataProcessor> processor)
        {
            processor = GetFactory(recordType);
            return true;
        }
        public Func<IServiceProvider, IDataProcessor> GetFactory(string recordType) =>
            recordType == "native" ? static services => services.GetRequiredService<NativeProcessor>() : throw new InvalidOperationException("Unexpected record type.");
    }

    private sealed class NativeProcessor(NativeProgress progress) : IDataProcessor
    {
        public async Task<long> RunAsync(long completedCount, string? processedCursor, UpdateCompletedCount update, CancellationToken token)
        {
            var call = Interlocked.Increment(ref progress.Calls);
            if (call == 1)
            {
                Require(completedCount == 0 && processedCursor is null, "Initial task progress was not empty.");
                await update(1, "native-cursor-雪", token);
                throw new InvalidOperationException("native-planned-failure");
            }
            Require(call == 2 && completedCount == 1 && processedCursor == "native-cursor-雪", "Recovery did not resume from its durable progress.");
            await update(2, null, token);
            return 2;
        }
        public ValueTask DisposeAsync()
        {
            Interlocked.Increment(ref progress.Disposals);
            return ValueTask.CompletedTask;
        }
    }
}
