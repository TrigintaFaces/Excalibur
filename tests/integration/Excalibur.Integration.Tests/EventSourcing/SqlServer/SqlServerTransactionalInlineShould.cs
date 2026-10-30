// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.EventSourcing;
using Excalibur.EventSourcing.SqlServer;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging.Abstractions;
using Tests.Shared.Fixtures;

namespace Excalibur.Integration.Tests.EventSourcing.SqlServer;

public sealed partial class SqlServerTransactionalAppendAtomicityShould
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    [Trait("Pattern", "TransactionalInline")]
    public async Task RejectDifferentDatabaseOrTenantBeforeAppending(bool differentDatabase)
    {
        _requiredContainer.Require();
        var connection = new SqlConnectionStringBuilder(_connectionString);
        if (differentDatabase) { connection.InitialCatalog = "AnotherDatabase"; }
        var tenant = FakeItEasy.A.Fake<Excalibur.Dispatch.ITenantContext>();
        FakeItEasy.A.CallTo(() => tenant.TenantId).Returns(differentDatabase ? SingleTenantTestContext.Instance.TenantId : "another-tenant");
        var projection = new SqlServerProjectionStore<TransactionalCount>(connection.ConnectionString,
            NullLogger<SqlServerProjectionStore<TransactionalCount>>.Instance, tenant);
        var id = Guid.NewGuid().ToString("N");
        var store = (SqlServerEventStore)CreateEventStore();
        await Should.ThrowAsync<InvalidOperationException>(async () => await store.AppendWithProjectionsAsync(
            id, "TestAggregate", [new TestDomainEvent(id, 0)], -1,
            (transaction, token) => projection.WriteAsync(transaction, id, "TestAggregate", "{}", token), CancellationToken.None));
        (await CountEventsAsync(id)).ShouldBe(0);
    }

    [Fact]
    [Trait("Pattern", "TransactionalInline")]
    public async Task CommitHistoryProjectionAndEventsOnOneTransaction()
    {
        var events = (SqlServerEventStore)CreateEventStore();
        var id = Guid.NewGuid().ToString("N");
        (await events.AppendAsync(id, "TestAggregate", [new TestDomainEvent(id, 0)], -1, CancellationToken.None)).Success.ShouldBeTrue();
        var projection = await CreateTransactionalProjectionAsync();
        var result = await events.AppendWithProjectionsAsync(id, "TestAggregate", [new TestDomainEvent(id, 1)], 0,
            async (transaction, token) =>
            {
                transaction.History.Count.ShouldBe(1);
                transaction.History[0].Version.ShouldBe(0);
                await projection.WriteAsync(transaction, id, "TestAggregate", projection.Serialize(new TransactionalCount { Count = 2 }), token);
                await InsertOutboxRowAsync(transaction.Transaction, id, token);
            }, CancellationToken.None);

        result.Success.ShouldBeTrue();
        (await CountEventsAsync(id)).ShouldBe(2);
        (await CountOutboxAsync(id)).ShouldBe(1);
        (await projection.GetByIdAsync(id, CancellationToken.None)).ShouldNotBeNull().Count.ShouldBe(2);
    }

    [Fact]
    [Trait("Pattern", "TransactionalInline")]
    public async Task RollBackProjectionAndOutboxWhenTransactionalHandlerFails()
    {
        var events = (SqlServerEventStore)CreateEventStore();
        var id = Guid.NewGuid().ToString("N");
        var projection = await CreateTransactionalProjectionAsync();
        await Should.ThrowAsync<InvalidOperationException>(async () =>
            await events.AppendWithProjectionsAsync(id, "TestAggregate", [new TestDomainEvent(id, 0)], -1,
                async (transaction, token) =>
                {
                    await projection.WriteAsync(transaction, id, "TestAggregate", projection.Serialize(new TransactionalCount { Count = 1 }), token);
                    await InsertOutboxRowAsync(transaction.Transaction, id, token);
                    throw new InvalidOperationException("handler failed after enlisted writes");
                }, CancellationToken.None));
        (await CountEventsAsync(id)).ShouldBe(0);
        (await CountOutboxAsync(id)).ShouldBe(0);
        (await projection.GetByIdAsync(id, CancellationToken.None)).ShouldBeNull();
    }

    [Fact]
    [Trait("Pattern", "TransactionalInline")]
    public async Task RefuseToAttestAnEarlierPlainAppend()
    {
        var events = (SqlServerEventStore)CreateEventStore();
        var id = Guid.NewGuid().ToString("N");
        var payload = new TestDomainEvent(id, 0);
        (await events.AppendAsync(id, "TestAggregate", [payload], -1, CancellationToken.None)).Success.ShouldBeTrue();
        var called = false;
        var result = await events.AppendWithProjectionsAsync(id, "TestAggregate", [payload], -1,
            (_, _) => { called = true; return ValueTask.CompletedTask; }, CancellationToken.None);
        called.ShouldBeFalse();
        result.Outcome.ShouldBe(AppendOutcome.Unknown);
        (await CountEventsAsync(id)).ShouldBe(1);
    }

    [Fact]
    [Trait("Pattern", "TransactionalInline")]
    public async Task PreserveRetirementAndRejectOtherAggregateOwners()
    {
        var events = (SqlServerEventStore)CreateEventStore();
        var id = Guid.NewGuid().ToString("N");
        var projection = await CreateTransactionalProjectionAsync();
        var initial = await events.AppendWithProjectionsAsync(id, "TestAggregate", [new TestDomainEvent(id, 0)], -1,
            (transaction, token) => projection.WriteAsync(transaction, id, "TestAggregate", "{\"count\":1}", token), CancellationToken.None);
        initial.Success.ShouldBeTrue();
        await Should.ThrowAsync<SqlException>(async () =>
            await events.AppendWithProjectionsAsync(id, "OtherAggregate", [new TestDomainEvent(id, 0)], -1,
                (transaction, token) => projection.WriteAsync(transaction, id, "OtherAggregate", "{\"count\":7}", token), CancellationToken.None));
        await projection.DeleteAsync(id, CancellationToken.None);
        await Should.ThrowAsync<SqlException>(async () =>
            await events.AppendWithProjectionsAsync(id, "TestAggregate", [new TestDomainEvent(id, 1)], 0,
                (transaction, token) => projection.WriteAsync(transaction, id, "TestAggregate", "{\"count\":2}", token), CancellationToken.None));
        (await CountEventsAsync(id)).ShouldBe(1);
        (await projection.GetByIdAsync(id, CancellationToken.None)).ShouldBeNull();
    }

    [Fact]
    [Trait("Pattern", "TransactionalInline")]
    public async Task RejectGlobalPositionedWritesToTransactionalTables()
    {
        var projection = await CreateTransactionalProjectionAsync();
        await Should.ThrowAsync<SqlException>(() => projection.UpsertAtPositionAsync("row", new TransactionalCount { Count = 9 }, null, 10, CancellationToken.None));
        (await projection.GetByIdAsync("row", CancellationToken.None)).ShouldBeNull();
    }

    [Fact]
    [Trait("Pattern", "TransactionalInline")]
    public async Task KeepOnlyTheWinningConcurrentSaveAndProjection()
    {
        var id = Guid.NewGuid().ToString("N");
        var projection = await CreateTransactionalProjectionAsync();
        async Task<AppendResult> Save(int count)
        {
            var store = (SqlServerEventStore)CreateEventStore();
            try
            {
                return await store.AppendWithProjectionsAsync(id, "TestAggregate", [new TestDomainEvent(id, count)], -1,
                    (transaction, token) => projection.WriteAsync(transaction, id, "TestAggregate", projection.Serialize(new TransactionalCount { Count = count }), token), CancellationToken.None);
            }
            catch (SqlException exception) when (exception.Number == 1205)
            {
                return AppendResult.CreateFailure("deadlock victim rolled back");
            }
        }

        var results = await Task.WhenAll(Save(10), Save(20));
        results.Count(r => r.Success).ShouldBe(1);
        var winner = results[0].Success ? 10 : 20;
        (await CountEventsAsync(id)).ShouldBe(1);
        (await projection.GetByIdAsync(id, CancellationToken.None)).ShouldNotBeNull().Count.ShouldBe(winner);
    }

    private async Task<SqlServerProjectionStore<TransactionalCount>> CreateTransactionalProjectionAsync()
    {
        _requiredContainer.Require();
        var table = "Transactional_" + Guid.NewGuid().ToString("N");
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
#pragma warning disable CA2100 // Provider validates the generated table identifier.
        command.CommandText = SqlServerProjectionSchema.CreateTransactionalTableScript(table);
#pragma warning restore CA2100
        _ = await command.ExecuteNonQueryAsync();
        return new SqlServerProjectionStore<TransactionalCount>(_connectionString!, NullLogger<SqlServerProjectionStore<TransactionalCount>>.Instance,
            SingleTenantTestContext.Instance, table);
    }

    private sealed class TransactionalCount
    {
        public int Count { get; set; }
    }
}
