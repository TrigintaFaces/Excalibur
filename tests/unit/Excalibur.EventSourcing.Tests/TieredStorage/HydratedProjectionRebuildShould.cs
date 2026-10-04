// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.EventSourcing.Projections;
using Excalibur.EventSourcing.Queries;
using Excalibur.EventSourcing.Tests.Projections;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Excalibur.EventSourcing.Tests.TieredStorage;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
[Trait("Pattern", "EventSourcing")]
public sealed class HydratedProjectionRebuildShould
{
    private static readonly long[] FailedCursors = [0];
    private static readonly long[] CompletedCursors = [0, 5];
    private static readonly string[] FailedColdReads = ["order-1", "order-3"];
    private static readonly string[] CompletedColdReads = ["order-1", "order-3", "order-4"];
    private static readonly string[] ExpectedDecoded = ["event-1", "event-2", "event-3", "event-5"];
    private static readonly string[] ExpectedObserved = ["event-1", "event-3", "event-4"];
    private static readonly string[] FailedObserved = ["event-1", "event-3"];

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PersistOnlyACompleteHydratedRebuild(bool corruptLaterArchive)
    {
        var fixture = new HydratedConsumerFixture(corruptLaterArchive);
        var projection = new MultiStreamProjection<Amounts>();
        projection.AddHandler<HydratedConsumerFixture.AmountAdded>((state, message) => state.Total += message.Amount);
        var store = new InMemoryProjectionStore<Amounts>();
        var services = new ServiceCollection();
        services.AddSingleton<IGlobalStreamQuery>(fixture.Query);
        services.AddSingleton(projection);
        services.AddSingleton<IProjectionStore<Amounts>>(store);
        using var provider = services.BuildServiceProvider();
        var rebuild = new ProjectionRebuildService(provider, fixture.Serializer,
            Options.Create(new ProjectionRebuildOptions { BatchSize = 10, BatchDelay = TimeSpan.Zero }),
            NullLogger<ProjectionRebuildService>.Instance);

        if (corruptLaterArchive)
        {
            (await Should.ThrowAsync<InvalidOperationException>(() => rebuild.RebuildAsync<Amounts>(CancellationToken.None)))
                .Message.ShouldBe("The cold event does not match the authoritative archive identity.");
            (await rebuild.GetStatusAsync<Amounts>(CancellationToken.None)).State.ShouldBe(ProjectionRebuildState.Failed);
            fixture.Decoded.ShouldBeEmpty("a later invalid archive prevents delivery of the entire page");
            fixture.RequestedPositions.ShouldBe(FailedCursors);
            fixture.ColdReads.ShouldBe(FailedColdReads);
            fixture.Observed.ShouldBe(FailedObserved);
            (await store.CountAsync(null, CancellationToken.None)).ShouldBe(0);
            for (var i = 1; i <= 5; i++)
            {
                (await store.GetByIdAsync($"order-{i}", CancellationToken.None)).ShouldBeNull();
            }
        }
        else
        {
            await rebuild.RebuildAsync<Amounts>(CancellationToken.None);
            (await rebuild.GetStatusAsync<Amounts>(CancellationToken.None)).State.ShouldBe(ProjectionRebuildState.Completed);
            fixture.Decoded.ShouldBe(ExpectedDecoded);
            fixture.RequestedPositions.ShouldBe(CompletedCursors);
            fixture.ColdReads.ShouldBe(CompletedColdReads);
            fixture.Observed.ShouldBe(ExpectedObserved);
            (await store.CountAsync(null, CancellationToken.None)).ShouldBe(4);
            (await store.GetByIdAsync("order-1", CancellationToken.None)).ShouldNotBeNull().Total.ShouldBe(17);
            (await store.GetByIdAsync("order-2", CancellationToken.None)).ShouldNotBeNull().Total.ShouldBe(5);
            (await store.GetByIdAsync("order-3", CancellationToken.None)).ShouldNotBeNull().Total.ShouldBe(11);
            (await store.GetByIdAsync("order-4", CancellationToken.None)).ShouldBeNull("fresh erasure must suppress the old cold payload");
            (await store.GetByIdAsync("order-5", CancellationToken.None)).ShouldNotBeNull().Total.ShouldBe(13);
        }
    }

    internal sealed class Amounts
    {
        public int Total { get; set; }
    }
}
