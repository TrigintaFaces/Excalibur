// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.IO;

using Excalibur.Dispatch;
using Excalibur.EventSourcing.Sqlite;

using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.EventSourcing.Tests.Sqlite;

/// <summary>
/// Author≠impl regression lock (bead w8admm) proving <see cref="SqliteTableInitializer"/> tracks
/// "table created" per physical <em>(database file, table name)</em> pair rather than a process-global
/// static flag.
/// </summary>
/// <remarks>
/// The pre-fix impl held two bare <c>static volatile bool</c> flags, keyed by nothing: the first store in
/// the process created its table and set the flag; every subsequent store — a different table name, or a
/// different database file — saw the flag set, skipped <c>CREATE TABLE</c>, and hit "no such table" on its
/// first read/write. These locks are non-vacuous against that code: each exercises a SECOND store whose
/// table the pre-fix flag would have skipped (different table on the same file; and the same table name on
/// a different file) and asserts that second store can append + load — which fails RED under the global
/// flag and passes GREEN once initialization is keyed by (connection string, table).
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
[Trait("Database", "Sqlite")]
public sealed class SqliteTableInitializerKeyedInitShould : IDisposable
{
    private const string AggregateType = "KeyedInitAggregate";

    private readonly List<string> _files = [];
    private bool _disposed;

    private string NewDbPath()
    {
        var path = Path.Combine(Path.GetTempPath(), $"excalibur-keyedinit-{Guid.NewGuid():N}.db");
        _files.Add(path);
        return path;
    }

    private static SqliteEventStore Store(string connectionString, string table) =>
        new(connectionString, NullLogger<SqliteEventStore>.Instance, TestTenantContext.SingleTenantDefault, Microsoft.Extensions.Options.Options.Create(new TenantContextOptions { RequireTenant = false }), table);

    [Fact]
    public async Task InitializeSecondTable_OnSameFile_WhenFirstTableAlreadyInitialized()
    {
        // Same database file, two DIFFERENT table names. Pre-fix: store A ("Events…") creates + sets the
        // global flag; store B ("Orders…") skips CREATE → "no such table" on append. Keyed init: B creates its own.
        var cs = $"Data Source={NewDbPath()}";
        var tableA = $"Events_{Guid.NewGuid():N}";
        var tableB = $"Orders_{Guid.NewGuid():N}";

        await AppendOneAsync(Store(cs, tableA), Guid.NewGuid().ToString()).ConfigureAwait(false);

        var storeB = Store(cs, tableB);
        var aggB = Guid.NewGuid().ToString();
        await AppendOneAsync(storeB, aggB).ConfigureAwait(false); // must not throw "no such table"

        var loaded = await storeB.LoadAsync(aggB, AggregateType, CancellationToken.None).ConfigureAwait(false);
        loaded.Count.ShouldBe(1);
    }

    [Fact]
    public async Task InitializeSameTableName_OnDifferentFiles_Independently()
    {
        // SAME table name, two DIFFERENT files. Pre-fix: file1 creates + flags the name globally; file2
        // skips CREATE on its own empty file → "no such table". Keyed init: each file creates its own.
        var table = $"Events_{Guid.NewGuid():N}";
        var cs1 = $"Data Source={NewDbPath()}";
        var cs2 = $"Data Source={NewDbPath()}";

        await AppendOneAsync(Store(cs1, table), Guid.NewGuid().ToString()).ConfigureAwait(false);

        var store2 = Store(cs2, table);
        var agg2 = Guid.NewGuid().ToString();
        await AppendOneAsync(store2, agg2).ConfigureAwait(false); // must not throw "no such table"

        var loaded = await store2.LoadAsync(agg2, AggregateType, CancellationToken.None).ConfigureAwait(false);
        loaded.Count.ShouldBe(1);
    }

    [Fact]
    public async Task CreateTableExactlyOnce_WhenManyStoresOnSameKeyInitializeConcurrently()
    {
        // AC-w3 concurrency: N stores on the SAME (file, table) initialize in parallel. Exactly-once
        // creation must hold (SemaphoreSlim + idempotent CREATE IF NOT EXISTS) and none may observe a
        // missing table. A keepAlive connection holds the file (and its default journal) alive for the run.
        var path = NewDbPath();
        var cs = $"Data Source={path}";
        var table = $"Events_{Guid.NewGuid():N}";

        await using var keepAlive = new SqliteConnection(cs);
        await keepAlive.OpenAsync().ConfigureAwait(false);

        var tasks = Enumerable.Range(0, 16)
            .Select(_ => AppendOneAsync(Store(cs, table), Guid.NewGuid().ToString()))
            .ToArray();

        // All concurrent initializers succeed — no "no such table", no exception.
        await Should.NotThrowAsync(() => Task.WhenAll(tasks));
    }

    private static async Task AppendOneAsync(SqliteEventStore store, string aggregateId)
    {
        var evt = new TestDomainEvent
        {
            EventId = Guid.NewGuid().ToString(),
            AggregateId = aggregateId,
            OccurredAt = DateTimeOffset.UtcNow,
            Data = "keyed-init",
        };

        var result = await store.AppendAsync(aggregateId, AggregateType, [evt], expectedVersion: -1, CancellationToken.None)
            .ConfigureAwait(false);
        result.Success.ShouldBeTrue("append on a freshly-keyed store must create its table and succeed");
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        try
        {
            SqliteConnection.ClearAllPools();
        }
        catch (Exception)
        {
            // Best-effort pool clear so files unlock for deletion.
        }

        foreach (var file in _files)
        {
            try
            {
                if (File.Exists(file))
                {
                    File.Delete(file);
                }
            }
            catch (IOException)
            {
                // Best-effort cleanup.
            }
            catch (UnauthorizedAccessException)
            {
                // Best-effort cleanup.
            }
        }
    }
}
