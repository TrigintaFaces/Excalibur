// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.EventSourcing;
using Microsoft.Extensions.Logging.Abstractions;
using Tests.Shared.Fixtures;
using Excalibur.Integration.Tests.Data.EventStore;
using Excalibur.EventSourcing.Sqlite;
using Excalibur.Testing.Conformance;
using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.Integration.Tests.EventSourcing.Sqlite;

[Collection(SqliteEventStoreTestCollection.CollectionName)]
[Trait("Category", "Integration")]
[Trait("Component", "EventStore")]
[Trait("Database", "Sqlite")]
[Trait("Pattern", "TransactionalInline")]
public sealed class SqliteTransactionalProjectionConformanceShould(SqliteEventStoreFixture fixture)
    : TransactionalProjectionConformanceTestKit, IClassFixture<SqliteEventStoreFixture>, IAsyncLifetime
{
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2213:Disposable fields should be disposed", Justification = "xUnit owns and disposes the class fixture.")]
    private readonly SqliteEventStoreFixture _fixture = fixture;
    private readonly List<ServiceProvider> _providers = [];
    private readonly List<AsyncServiceScope> _scopes = [];

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;

    [Theory]
    [InlineData("Data Source=:memory:")]
    [InlineData("Data Source=:memory:;Mode=Memory;Cache=Shared")]
    public async Task RejectPrivateMemoryEvenWhenSharedFlagsArePresent(string connectionString)
    {
        await using var connection = new Microsoft.Data.Sqlite.SqliteConnection(connectionString);
        await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        var store = new SqliteProjectionStore<ConformanceProjection>(connectionString, SingleTenantTestContext.Instance, "PrivateModel");
        var exception = await Should.ThrowAsync<InvalidOperationException>(async () =>
            await store.WriteAsync(new ProjectionTransaction(transaction, [], SingleTenantTestContext.Instance.TenantId!, "[Events]"),
                "row", "Aggregate", "{}", CancellationToken.None));
        exception.Message.ShouldContain("same SQLite file");
    }

    [Fact]
    public async Task RejectAnotherFileBeforeAttemptingAnyProjectionWrite()
    {
        await using var connection = new Microsoft.Data.Sqlite.SqliteConnection(_fixture.ConnectionString);
        await connection.OpenAsync();
        Path.IsPathFullyQualified(connection.DataSource).ShouldBeTrue("The opened provider exposes the actual absolute file identity.");
        await using var transaction = await connection.BeginTransactionAsync();
        var other = new Microsoft.Data.Sqlite.SqliteConnectionStringBuilder(_fixture.ConnectionString)
        { DataSource = connection.DataSource + ".other" };
        var store = new SqliteProjectionStore<ConformanceProjection>(other.ConnectionString, SingleTenantTestContext.Instance, "OtherModel");
        var exception = await Should.ThrowAsync<InvalidOperationException>(async () =>
            await store.WriteAsync(new ProjectionTransaction(transaction, [], SingleTenantTestContext.Instance.TenantId!, "[Events]"),
                "row", "Aggregate", "{}", CancellationToken.None));
        exception.Message.ShouldContain("same SQLite file");
        File.Exists(other.DataSource).ShouldBeFalse("Validation must not open or create a second database.");
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var scope in _scopes) { await scope.DisposeAsync(); }
        foreach (var provider in _providers) { await provider.DisposeAsync(); }
    }

    protected override async Task<bool> ObserveHistoryContentionAsync(System.Data.Common.DbConnection blocker, Task append)
    {
        var exception = await Should.ThrowAsync<Microsoft.Data.Sqlite.SqliteException>(async () => await append.WaitAsync(TimeSpan.FromSeconds(15)));
        exception.SqliteErrorCode.ShouldBe(5);
        return true;
    }

    protected override async Task AssertHistoryLockRefusedAsync(string aggregateId, string aggregateType)
    {
        await using var connection = new Microsoft.Data.Sqlite.SqliteConnection(_fixture.ConnectionString) { DefaultTimeout = 1 };
        await connection.OpenAsync();
        var exception = Should.Throw<Microsoft.Data.Sqlite.SqliteException>(() =>
        {
            using var unexpected = connection.BeginTransaction(deferred: false);
        });
        exception.SqliteErrorCode.ShouldBe(5);
    }

    protected override async Task<System.Data.Common.DbConnection> OpenSourceConnectionAsync()
    {
        var connection = new Microsoft.Data.Sqlite.SqliteConnection(_fixture.ConnectionString);
        await connection.OpenAsync();
        return connection;
    }

    protected override System.Data.Common.DbCommand CreateHistoryErasureCommand(System.Data.Common.DbConnection connection,
        System.Data.Common.DbTransaction transaction, string aggregateId, string aggregateType)
    {
        var command = connection.CreateCommand();
        command.Transaction = transaction;
#pragma warning disable CA2100 // Test fixture source names; values remain parameters.
        command.CommandText = $"UPDATE [Events] SET EventType = @Marker WHERE AggregateId = @Id AND AggregateType = @Type AND TenantId = @Tenant";
#pragma warning restore CA2100
        foreach (var (name, value) in new[] { ("Marker", ErasedEventMarker.EventType), ("Id", aggregateId),
            ("Type", aggregateType), ("Tenant", SingleTenantTestContext.Instance.TenantId!) })
        {
            var parameter = command.CreateParameter();
            parameter.ParameterName = "@" + name;
            parameter.Value = value;
            command.Parameters.Add(parameter);
        }
        return command;
    }

    protected override Task<IEventStore> CreateLostAcknowledgementStoreAsync()
    {
        IEventStore store = new SqliteEventStore(_fixture.ConnectionString, NullLogger<SqliteEventStore>.Instance, SingleTenantTestContext.Instance, Microsoft.Extensions.Options.Options.Create(new Excalibur.Dispatch.TenantContextOptions()))
        {
            CommitTransactionAsync = async (transaction, token) =>
            {
                await transaction.CommitAsync(token);
                throw new IOException("Injected loss after durable commit");
            },
        };
        return Task.FromResult(store);
    }

    protected override Task<IEventStore> CreateEventStoreAsync()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddExcalibur(x => x.AddEventSourcing(es => es.UseSqlite(options => options.ConnectionString = new Microsoft.Data.Sqlite.SqliteConnectionStringBuilder(_fixture.ConnectionString) { DefaultTimeout = 1 }.ConnectionString)));
        return Task.FromResult(Resolve<IEventStore>(services));
    }

    protected override async Task<IProjectionStore<ConformanceProjection>> CreateProjectionStoreAsync()
    {
        var table = "transactional_" + Guid.NewGuid().ToString("N");
        await using var connection = new Microsoft.Data.Sqlite.SqliteConnection(_fixture.ConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
#pragma warning disable CA2100 // Provider validates the generated identifier.
        command.CommandText = SqliteProjectionSchema.CreateTransactionalTableScript(table);
#pragma warning restore CA2100
        await command.ExecuteNonQueryAsync();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSqliteProjectionStore<ConformanceProjection>(_fixture.ConnectionString, table);
        return Resolve<IProjectionStore<ConformanceProjection>>(services);
    }

    protected override async Task<(IProjectionStore<ConformanceProjection> First,
        IProjectionStore<ConformanceProjectionNextGeneration> Second)> CreateRegisteredProjectionPairAsync()
    {
        var first = "pair_first_" + Guid.NewGuid().ToString("N");
        var second = "pair_second_" + Guid.NewGuid().ToString("N");
        await using var connection = new Microsoft.Data.Sqlite.SqliteConnection(_fixture.ConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
#pragma warning disable CA2100 // Validated provider-generated identifiers.
        command.CommandText = SqliteProjectionSchema.CreateTransactionalTableScript(first)
            + SqliteProjectionSchema.CreateTransactionalTableScript(second + "_v2");
#pragma warning restore CA2100
        await command.ExecuteNonQueryAsync();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSqliteProjectionStore<ConformanceProjection>(_fixture.ConnectionString, first);
        services.AddSqliteProjectionStore<ConformanceProjectionNextGeneration>(_fixture.ConnectionString, second);
        var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        _providers.Add(provider);
        var scope = provider.CreateAsyncScope();
        _scopes.Add(scope);
        return (scope.ServiceProvider.GetRequiredService<IProjectionStore<ConformanceProjection>>(),
            scope.ServiceProvider.GetRequiredService<IProjectionStore<ConformanceProjectionNextGeneration>>());
    }

    [Fact]
    public override Task Isolate_two_registered_models_in_one_host() => base.Isolate_two_registered_models_in_one_host();

    private T Resolve<T>(ServiceCollection services) where T : notnull
    {
        var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        _providers.Add(provider);
        var scope = provider.CreateAsyncScope();
        _scopes.Add(scope);
        return scope.ServiceProvider.GetRequiredService<T>();
    }

    [Fact]
    public async Task FilterDecimalValuesInQueriesAndCounts()
    {
        var store = await CreateProjectionStoreAsync();
        foreach (var total in new[] { 2, 10, 100 })
        {
            var id = total.ToString(System.Globalization.CultureInfo.InvariantCulture);
            await store.UpsertAsync(id, new ConformanceProjection { Id = id, Total = total }, CancellationToken.None);
        }
        foreach (var (filter, value, expected) in new (string, object, int)[]
        {
            ("Total", 10m, 1), ("Total:gte", 10m, 2), ("Total:neq", 10m, 2),
            ("Total:lt", 2.5m, 1), ("Total:in", new[] { 2m, 100m }, 2),
            ("Id", "10", 1), ("Id:in", new[] { "2", "100" }, 2),
        })
        {
            var filters = new Dictionary<string, object> { [filter] = value };
            (await store.CountAsync(filters, CancellationToken.None)).ShouldBe(expected);
            (await store.QueryAsync(filters, null, CancellationToken.None)).Count.ShouldBe(expected);
        }
    }

    [Fact]
    public async Task OrderNumericModelsBeforePaging()
    {
        var store = await CreateProjectionStoreAsync();
        foreach (var total in new[] { 2, 10, 100 })
        {
            var id = total.ToString(System.Globalization.CultureInfo.InvariantCulture);
            await store.UpsertAsync(id, new ConformanceProjection { Id = id, Total = total }, CancellationToken.None);
        }
        var rows = await store.QueryAsync(null, new QueryOptions { OrderBy = "Total", Skip = 1, Take = 1 }, CancellationToken.None);
        rows.Single().Total.ShouldBe(10);
        var descending = await store.QueryAsync(null, new QueryOptions { OrderBy = "Total", Descending = true }, CancellationToken.None);
        descending.Count.ShouldBe(3);
        descending[0].Total.ShouldBe(100);
        descending[1].Total.ShouldBe(10);
        descending[2].Total.ShouldBe(2);
    }

    [Fact]
    public override Task Commit_events_and_complete_history_model() => base.Commit_events_and_complete_history_model();

    [Fact]
    public override Task Roll_back_every_destination_when_handler_fails() => base.Roll_back_every_destination_when_handler_fails();

    [Fact]
    public override Task Reject_stale_version_without_running_handler() => base.Reject_stale_version_without_running_handler();

    [Fact]
    public override Task Refuse_to_attest_projection_for_prior_plain_append() => base.Refuse_to_attest_projection_for_prior_plain_append();

    [Fact]
    public override Task Preserve_retirement_and_roll_back_append() => base.Preserve_retirement_and_roll_back_append();

    [Fact]
    public override Task ConformanceSuite_ShouldWireEveryArm() => base.ConformanceSuite_ShouldWireEveryArm();

    [Fact]
    public override Task Report_unknown_after_lost_commit_acknowledgement() => base.Report_unknown_after_lost_commit_acknowledgement();

    [Fact]
    public override Task Refuse_model_replaced_by_ordinary_upsert() => base.Refuse_model_replaced_by_ordinary_upsert();

    [Fact]
    public override Task Preserve_source_type_and_tenant_ownership() => base.Preserve_source_type_and_tenant_ownership();

    [Fact]
    public override Task Commit_only_the_winning_concurrent_model() => base.Commit_only_the_winning_concurrent_model();

    [Fact]
    public override Task Query_and_count_visible_models_and_preserve_deletion() => base.Query_and_count_visible_models_and_preserve_deletion();

    [Fact]
    public override Task Roll_back_when_cancelled_before_commit() => base.Roll_back_when_cancelled_before_commit();

    [Fact]
    public override Task Observe_erasure_that_commits_while_waiting_for_history() => base.Observe_erasure_that_commits_while_waiting_for_history();

    [Fact]
    public override Task Hold_history_against_erasure_until_commit() => base.Hold_history_against_erasure_until_commit();
}
