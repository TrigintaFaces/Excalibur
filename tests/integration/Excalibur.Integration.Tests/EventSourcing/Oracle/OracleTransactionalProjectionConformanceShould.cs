// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.EventSourcing;
using Microsoft.Extensions.Logging.Abstractions;
using Tests.Shared.Fixtures;
using Excalibur.Integration.Tests.Data.EventStore;
using Excalibur.EventSourcing.Oracle;
using Excalibur.Testing.Conformance;
using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.Integration.Tests.EventSourcing.Oracle;

[Collection(OracleEventStoreTestCollection.CollectionName)]
[Trait("Category", "Integration")]
[Trait("Component", "EventStore")]
[Trait("Database", "Oracle")]
[Trait("Pattern", "TransactionalInline")]
public sealed class OracleTransactionalProjectionConformanceShould(OracleEventStoreContainerFixture fixture)
    : TransactionalProjectionConformanceTestKit, IClassFixture<OracleEventStoreContainerFixture>, IAsyncLifetime
{
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2213:Disposable fields should be disposed", Justification = "xUnit owns and disposes the class fixture.")]
    private readonly OracleEventStoreContainerFixture _fixture = fixture;
    private readonly List<ServiceProvider> _providers = [];
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
    }

    [Fact]
    public async Task RejectDifferentlyConfiguredDestinationBeforeWriting()
    {
        await using var connection = await OpenSourceConnectionAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        var other = new global::Oracle.ManagedDataAccess.Client.OracleConnectionStringBuilder(_fixture.ConnectionString) { UserID = "NOT_THE_SOURCE_USER" };
        var store = new OracleProjectionStore<ConformanceProjection>(other.ConnectionString, SingleTenantTestContext.Instance, "MissingModel");
        var exception = await Should.ThrowAsync<InvalidOperationException>(async () =>
            await store.WriteAsync(new ProjectionTransaction(transaction, [], SingleTenantTestContext.Instance.TenantId!, "source"),
                "row", "Aggregate", "{}", CancellationToken.None));
        exception.Message.ShouldContain("same configured Oracle service and database user");
    }

    protected override async Task<bool> ObserveHistoryContentionAsync(System.Data.Common.DbConnection blocker, Task append)
    {
        await using var identity = blocker.CreateCommand();
        identity.CommandText = "SELECT SYS_CONTEXT('USERENV', 'SID') FROM DUAL";
        var blockerId = Convert.ToInt32(await identity.ExecuteScalarAsync(), System.Globalization.CultureInfo.InvariantCulture);
        await using var observer = await OpenSourceConnectionAsync();
        await using var query = observer.CreateCommand();
        query.CommandText = "SELECT COUNT(*) FROM V$SESSION WHERE BLOCKING_SESSION_STATUS = 'VALID' AND BLOCKING_SESSION = :Blocker AND STATE = 'WAITING'";
        var parameter = query.CreateParameter();
        parameter.ParameterName = "Blocker";
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
        await using var command = CreateHistoryErasureCommand(connection, transaction, aggregateId, aggregateType);
#pragma warning disable CA2100 // Reuse the parameterized fixture predicate for an immediate conflicting row lock.
        command.CommandText = command.CommandText.Replace("UPDATE ", "SELECT EventId FROM ", StringComparison.Ordinal)
            .Replace(" SET EventType = :Marker", "", StringComparison.Ordinal) + " FOR UPDATE NOWAIT";
#pragma warning restore CA2100
        command.Parameters.RemoveAt(0);
        var exception = await Should.ThrowAsync<global::Oracle.ManagedDataAccess.Client.OracleException>(async () =>
        {
            await using var reader = await command.ExecuteReaderAsync();
        });
        exception.Number.ShouldBe(54);
    }

    protected override async Task<System.Data.Common.DbConnection> OpenSourceConnectionAsync()
    {
        var connection = new global::Oracle.ManagedDataAccess.Client.OracleConnection(_fixture.ConnectionString);
        await connection.OpenAsync();
        return connection;
    }

    protected override System.Data.Common.DbCommand CreateHistoryErasureCommand(System.Data.Common.DbConnection connection,
        System.Data.Common.DbTransaction transaction, string aggregateId, string aggregateType)
    {
        var command = connection.CreateCommand();
        command.Transaction = transaction;
        ((global::Oracle.ManagedDataAccess.Client.OracleCommand)command).BindByName = true;
#pragma warning disable CA2100 // Test fixture source names; values remain parameters.
        command.CommandText = $"UPDATE \"{_fixture.Schema}\".\"{_fixture.TableName}\" SET EventType = :Marker WHERE AggregateId = :Id AND AggregateType = :Type AND TenantId = :Tenant";
#pragma warning restore CA2100
        foreach (var (name, value) in new[] { ("Marker", ErasedEventMarker.EventType), ("Id", aggregateId),
            ("Type", aggregateType), ("Tenant", SingleTenantTestContext.Instance.TenantId!) })
        {
            var parameter = command.CreateParameter();
            parameter.ParameterName = ":" + name;
            parameter.Value = value;
            command.Parameters.Add(parameter);
        }
        return command;
    }

    protected override Task<IEventStore> CreateLostAcknowledgementStoreAsync()
    {
        IEventStore store = new OracleEventStore(() => new global::Oracle.ManagedDataAccess.Client.OracleConnection(_fixture.ConnectionString), NullLogger<OracleEventStore>.Instance, SingleTenantTestContext.Instance, schema: _fixture.Schema, table: _fixture.TableName)
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
        services.AddExcalibur(x => x.AddEventSourcing(es => es.UseOracle(options => { options.ConnectionString = _fixture.ConnectionString; options.Schema = _fixture.Schema; options.Table = _fixture.TableName; })));
        return Task.FromResult(Resolve<IEventStore>(services));
    }

    protected override async Task<IProjectionStore<ConformanceProjection>> CreateProjectionStoreAsync()
    {
        var table = "transactional_" + Guid.NewGuid().ToString("N");
        await using var connection = new global::Oracle.ManagedDataAccess.Client.OracleConnection(_fixture.ConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
#pragma warning disable CA2100 // Provider validates the generated identifier.
        command.CommandText = OracleProjectionSchema.CreateTransactionalTableScript(table, _fixture.Schema);
#pragma warning restore CA2100
        await command.ExecuteNonQueryAsync();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddOracleProjectionStore<ConformanceProjection>(_fixture.ConnectionString, table, schemaName: _fixture.Schema);
        return Resolve<IProjectionStore<ConformanceProjection>>(services);
    }

    protected override async Task<(IProjectionStore<ConformanceProjection> First,
        IProjectionStore<ConformanceProjectionNextGeneration> Second)> CreateRegisteredProjectionPairAsync()
    {
        var first = "pair_first_" + Guid.NewGuid().ToString("N");
        var second = "pair_second_" + Guid.NewGuid().ToString("N");
        await using var connection = new global::Oracle.ManagedDataAccess.Client.OracleConnection(_fixture.ConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
#pragma warning disable CA2100 // Validated provider-generated identifiers.
        command.CommandText = OracleProjectionSchema.CreateTransactionalTableScript(first, _fixture.Schema);
        await command.ExecuteNonQueryAsync();
        command.CommandText = OracleProjectionSchema.CreateTransactionalTableScript(second + "_v2", _fixture.Schema);
#pragma warning restore CA2100
        await command.ExecuteNonQueryAsync();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddOracleProjectionStore<ConformanceProjection>(_fixture.ConnectionString, first, schemaName: _fixture.Schema);
        services.AddOracleProjectionStore<ConformanceProjectionNextGeneration>(_fixture.ConnectionString, second, schemaName: _fixture.Schema);
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

    public enum SortPriority { Zebra, Alpha }

    private sealed class NamedEnumModel
    {
        public NamedEnumModel() { }

        [System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter<SortPriority>))]
        public SortPriority Priority { get; set; }
        public bool IsActive { get; set; }
    }

    [Fact]
    public async Task FilterBooleanValuesInQueriesAndCounts()
    {
        var table = "bool_" + Guid.NewGuid().ToString("N");
        await using var connection = await OpenSourceConnectionAsync();
        await using var command = connection.CreateCommand();
#pragma warning disable CA2100 // Provider validates the isolated test table and schema.
        command.CommandText = OracleProjectionSchema.CreateTransactionalTableScript(table, _fixture.Schema);
#pragma warning restore CA2100
        await command.ExecuteNonQueryAsync();
        var store = new OracleProjectionStore<NamedEnumModel>(_fixture.ConnectionString, SingleTenantTestContext.Instance, table, schemaName: _fixture.Schema);
        await store.UpsertAsync("a", new NamedEnumModel { IsActive = true }, CancellationToken.None);
        await store.UpsertAsync("z", new NamedEnumModel { IsActive = false }, CancellationToken.None);
        foreach (var (filter, value, expected) in new (string, object, int)[]
        {
            ("IsActive", true, 1), ("IsActive", false, 1), ("IsActive:neq", true, 1),
            ("IsActive:in", new[] { true, false }, 2),
        })
        {
            var filters = new Dictionary<string, object> { [filter] = value };
            (await store.CountAsync(filters, CancellationToken.None)).ShouldBe(expected);
            (await store.QueryAsync(filters, null, CancellationToken.None)).Count.ShouldBe(expected);
        }
    }

    [Fact]
    public async Task PreserveStringEnumOrdering()
    {
        var table = "enum_" + Guid.NewGuid().ToString("N");
        await using var connection = await OpenSourceConnectionAsync();
        await using var command = connection.CreateCommand();
#pragma warning disable CA2100 // Provider validates the isolated test table and schema.
        command.CommandText = OracleProjectionSchema.CreateTransactionalTableScript(table, _fixture.Schema);
#pragma warning restore CA2100
        await command.ExecuteNonQueryAsync();
        var store = new OracleProjectionStore<NamedEnumModel>(_fixture.ConnectionString, SingleTenantTestContext.Instance, table, schemaName: _fixture.Schema);
        await store.UpsertAsync("a", new NamedEnumModel { Priority = SortPriority.Zebra }, CancellationToken.None);
        await store.UpsertAsync("z", new NamedEnumModel { Priority = SortPriority.Alpha }, CancellationToken.None);
        var rows = await store.QueryAsync(null, new QueryOptions { OrderBy = nameof(NamedEnumModel.Priority) }, CancellationToken.None);
        rows.Count.ShouldBe(2);
        rows[0].Priority.ShouldBe(SortPriority.Alpha);
        rows[1].Priority.ShouldBe(SortPriority.Zebra);
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
