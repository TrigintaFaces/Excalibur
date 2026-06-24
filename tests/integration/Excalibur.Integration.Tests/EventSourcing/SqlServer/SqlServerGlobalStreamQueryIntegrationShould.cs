// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Reflection;

using Excalibur.Dispatch;

using Excalibur.EventSourcing;
using Excalibur.EventSourcing.Queries;
using Excalibur.EventSourcing.SqlServer;
using Excalibur.EventSourcing.SqlServer.DependencyInjection;

using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using Testcontainers.MsSql;

namespace Excalibur.Integration.Tests.EventSourcing.SqlServer;

/// <summary>
/// bd-wimxhb (S840, AC-9) — independent regression lock (author≠impl, TestsDeveloper) for
/// <c>SqlServerGlobalStreamQuery</c> against a real SQL Server (TestContainers).
/// <para>
/// The global stream MUST be paged and ordered by the global ordinal (<c>Position</c>, an IDENTITY
/// column surfaced as <see cref="StoredEvent.GlobalPosition"/>) — NOT by the per-aggregate
/// <c>Version</c>. The pre-fix query ordered/paged by <c>Version</c>, which interleaves and skips events
/// across aggregates (a global stream is not per-aggregate). This lock appends interleaved
/// multi-aggregate events and asserts they read back exactly once, in global append order, across a
/// page boundary. RED on the pre-fix (Version-ordered) query.
/// </para>
/// </summary>
[Trait("Category", "Integration")]
[Trait("Database", "SqlServer")]
[Trait("Component", "EventStore")]
public sealed class SqlServerGlobalStreamQueryIntegrationShould : IAsyncLifetime
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

    [Fact]
    public async Task ReadEventsAcrossAggregatesInGlobalPositionOrderAcrossAPageBoundary()
    {
        if (!_dockerAvailable)
        {
            return;
        }

        // Arrange — append INTERLEAVED so the global (append/Position) order differs from per-aggregate
        // Version order. Aggregate A gets 3 events (Position 1,2,3), then aggregate B gets 1 (Position 4).
        //   Global Position order : A.v0, A.v1, A.v2, B.v0
        //   Per-aggregate Version : 0,    1,    2,    0     → Version-ordered would be A.v0, B.v0, A.v1, A.v2
        var eventStore = CreateEventStore();
        var aggA = "agg-A-" + Guid.NewGuid().ToString("N");
        var aggB = "agg-B-" + Guid.NewGuid().ToString("N");
        const string type = "TestAggregate";

        _ = await eventStore.AppendAsync(aggA, type,
            [new TestDomainEvent(aggA, 0), new TestDomainEvent(aggA, 1), new TestDomainEvent(aggA, 2)],
            -1, CancellationToken.None);
        _ = await eventStore.AppendAsync(aggB, type,
            [new TestDomainEvent(aggB, 0)], -1, CancellationToken.None);

        var query = CreateGlobalStreamQuery();

        // Act — full read in global order.
        var all = await query.ReadAllAsync(GlobalStreamPosition.Start, 100, CancellationToken.None);

        // Assert — exactly 4 events, GlobalPosition strictly increasing (the global ordinal is populated),
        // and the order is the global APPEND order (A,A,A,B) — NOT the Version order (A,B,A,A).
        all.Count.ShouldBe(4);
        all.Select(e => e.GlobalPosition).ShouldBe(all.Select(e => e.GlobalPosition).OrderBy(p => p), "GlobalPosition must be ascending");
        all.Select(e => e.GlobalPosition).Distinct().Count().ShouldBe(4, "no duplicate positions");
        all[0].AggregateId.ShouldBe(aggA);
        all[1].AggregateId.ShouldBe(aggA);
        all[2].AggregateId.ShouldBe(aggA);
        all[3].AggregateId.ShouldBe(aggB); // RED pre-fix: Version-order would put B.v0 at index 1

        // Page boundary — read in pages of 2 from the global position; the two pages must be contiguous,
        // each event exactly once, no skip/dup across the boundary.
        var page1 = await query.ReadAllAsync(GlobalStreamPosition.Start, 2, CancellationToken.None);
        page1.Count.ShouldBe(2);
        var nextPos = new GlobalStreamPosition(page1[^1].GlobalPosition + 1, default);
        var page2 = await query.ReadAllAsync(nextPos, 2, CancellationToken.None);
        page2.Count.ShouldBe(2);

        var paged = page1.Concat(page2).Select(e => e.EventId).ToList();
        paged.Distinct().Count().ShouldBe(4, "no duplicate events across the page boundary");
        paged.ShouldBe(all.Select(e => e.EventId).ToList(), "paged read must match the global-order full read");
    }

    private IEventStore CreateEventStore() =>
        new SqlServerEventStore(_connectionString!, NullLogger<SqlServerEventStore>.Instance);

    // SqlServerGlobalStreamQuery is internal; this integration-test assembly is not a friend assembly,
    // so construct it via its internal ctor (Func<SqlConnection>, IOptions<SqlServerEventSourcingOptions>)
    // and use it through the public IGlobalStreamQuery contract — mirroring the reflection pattern used
    // for other internal CDC/event-sourcing components in the test suite. Options use the defaults
    // (dbo.EventStoreEvents), which match SqlServerEventStore's defaults and the table created below.
    private IGlobalStreamQuery CreateGlobalStreamQuery()
    {
        var queryType = typeof(SqlServerEventStore).Assembly
            .GetType("Excalibur.EventSourcing.SqlServer.SqlServerGlobalStreamQuery")
            ?? throw new InvalidOperationException("Expected internal SqlServerGlobalStreamQuery type.");

        Func<SqlConnection> connectionFactory = () => new SqlConnection(_connectionString!);
        var options = Options.Create(new SqlServerEventSourcingOptions());

        return (IGlobalStreamQuery)Activator.CreateInstance(
            queryType,
            BindingFlags.NonPublic | BindingFlags.Instance,
            binder: null,
            args: [connectionFactory, options],
            culture: null)!;
    }

    private async Task InitializeDatabaseAsync()
    {
        const string createTableSql = """
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
            )
            """;

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync().ConfigureAwait(false);
        await using var command = new SqlCommand(createTableSql, connection);
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
