// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Data;
using Tests.Shared.Fixtures;

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
    private readonly RequiredContainer _requiredContainer = new("SQL Server (Docker)");

    public async ValueTask InitializeAsync()
    {
        try
        {
            _container = new MsSqlBuilder()
                .WithBoundedMemory()
                .WithImage("mcr.microsoft.com/mssql/server:2022-CU26-ubuntu-22.04")
                .Build();

            await _container.StartAsync().ConfigureAwait(false);
            _connectionString = _container.GetConnectionString();
            _requiredContainer.MarkStarted();

            await InitializeDatabaseAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            throw _requiredContainer.Failed(ex);
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
        _requiredContainer.Require();

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
        _requiredContainer.Require();

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
    // k44na4 — TRUE concurrent race, unlike the test above: every writer presents the SAME (correct at
    // the time it reads) expectedVersion, so ALL of them pass the deterministic pre-check inside their
    // own transaction — the race is decided by SQL Server's own key locking at INSERT/COMMIT, not by a
    // stale precondition an app-level check could catch early. No delay hook is needed: N writers all
    // insert the identical (AggregateId, Version) row; SQL Server serialises them on that key, exactly
    // one commits, and every other transaction's INSERT then fails on the unique constraint once it
    // unblocks. The property under test is what happens to the losers.
    // -------------------------------------------------------------------------------------------------
    [Fact]
    public async Task ClassifyATrueConcurrentRace_AsConcurrencyConflict_NotARawException()
    {
        _requiredContainer.Require();

        var store = (ITransactionalEventStore)CreateEventStore();
        var aggId = "agg-" + Guid.NewGuid().ToString("N");
        const string type = "TestAggregate";
        const int concurrency = 8;

        var tasks = Enumerable.Range(0, concurrency).Select(async i =>
        {
            var outboxId = $"ob-{i}-" + Guid.NewGuid().ToString("N");
            try
            {
                var result = await store.AppendWithOutboxStagingAsync(
                    aggId, type, [new TestDomainEvent(aggId, 0)], expectedVersion: -1,
                    async (txn, ct) => await InsertOutboxRowAsync(txn, outboxId, ct).ConfigureAwait(false),
                    CancellationToken.None).ConfigureAwait(false);
                return (Result: (AppendResult?)result, Exception: (Exception?)null);
            }
            catch (Exception ex)
            {
                return (Result: (AppendResult?)null, Exception: ex);
            }
        }).ToArray();

        var outcomes = await Task.WhenAll(tasks).ConfigureAwait(false);

        var successes = outcomes.Where(o => o.Result is { IsConcurrencyConflict: false }).ToList();
        var conflicts = outcomes.Where(o => o.Result is { IsConcurrencyConflict: true }).ToList();
        var rawExceptions = outcomes.Where(o => o.Exception is not null).ToList();

        successes.Count.ShouldBe(1,
            "exactly one of the concurrent writers must win the race and commit version 0");
        (conflicts.Count + rawExceptions.Count).ShouldBe(concurrency - 1,
            "every losing writer must be accounted for as either a classified conflict or a raw exception");

        // THE ASSERTION k44na4 EXISTS FOR: a loser of a TRUE race (past the pre-check, decided by the
        // database's own locking) must be classified via IsLostRace exactly like the plain AppendAsync
        // path, not surfaced to the caller as a raw, unclassified SqlException.
        rawExceptions.ShouldBeEmpty(
            "a true concurrent race must classify as AppendResult.CreateConcurrencyConflict, matching the " +
            "plain-append contract — not surface as a raw exception. Raw exception types seen: " +
            string.Join(", ", rawExceptions.Select(o => o.Exception!.GetType().Name)));

        (await CountEventsAsync(aggId).ConfigureAwait(false)).ShouldBe(1,
            "only the single winning writer's event must persist at version 0");
    }

    // AC-K.4 / EC-K.4 marker-contract assertions are container-independent (pure type checks) and live in
    // the fixture-less SqlServerEventStoreMarkerContractShould below — bd-2iva37: a pure type-assertion must
    // not be collateral damage of a TestContainers start cascade under full-suite load.

    private IEventStore CreateEventStore() =>
        new SqlServerEventStore(_connectionString!, NullLogger<SqlServerEventStore>.Instance, SingleTenantTestContext.Instance);

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
        // The event-store table comes from the shipped DDL; TestOutbox is this suite's own fixture and
        // has no shipped counterpart, so it stays here.
        await ShippedEventStoreSchema.EnsureCreatedAsync(_connectionString, CancellationToken.None)
            .ConfigureAwait(false);

        const string createSql = """
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

[MessageName("Test.SqlServerTransactionalAppendAtomicity.TestDomainEvent")]
private sealed record TestDomainEvent : IDomainEvent
    {
        public TestDomainEvent(string aggregateId, long version)
        {
            EventId = Guid.NewGuid().ToString();
            AggregateId = aggregateId;
            Version = version;
            OccurredAt = DateTimeOffset.UtcNow;
        }

        public string EventId { get; init; }
        public string AggregateId { get; init; }
        public long Version { get; init; }
        public DateTimeOffset OccurredAt { get; init; }
        public IDictionary<string, object>? Metadata => null;
    }
}

/// <summary>
/// bd-2iva37 — container-independent marker-contract lock for AC-K.4 / EC-K.4, extracted from
/// <see cref="SqlServerTransactionalAppendAtomicityShould"/> so a pure type-assertion is NOT collateral
/// damage of a TestContainers MsSql start cascade under full-suite container load (the reported P3 flake).
/// This class owns no fixture / <c>IAsyncLifetime</c> and touches no database — the store's connection
/// factory is lazy, so a dummy connection string is never opened.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "EventStore")]
public sealed class SqlServerEventStoreMarkerContractShould
{
    // AC-K.4 / EC-K.4 — SqlServerEventStore IS ITransactionalEventStore (the capability probe
    // `eventStore is ITransactionalEventStore` works), and the marker extends IEventStore so a
    // non-implementing (e.g. non-SqlServer) store remains an unaffected plain IEventStore.
    [Fact]
    public void ExposeTransactionalCapabilityViaTheMarkerInterface()
    {
        // Lazy connection factory ⇒ never opened for a pure type assertion; no container required.
        IEventStore store = new SqlServerEventStore(
            "Server=unused;Database=unused;", NullLogger<SqlServerEventStore>.Instance,
            SingleTenantTestContext.Instance);

        store.ShouldBeAssignableTo<ITransactionalEventStore>("SqlServerEventStore must expose the transactional seam");
        typeof(IEventStore).IsAssignableFrom(typeof(ITransactionalEventStore))
            .ShouldBeTrue("ITransactionalEventStore must extend IEventStore so the marker probe is sound and " +
                "non-implementing stores stay unaffected plain IEventStores (EC-K.4)");
    }
}
