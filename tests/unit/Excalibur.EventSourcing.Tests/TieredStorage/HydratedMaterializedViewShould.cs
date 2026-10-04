// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch;
using Excalibur.EventSourcing.Diagnostics;
using Excalibur.EventSourcing.DependencyInjection;
using Excalibur.EventSourcing.Queries;
using Excalibur.EventSourcing.Views;

using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.EventSourcing.Tests.TieredStorage;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
[Trait("Pattern", "EventSourcing")]
public sealed class HydratedMaterializedViewShould
{
    private static readonly long[] ExpectedWrites = [1, 2, 3, 5];

    [Theory]
    [InlineData(false, "default", false)]
    [InlineData(true, "default", false)]
    [InlineData(false, "explicit", false)]
    [InlineData(true, "explicit", false)]
    [InlineData(false, "factory", false)]
    [InlineData(true, "factory", false)]
    [InlineData(false, "default", true)]
    [InlineData(false, "explicit", true)]
    [InlineData(false, "factory", true)]
    public async Task PersistHydratedValuesOrKeepTheViewUnchanged(bool corruptLaterArchive, string registration, bool rebuild)
    {
        var fixture = new HydratedConsumerFixture(corruptLaterArchive);
        var store = A.Fake<IAtomicMaterializedViewStore>();
        A.CallTo(() => store.SupportsAtomicWrites).Returns(true);
        long? position = null;
        var views = new Dictionary<string, Amount>();
        var writes = new List<long>();
        A.CallTo(() => store.GetPositionAsync("amounts", A<CancellationToken>._))
            .ReturnsLazily(() => ValueTask.FromResult(position));
        A.CallTo(() => store.GetAsync<Amount>("amounts", A<string>._, A<CancellationToken>._))
            .ReturnsLazily(call => ValueTask.FromResult(views.TryGetValue(call.GetArgument<string>(1)!, out var view)
                ? new Amount { Value = view.Value } : null));
        A.CallTo(() => store.SaveViewAndPositionAsync("amounts", A<string>._, A<Amount>._, A<long>._, A<CancellationToken>._))
            .ReturnsLazily(call =>
            {
                var next = call.GetArgument<long>(3);
                next.ShouldBeGreaterThan(position ?? 0);
                views[call.GetArgument<string>(1)!] = new Amount { Value = call.GetArgument<Amount>(2)!.Value };
                position = next;
                writes.Add(next);
                return ValueTask.CompletedTask;
            });
        A.CallTo(() => store.SavePositionAsync("amounts", A<long>._, A<CancellationToken>._))
            .ReturnsLazily(call =>
            {
                var next = call.GetArgument<long>(1);
                if (position >= next)
                {
                    return ValueTask.FromResult(ViewPositionSaveOutcome.RefusedAsStale);
                }
                position = next;
                writes.Add(next);
                return ValueTask.FromResult(ViewPositionSaveOutcome.Advanced);
            });
        using var metrics = new MaterializedViewMetrics();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IGlobalStreamQuery>(fixture.Query);
        services.AddSingleton(fixture.Serializer);
        services.AddSingleton(metrics);
        services.AddMaterializedViews(builder =>
        {
            builder.UseStore(_ => store);
            if (registration == "default")
            {
                builder.AddBuilder<Amount, AmountBuilder>();
            }
            else if (registration == "explicit")
            {
                builder.AddBuilder<Amount, ExplicitAmountBuilder>();
            }
            else
            {
                builder.AddBuilder<Amount>(_ => new ExplicitAmountBuilder());
            }
        });
        services.Configure<MaterializedViewOptions>(options =>
        {
            options.BatchSize = 10;
            options.BatchDelay = TimeSpan.Zero;
        });
        using var provider = services.BuildServiceProvider();
        var processor = provider.GetRequiredService<IMaterializedViewProcessor>();

        if (corruptLaterArchive)
        {
            (await Should.ThrowAsync<InvalidOperationException>(() => processor.CatchUpAsync("amounts", CancellationToken.None)))
                .Message.ShouldBe("The cold event does not match the authoritative archive identity.");
            views.ShouldBeEmpty();
            position.ShouldBeNull();
            writes.ShouldBeEmpty();
            Fake.GetCalls(store).Where(call => call.Method.Name is "SaveAsync" or "SaveViewAndPositionAsync"
                or "SavePositionAsync" or "ResetPositionAsync" or "DeleteAsync").ShouldBeEmpty();
            fixture.Decoded.ShouldBeEmpty();
            fixture.ColdReads.Count.ShouldBe(2);
            fixture.Observed.Last().ShouldBe("event-3");
            metrics.GetFailureRatePercent().ShouldBe(100d);
        }
        else
        {
            if (rebuild)
            {
                await processor.RebuildAsync(CancellationToken.None);
            }
            else
            {
                await processor.CatchUpAsync("amounts", CancellationToken.None);
            }
            var initialValue = registration == "default" ? 0 : 100;
            views.Count.ShouldBe(4);
            views["order-1"].Value.ShouldBe(initialValue + 17);
            views["order-2"].Value.ShouldBe(initialValue + 5);
            views["order-3"].Value.ShouldBe(initialValue + 11);
            views.ContainsKey("order-4").ShouldBeFalse();
            views["order-5"].Value.ShouldBe(initialValue + 13);
            position.ShouldBe(5);
            writes.ShouldBe(ExpectedWrites);
            fixture.RequestedPositions.Last().ShouldBe(5);
            metrics.GetFailureRatePercent().ShouldBe(0d);
        }
    }

    internal sealed class Amount
    {
        public int Value { get; set; }
    }

    private sealed class AmountBuilder : IMaterializedViewBuilder<Amount>
    {
        public AmountBuilder() { }
        public string ViewName => "amounts";
        public IReadOnlyList<Type> HandledEventTypes { get; } = [typeof(HydratedConsumerFixture.AmountAdded)];
        public string GetViewId(IDomainEvent @event) => ((HydratedConsumerFixture.AmountAdded)@event).AggregateId;
        public Amount Apply(Amount view, IDomainEvent @event)
        {
            view.Value += ((HydratedConsumerFixture.AmountAdded)@event).Amount;
            return view;
        }
    }

    private sealed class ExplicitAmountBuilder : IMaterializedViewBuilder<Amount>
    {
        public ExplicitAmountBuilder() { }
        string IMaterializedViewBuilder<Amount>.ViewName => "amounts";
        IReadOnlyList<Type> IMaterializedViewBuilder<Amount>.HandledEventTypes { get; } = [typeof(HydratedConsumerFixture.AmountAdded)];
        string IMaterializedViewBuilder<Amount>.GetViewId(IDomainEvent @event) => ((HydratedConsumerFixture.AmountAdded)@event).AggregateId;
        Amount IMaterializedViewBuilder<Amount>.CreateNew() => new() { Value = 100 };
        Amount IMaterializedViewBuilder<Amount>.Apply(Amount view, IDomainEvent @event)
        {
            view.Value += ((HydratedConsumerFixture.AmountAdded)@event).Amount;
            return view;
        }
    }
}
