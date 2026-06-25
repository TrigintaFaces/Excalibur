// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Data;

using Excalibur.Dispatch;

using Excalibur.EventSourcing;
using Excalibur.EventSourcing.SqlServer;

using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging.Abstractions;

using Testcontainers.MsSql;

namespace Excalibur.Integration.Tests.EventSourcing.SqlServer;

/// <summary>
/// bd-a2ck2y (S848, Lane K, AC-K.1/EC-K.2/EC-K.4) — independent regression lock (author≠impl,
/// TestsDeveloper) for the store-owned transactional append+outbox seam
/// <see cref="ITransactionalEventStore.AppendWithOutboxStagingAsync"/> on a real SQL Server
/// (TestContainers, serial <c>-m:1</c>).
/// <para>
/// THE atomicity invariant (AC-K.1): events and outbox staging share ONE <see cref="IDbTransaction"/>,
/// so a throw from <c>stageOutbox</c> rolls the WHOLE unit of work back — NEITHER the appended events
/// NOR the staged outbox rows persist. This is RED on a two-transaction impl (events committed on a
/// separate transaction before staging would survive the staging throw) and GREEN only on the
/// single-<see cref="IDbTransaction"/> store-owned impl pinned at GUIDE.
/// </para>
/// <para>
/// Pairs with the Backend impl in <c>SqlServerEventStore.AppendWithOutboxStagingAsync</c> +
/// the relocated public <c>ITransactionalEventStore</c> (Excalibur.EventSourcing.Abstractions).
/// </para>
/// </summary>
[Trait("Category", "Integration")]
[Trait("Database", "SqlServer")]
[Trait("Component", "EventStore")]
public sealed class SqlServerTransactionalAppendAtomicityShould : IAsyncLifetime
{
    private MsSqlContainer? _container;
    private string? _connectionString;
    private bool _dockerAvailable;

    public async ValueTask InitializeAsync()
    {
        try
        {
            _container = new MsSqlBuilder()
                .WithImage("mcr.microsoft.com/mssql/server:2022-latest")
                .Build();

            await _container.StartAsync().ConfigureAwait(false);
            _connectionString = _container.GetConnectionString();
            _dockerAvailable = true;

            await InitializeDatabaseAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Docker initialization failed: {ex.Message}");
            _dockerAvailable = false;
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_container != null)
        {
            try
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
                await _container.DisposeAsync().AsTask().WaitAsync(cts.Token).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Container cleanup failed: {ex.Message}");
            }
        }
    }

    // -------------------------------------------------------------------------------------------------
    // AC-K.1 — atomicity: a throw from stageOutbox rolls back the WHOLE unit of work. Neither the
    // appended events NOR the staged outbox row persist. RED on a two-transaction impl (events would
    // survive the staging throw); GREEN only on the single-IDbTransaction store-owned impl.
    // -------------------------------------------------------------------------------------------------
    [Fact]
    public async Task RollBackEventsAndOutboxWhenStageOutboxThrows()
    {
        if (!_dockerAvailable)
        {
            return;
        }

        var store = (ITransactionalEventStore)CreateEventStore();
        var aggId = "agg-" + Guid.NewGuid().ToString("N");
        const string type = "TestAggregate";
        var outboxId = "ob-" + Guid.NewGuid().ToString("N");

        // stageOutbox stages a real outbox row on the SUPPLIED transaction, THEN throws. On the correct
        // single-transaction impl the throw rolls back the row AND the events together.
        async ValueTask StageThenThrow(IDbTransaction txn, CancellationToken ct)
        {
            await InsertOutboxRowAsync(txn, outboxId, ct).ConfigureAwait(false);
            throw new InvalidOperationException("stage boom");
        }

        // Act — the store surfaces the staging failure to the caller (after rolling back).
        _ = await Should.ThrowAsync<InvalidOperationException>(async () =>
            await store.AppendWithOutboxStagingAsync(
                aggId, type,
                [new TestDomainEvent(aggId, 0), new TestDomainEvent(aggId, 1), new TestDomainEvent(aggId, 2)],
                expectedVersion: -1,
                StageThenThrow,
                CancellationToken.None)).ConfigureAwait(false);

        // Assert — BOTH absent (the whole transaction rolled back).
        (await CountEventsAsync(aggId).ConfigureAwait(false))
            .ShouldBe(0, "events must NOT persist when stageOutbox throws (RED on a two-transaction impl)");
        (await CountOutboxAsync(outboxId).ConfigureAwait(false))
            .ShouldBe(0, "the staged outbox row must roll back with the events");
    }

    // -------------------------------------------------------------------------------------------------
    // EC-K.2 — concurrency conflict: the version check fails BEFORE staging, so stageOutbox is NOT
    // invoked, the result is a concurrency conflict, and nothing new persists.
    // -------------------------------------------------------------------------------------------------
    [Fact]
    public async Task NotInvokeStageOutboxAndPersistNothingOnConcurrencyConflict()
    {
        if (!_dockerAvailable)
        {
            return;
        }

        var store = (ITransactionalEventStore)CreateEventStore();
        var aggId = "agg-" + Guid.NewGuid().ToString("N");
        const string type = "TestAggregate";

        // Seed the aggregate to version 0 (a clean commit, no outbox row staged).
        var seed = await store.AppendWithOutboxStagingAsync(
            aggId, type, [new TestDomainEvent(aggId, 0)], expectedVersion: -1,
            static (_, _) => ValueTask.CompletedTask, CancellationToken.None).ConfigureAwait(false);
        seed.IsConcurrencyConflict.ShouldBeFalse("seed append should succeed");

        var stageInvoked = false;
        var staleOutboxId = "ob-" + Guid.NewGuid().ToString("N");

        // Act — append again with the STALE expectedVersion (-1) while the actual version is now 0.
        var result = await store.AppendWithOutboxStagingAsync(
            aggId, type, [new TestDomainEvent(aggId, 1)], expectedVersion: -1,
            async (txn, ct) =>
            {
                stageInvoked = true;
                await InsertOutboxRowAsync(txn, staleOutboxId, ct).ConfigureAwait(false);
            },
            CancellationToken.None).ConfigureAwait(false);

        // Assert — conflict surfaced, stageOutbox NEVER ran, nothing new persisted.
        result.IsConcurrencyConflict.ShouldBeTrue("a stale expectedVersion must yield a concurrency conflict");
        stageInvoked.ShouldBeFalse("stageOutbox must NOT be invoked on a concurrency conflict (EC-K.2)");
        (await CountEventsAsync(aggId).ConfigureAwait(false)).ShouldBe(1, "only the seed event must remain");
        (await CountOutboxAsync(staleOutboxId).ConfigureAwait(false)).ShouldBe(0, "no outbox row on conflict");
    }

    // -------------------------------------------------------------------------------------------------
    // AC-K.4 / EC-K.4 — the marker contract: SqlServerEventStore IS ITransactionalEventStore (the
    // capability probe `eventStore is ITransactionalEventStore` works), and the marker extends IEventStore
    // so a non-implementing (e.g. non-SqlServer) store remains an unaffected plain IEventStore.
    // -------------------------------------------------------------------------------------------------
    [Fact]
    public void ExposeTransactionalCapabilityViaTheMarkerInterface()
    {
        var store = CreateEventStore();

        store.ShouldBeAssignableTo<ITransactionalEventStore>("SqlServerEventStore must expose the transactional seam");
        typeof(IEventStore).IsAssignableFrom(typeof(ITransactionalEventStore))
            .ShouldBeTrue("ITransactionalEventStore must extend IEventStore so the marker probe is sound and " +
                "non-implementing stores stay unaffected plain IEventStores (EC-K.4)");
    }

    private IEventStore CreateEventStore() =>
        new SqlServerEventStore(_connectionString!, NullLogger<SqlServerEventStore>.Instance);

    private async Task<int> CountEventsAsync(string aggregateId)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync().ConfigureAwait(false);
        await using var command =
            new SqlCommand("SELECT COUNT(*) FROM EventStoreEvents WHERE AggregateId = @id", connection);
        _ = command.Parameters.AddWithValue("@id", aggregateId);
        return Convert.ToInt32(await command.ExecuteScalarAsync().ConfigureAwait(false));
    }

    private async Task<int> CountOutboxAsync(string outboxId)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync().ConfigureAwait(false);
        await using var command =
            new SqlCommand("SELECT COUNT(*) FROM TestOutbox WHERE Id = @id", connection);
        _ = command.Parameters.AddWithValue("@id", outboxId);
        return Convert.ToInt32(await command.ExecuteScalarAsync().ConfigureAwait(false));
    }

    private static async ValueTask InsertOutboxRowAsync(IDbTransaction transaction, string outboxId, CancellationToken ct)
    {
        var sqlTransaction = (SqlTransaction)transaction;
        await using var command = new SqlCommand(
            "INSERT INTO TestOutbox (Id, Payload) VALUES (@id, @p)",
            (SqlConnection)sqlTransaction.Connection!,
            sqlTransaction);
        _ = command.Parameters.AddWithValue("@id", outboxId);
        _ = command.Parameters.AddWithValue("@p", "payload");
        _ = await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }

    private async Task InitializeDatabaseAsync()
    {
        const string createSql = """
            IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='EventStoreEvents' AND xtype='U')
            CREATE TABLE EventStoreEvents (
                Position BIGINT IDENTITY(1,1) PRIMARY KEY,
                EventId NVARCHAR(255) NOT NULL UNIQUE,
                AggregateId NVARCHAR(255) NOT NULL,
                AggregateType NVARCHAR(255) NOT NULL,
                EventType NVARCHAR(500) NOT NULL,
                EventData VARBINARY(MAX) NOT NULL,
                Metadata VARBINARY(MAX) NULL,
                Version BIGINT NOT NULL,
                Timestamp DATETIMEOFFSET NOT NULL,
                INDEX IX_EventStoreEvents_Aggregate (AggregateId, AggregateType, Version)
            );
            IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='TestOutbox' AND xtype='U')
            CREATE TABLE TestOutbox (
                Id NVARCHAR(255) NOT NULL PRIMARY KEY,
                Payload NVARCHAR(MAX) NOT NULL
            );
            """;

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync().ConfigureAwait(false);
        await using var command = new SqlCommand(createSql, connection);
        _ = await command.ExecuteNonQueryAsync().ConfigureAwait(false);
    }

    private sealed record TestDomainEvent : IDomainEvent
    {
        public TestDomainEvent(string aggregateId, long version)
        {
            EventId = Guid.NewGuid().ToString();
            AggregateId = aggregateId;
            Version = version;
            OccurredAt = DateTimeOffset.UtcNow;
            EventType = nameof(TestDomainEvent);
        }

        public string EventId { get; init; }
        public string AggregateId { get; init; }
        public long Version { get; init; }
        public DateTimeOffset OccurredAt { get; init; }
        public string EventType { get; init; }
        public IDictionary<string, object>? Metadata => null;
    }
}
