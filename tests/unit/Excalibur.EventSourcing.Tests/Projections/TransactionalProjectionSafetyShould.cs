// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch;
using Excalibur.EventSourcing.DependencyInjection;
using Excalibur.EventSourcing.Projections;
using Excalibur.EventSourcing.Queries;
using Excalibur.EventSourcing.Sqlite;
using Excalibur.EventSourcing.Subscriptions;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Excalibur.EventSourcing.Tests.Projections;

[Trait("Category", "Unit")]
[Trait("Component", "EventSourcing")]
[Trait("Pattern", "TransactionalInline")]
public sealed class TransactionalProjectionSafetyShould
{
    [Fact]
    public async Task PreserveTheCommittedModelAndAllowTheNextSaveAfterRejectedRecovery()
    {
        var connectionString = $"Data Source=projection-safety-{Guid.NewGuid():N};Mode=Memory;Cache=Shared;Pooling=False";
        await using var keeper = new SqliteConnection(connectionString);
        await keeper.OpenAsync();
        await using (var command = keeper.CreateCommand())
        {
#pragma warning disable CA2100 // Schema generator validates identifiers; this test supplies a constant table name.
            command.CommandText = SqliteProjectionSchema.CreateTransactionalTableScript("Counts");
#pragma warning restore CA2100
            await command.ExecuteNonQueryAsync();
        }

        var tenant = TestTenantContext.SingleTenantDefault;
        var eventStore = new SqliteEventStore(connectionString, NullLogger<SqliteEventStore>.Instance,
            tenant, Options.Create(new TenantContextOptions()), eventTypeInfoResolver: GeneratedProjectionJson.Default);
        var destination = new SqliteProjectionStore<GeneratedReadModel>(connectionString, tenant, "Counts",
            destinationKind: ProjectionDestinationKind.Transactional);
        var registry = new InMemoryProjectionRegistry();
        var builder = new ProjectionBuilder<GeneratedReadModel>(registry);
        builder.TransactionalInline("Counter").When<GeneratedAdded>((model, e) => model.Count += e.Amount);
        builder.BuildGenerated(registry, GeneratedProjectionJson.Default.GeneratedReadModel);
        var serializer = new AotJsonEventSerializer(GeneratedProjectionJson.Default, typeof(GeneratedAdded));
        await using var services = new ServiceCollection()
            .AddSingleton<IProjectionRegistry>(registry)
            .AddSingleton<IEventSerializer>(serializer)
            .AddSingleton<IProjectionStore<GeneratedReadModel>>(destination).BuildServiceProvider();
        var coordinator = new TransactionalProjectionCoordinator(registry, services.GetRequiredService<IServiceScopeFactory>());
        var recovery = new ProjectionRecoveryService(registry, eventStore, serializer, services, NullLogger<ProjectionRecoveryService>.Instance);

        var first = await AppendAsync(3, -1);
        first.Success.ShouldBeTrue(first.ErrorMessage);
        await Should.ThrowAsync<NotSupportedException>(() => recovery.ReapplyAsync<GeneratedReadModel>("row", "Counter", CancellationToken.None));
        (await destination.GetByIdAsync("row", CancellationToken.None)).ShouldNotBeNull().Count.ShouldBe(3);

        var second = await AppendAsync(4, first.NextExpectedVersion!.Value);
        second.Success.ShouldBeTrue(second.ErrorMessage);
        (await destination.GetByIdAsync("row", CancellationToken.None)).ShouldNotBeNull().Count.ShouldBe(7);
        (await eventStore.LoadAsync("row", "Counter", CancellationToken.None)).Count.ShouldBe(2);

        ValueTask<AppendResult> AppendAsync(int amount, long expectedVersion) => coordinator.AppendAsync(eventStore,
            "row", "Counter", [new GeneratedAdded(amount)], expectedVersion,
            stored => serializer.DeserializeEvent(stored.EventData!, serializer.ResolveType(stored.EventType)),
            null, CancellationToken.None);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RefuseRecoveryBeforeResolvingADestinationOrReadingHistory(bool generated)
    {
        var services = new ServiceCollection();
        var builder = A.Fake<IEventSourcingBuilder>();
        A.CallTo(() => builder.Services).Returns(services);
        builder.AddProjection<GeneratedReadModel>(p => p.TransactionalInline("Counter"));
        services.AddScoped<IProjectionStore<GeneratedReadModel>>(_ => throw new InvalidOperationException("Destination was resolved"));
        await using var provider = services.BuildServiceProvider();
        var eventStore = A.Fake<IEventStore>();
        var recovery = new ProjectionRecoveryService(provider.GetRequiredService<IProjectionRegistry>(), eventStore,
            A.Fake<IEventSerializer>(), provider, NullLogger<ProjectionRecoveryService>.Instance);

        if (generated)
        {
            await Should.ThrowAsync<NotSupportedException>(() => recovery.ReapplyGeneratedAsync<GeneratedReadModel>(
                "row", "Counter", null!, provider, CancellationToken.None));
        }
        else
        {
            await Should.ThrowAsync<NotSupportedException>(() => recovery.ReapplyAsync<GeneratedReadModel>("row", "Counter", CancellationToken.None));
        }
        A.CallTo(() => eventStore.LoadAsync(A<string>._, A<string>._, A<CancellationToken>._)).MustNotHaveHappened();
    }

    [Fact]
    public async Task RefuseRebuildBeforeResolvingItsQueryDestinationOrCheckpoints()
    {
        var services = new ServiceCollection();
        var builder = A.Fake<IEventSourcingBuilder>();
        A.CallTo(() => builder.Services).Returns(services);
        builder.AddProjection<GeneratedReadModel>(p => p.TransactionalInline("Counter"));
        services.AddScoped<IProjectionStore<GeneratedReadModel>>(_ => throw new InvalidOperationException("Destination was resolved"));
        services.AddSingleton<IGlobalStreamQuery>(_ => throw new InvalidOperationException("Query was resolved"));
        services.AddSingleton<ISubscriptionCheckpointStore>(_ => throw new InvalidOperationException("Checkpoints were resolved"));
        await using var provider = services.BuildServiceProvider();
        using var rebuild = new ProjectionRebuildService(provider, A.Fake<IEventSerializer>(),
            Options.Create(new ProjectionRebuildOptions { OfflineRebuildAcknowledged = true }), NullLogger<ProjectionRebuildService>.Instance);
        await Should.ThrowAsync<NotSupportedException>(() => rebuild.RebuildAsync<GeneratedReadModel>(CancellationToken.None));
    }
}
