// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.EventSourcing;
using Microsoft.Extensions.Logging.Abstractions;
using Tests.Shared.Fixtures;
using Excalibur.Integration.Tests.Data.EventStore;
using Excalibur.EventSourcing.Postgres;
using Excalibur.Testing.Conformance;
using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.Integration.Tests.EventSourcing.Postgres;

[Collection(PostgresEventStoreTestCollection.CollectionName)]
[Trait("Category", "Integration")]
[Trait("Component", "EventStore")]
[Trait("Database", "Postgres")]
[Trait("Pattern", "TransactionalInline")]
public sealed class PostgresTransactionalProjectionConformanceShould(PostgresEventStoreContainerFixture fixture)
    : TransactionalProjectionConformanceTestKit, IClassFixture<PostgresEventStoreContainerFixture>, IAsyncLifetime
{
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2213:Disposable fields should be disposed", Justification = "xUnit owns and disposes the class fixture.")]
    private readonly PostgresEventStoreContainerFixture _fixture = fixture;
    private readonly List<ServiceProvider> _providers = [];
    private readonly List<Npgsql.NpgsqlDataSource> _dataSources = [];
    private readonly List<AsyncServiceScope> _scopes = [];

    public async ValueTask InitializeAsync()
    {
        _fixture.DockerAvailable.ShouldBeTrue("Transactional conformance requires a real database; no arms may skip.");
        await _fixture.EnsureInitializedAsync();
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var scope in _scopes) { await scope.DisposeAsync(); }
        foreach (var provider in _providers) { await provider.DisposeAsync(); }
        foreach (var dataSource in _dataSources) { await dataSource.DisposeAsync(); }
    }

    [Fact]
    public async Task RejectDifferentlyConfiguredDestinationBeforeWriting()
    {
        await using var connection = await OpenSourceConnectionAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        var other = new Npgsql.NpgsqlConnectionStringBuilder(_fixture.ConnectionString) { Database = "not_the_source_database" };
        await using var dataSource = Npgsql.NpgsqlDataSource.Create(other.ConnectionString);
        var store = new PostgresProjectionStore<ConformanceProjection>(dataSource, NullLogger<PostgresProjectionStore<ConformanceProjection>>.Instance,
            SingleTenantTestContext.Instance, "MissingModel");
        var exception = await Should.ThrowAsync<InvalidOperationException>(async () =>
            await store.WriteAsync(new ProjectionTransaction(transaction, [], SingleTenantTestContext.Instance.TenantId!, "source"),
                "row", "Aggregate", "{}", CancellationToken.None));
        exception.Message.ShouldContain("same configured PostgreSQL database");
    }

    protected override async Task<bool> ObserveHistoryContentionAsync(System.Data.Common.DbConnection blocker, Task append)
    {
        await using var identity = blocker.CreateCommand();
        identity.CommandText = "SELECT pg_backend_pid()";
        var blockerId = Convert.ToInt32(await identity.ExecuteScalarAsync(), System.Globalization.CultureInfo.InvariantCulture);
        await using var observer = await OpenSourceConnectionAsync();
        await using var query = observer.CreateCommand();
        query.CommandText = "SELECT COUNT(*) FROM pg_stat_activity WHERE wait_event_type = 'Lock' AND @Blocker = ANY(pg_blocking_pids(pid))";
        var parameter = query.CreateParameter();
        parameter.ParameterName = "@Blocker";
        parameter.Value = blockerId;
        query.Parameters.Add(parameter);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        while (!timeout.IsCancellationRequested)
        {
            append.IsCompleted.ShouldBeFalse("The contender must still be waiting for the owned history.");
            if (Convert.ToInt32(await query.ExecuteScalarAsync(timeout.Token), System.Globalization.CultureInfo.InvariantCulture) > 0)
            { return false; }
            await Task.Delay(TimeSpan.FromMilliseconds(20), TimeProvider.System, timeout.Token);
        }
        throw new InvalidOperationException("No database lock waiter was observed before timeout.");
    }

    protected override async Task AssertHistoryLockRefusedAsync(string aggregateId, string aggregateType)
    {
        await using var connection = await OpenSourceConnectionAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        await using var setup = connection.CreateCommand();
        setup.Transaction = transaction;
        setup.CommandText = "SET LOCAL lock_timeout = '250ms'";
        await setup.ExecuteNonQueryAsync();
        await using var command = CreateHistoryErasureCommand(connection, transaction, aggregateId, aggregateType);
        var exception = await Should.ThrowAsync<Npgsql.PostgresException>(async () => await command.ExecuteNonQueryAsync());
        exception.SqlState.ShouldBe("55P03");
    }

    protected override async Task<System.Data.Common.DbConnection> OpenSourceConnectionAsync()
    {
        var connection = new Npgsql.NpgsqlConnection(_fixture.ConnectionString);
        await connection.OpenAsync();
        return connection;
    }

    protected override System.Data.Common.DbCommand CreateHistoryErasureCommand(System.Data.Common.DbConnection connection,
        System.Data.Common.DbTransaction transaction, string aggregateId, string aggregateType)
    {
        var command = connection.CreateCommand();
        command.Transaction = transaction;
#pragma warning disable CA2100 // Test fixture source names; values remain parameters.
        command.CommandText = $"UPDATE public.events SET event_type = @Marker WHERE aggregate_id = @Id AND aggregate_type = @Type AND tenant_id = @Tenant";
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
        var dataSource = Npgsql.NpgsqlDataSource.Create(_fixture.ConnectionString);
        _dataSources.Add(dataSource);
        IEventStore store = new PostgresEventStore(dataSource, NullLogger<PostgresEventStore>.Instance, SingleTenantTestContext.Instance, table: _fixture.TableName)
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
        services.AddExcalibur(x => x.AddEventSourcing(es => es.UsePostgres(options => options
            .ConnectionString(_fixture.ConnectionString).EventStoreSchema("public").EventStoreTable(_fixture.TableName))));
        return Task.FromResult(Resolve<IEventStore>(services));
    }

    protected override async Task<IProjectionStore<ConformanceProjection>> CreateProjectionStoreAsync()
    {
        var table = "transactional_" + Guid.NewGuid().ToString("N");
        await using var connection = new Npgsql.NpgsqlConnection(_fixture.ConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
#pragma warning disable CA2100 // Provider validates the generated identifier.
        command.CommandText = PostgresProjectionSchema.CreateTransactionalTableScript(table, "public");
#pragma warning restore CA2100
        await command.ExecuteNonQueryAsync();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddPostgresProjectionStore<ConformanceProjection>(options =>
        {
            options.ConnectionString = _fixture.ConnectionString;
            options.TableName = table;
            options.SchemaName = "public";
        });
        return Resolve<IProjectionStore<ConformanceProjection>>(services);
    }

    protected override async Task<(IProjectionStore<ConformanceProjection> First,
        IProjectionStore<ConformanceProjectionNextGeneration> Second)> CreateRegisteredProjectionPairAsync()
    {
        var first = "pair_first_" + Guid.NewGuid().ToString("N");
        var second = "pair_second_" + Guid.NewGuid().ToString("N");
        await using var connection = new Npgsql.NpgsqlConnection(_fixture.ConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
#pragma warning disable CA2100 // Validated provider-generated identifiers.
        command.CommandText = PostgresProjectionSchema.CreateTransactionalTableScript(first, "public")
            + PostgresProjectionSchema.CreateTransactionalTableScript(second + "_v2", "public");
#pragma warning restore CA2100
        await command.ExecuteNonQueryAsync();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddPostgresProjections(shared =>
        {
            shared.ConnectionString = _fixture.ConnectionString;
            shared.SchemaName = "public";
        }, projections =>
        {
            projections.Add<ConformanceProjection>(options => options.TableName = first);
            projections.Add<ConformanceProjectionNextGeneration>(options => options.TableName = second);
        });
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
