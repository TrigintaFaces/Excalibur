// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch;
using Excalibur.EventSourcing.Projections;
using Excalibur.EventSourcing.Queries;
using Excalibur.EventSourcing.Subscriptions;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using Tests.Shared.Infrastructure;

namespace Excalibur.EventSourcing.Tests.TieredStorage;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
[Trait("Pattern", "EventSourcing")]
public sealed class HydratedProjectionHostsShould
{
    private static readonly string[] ExpectedIds = ["event-1", "event-2", "event-3", "event-5"];

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task AdvanceOnlyAfterApplyingTheHydratedPage(bool asynchronous, bool corruptLaterArchive)
    {
        var fixture = new HydratedConsumerFixture(corruptLaterArchive);
        var finished = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var polls = 0;
        fixture.OnRead = _ =>
        {
            if (++polls == 3 && corruptLaterArchive)
            {
                finished.TrySetResult();
            }
        };
        var applied = new List<string>();
        var total = 0;
        void Apply(IDomainEvent domain)
        {
            var message = domain.ShouldBeOfType<HydratedConsumerFixture.AmountAdded>();
            applied.Add(message.EventId);
            total += message.Amount;
        }

        var checkpoint = A.Fake<ISubscriptionCheckpointStore>();
        A.CallTo(() => checkpoint.GetCheckpointAsync(A<string>._, A<CancellationToken>._))
            .Returns(Task.FromResult<long?>(null));
        var cursorMaps = A.Fake<ICursorMapStore>();
        IReadOnlyDictionary<string, long>? savedMap = null;
        A.CallTo(() => cursorMaps.SaveCursorMapAsync(A<string>._, A<IReadOnlyDictionary<string, long>>._, A<CancellationToken>._))
            .ReturnsLazily(call =>
            {
                savedMap = new Dictionary<string, long>(call.GetArgument<IReadOnlyDictionary<string, long>>(1)!);
                return Task.CompletedTask;
            });
        long? persisted = null;
        var attempts = new List<(long? Expected, long Proposed, string[] Applied, int Total, IReadOnlyDictionary<string, long>? Map)>();
        A.CallTo(() => checkpoint.AdvanceCheckpointAsync(A<string>._, A<long?>._, A<long>._, A<CancellationToken>._))
            .ReturnsLazily(call =>
            {
                attempts.Add((call.GetArgument<long?>(1), call.GetArgument<long>(2), applied.ToArray(), total,
                    savedMap is null ? null : new Dictionary<string, long>(savedMap)));
                persisted = call.GetArgument<long>(2);
                finished.TrySetResult();
                return Task.FromResult(CheckpointAdvanceOutcome.Advanced);
            });

        var services = new ServiceCollection();
        services.AddSingleton<IGlobalStreamQuery>(fixture.Query);
        services.AddSingleton(checkpoint);
        services.AddSingleton(cursorMaps);
        services.AddSingleton<IGlobalStreamProjection<State>>(new RecordingProjection(Apply));
        using var provider = services.BuildServiceProvider();
        var options = Options.Create(new GlobalStreamProjectionOptions
        {
            BatchSize = 10, CheckpointInterval = 1, IdlePollingInterval = TimeSpan.FromMilliseconds(10),
        });
        var registry = new InMemoryProjectionRegistry();
        registry.Register(new ProjectionRegistration(typeof(State), ProjectionMode.Async, new MultiStreamProjection<State>(),
            (events, _, _, _) =>
            {
                foreach (var item in events)
                {
                    Apply(item.Domain);
                }
                return Task.CompletedTask;
            }));
        using BackgroundService host = asynchronous
            ? new AsyncProjectionProcessingHost(registry, fixture.Serializer, checkpoint, options, provider,
                NullLogger<AsyncProjectionProcessingHost>.Instance)
            : new GlobalStreamProjectionHost<State>(provider.GetRequiredService<IServiceScopeFactory>(), fixture.Serializer,
                options, NullLogger<GlobalStreamProjectionHost<State>>.Instance, provider);
        await host.StartAsync(CancellationToken.None);
        try
        {
            await WaitHelpers.AwaitSignalAsync(finished.Task, TestTimeouts.Scale(TimeSpan.FromSeconds(30)));
        }
        finally
        {
            await host.StopAsync(CancellationToken.None);
        }

        if (corruptLaterArchive)
        {
            polls.ShouldBeGreaterThanOrEqualTo(3);
            fixture.RequestedPositions.ShouldAllBe(p => p == 0);
            fixture.ColdReads.Count.ShouldBeGreaterThanOrEqualTo(4);
            fixture.Observed.ShouldContain("event-3");
            fixture.Decoded.ShouldBeEmpty();
            applied.ShouldBeEmpty();
            persisted.ShouldBeNull();
            attempts.ShouldBeEmpty();
            savedMap.ShouldBeNull();
            A.CallTo(() => checkpoint.AdvanceCheckpointAsync(A<string>._, A<long?>._, A<long>._, A<CancellationToken>._))
                .MustNotHaveHappened();
            A.CallTo(() => cursorMaps.SaveCursorMapAsync(A<string>._, A<IReadOnlyDictionary<string, long>>._, A<CancellationToken>._))
                .MustNotHaveHappened();
        }
        else
        {
            persisted.ShouldBe(5);
            applied.ShouldBe(ExpectedIds);
            fixture.Decoded.ShouldBe(ExpectedIds);
            total.ShouldBe(46);
            var attempt = attempts.ShouldHaveSingleItem();
            attempt.Expected.ShouldBeNull();
            attempt.Proposed.ShouldBe(5);
            attempt.Applied.ShouldBe(ExpectedIds);
            attempt.Total.ShouldBe(46);
            if (!asynchronous)
            {
                attempt.Map.ShouldNotBeNull().Count.ShouldBe(5);
                savedMap.ShouldNotBeNull().Count.ShouldBe(5);
                for (var i = 1; i <= 5; i++)
                {
                    savedMap[$"Order:order-{i}"].ShouldBe(0);
                    attempt.Map[$"Order:order-{i}"].ShouldBe(0);
                }
            }
        }
    }

    internal sealed class State
    {
    }

    private sealed class RecordingProjection(Action<IDomainEvent> apply) : IGlobalStreamProjection<State>
    {
        public Task ApplyAsync(IDomainEvent domainEvent, State state, CancellationToken cancellationToken)
        {
            apply(domainEvent);
            return Task.CompletedTask;
        }
    }
}
