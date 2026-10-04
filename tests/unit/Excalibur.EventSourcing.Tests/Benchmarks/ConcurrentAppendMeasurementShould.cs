// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Collections.Concurrent;

using Excalibur.Benchmarks.EventSourcing;
using Excalibur.Dispatch;
using TestDomainEvent = Excalibur.Benchmarks.EventSourcing.TestDomainEvent;

namespace Excalibur.EventSourcing.Tests.Benchmarks;

[Trait("Category", "Unit")]
[Trait("Component", "EventSourcing")]
[Trait("Pattern", "Regression")]
public sealed class ConcurrentAppendMeasurementShould
{
    [Theory]
    [InlineData(AppendOutcome.Failed)]
    [InlineData(AppendOutcome.ConcurrencyConflict)]
    [InlineData(AppendOutcome.AlreadyCommitted)]
    public async Task RejectNonthrowingResultsEvenWhenSomeWritersCommit(AppendOutcome outcome)
    {
        var calls = 0;
        var store = CreateStore(() => Interlocked.Increment(ref calls) == 1 ? outcome switch
        {
            AppendOutcome.Failed => AppendResult.CreateFailure("negative control"),
            AppendOutcome.ConcurrencyConflict => AppendResult.CreateConcurrencyConflict(-1, 0),
            _ => AppendResult.CreateAlreadyCommitted(0, 1),
        } : AppendResult.CreateSuccess(0, 1));
        var benchmark = CreateBenchmark(store);
        benchmark.IterationSetup();

        var error = await Should.ThrowAsync<InvalidOperationException>(benchmark.ConcurrentAppendToDistinctAggregates);

        error.Message.ShouldContain(outcome.ToString());
        calls.ShouldBe(4); // The entire wave is drained before validation; no orphan writers.
        await Should.ThrowAsync<InvalidOperationException>(benchmark.IterationCleanup);
    }

    [Theory]
    [InlineData(3L, 4L, 4L)]
    [InlineData(4L, 3L, 4L)]
    [InlineData(4L, 4L, 3L)]
    [InlineData(5L, 4L, 4L)]
    public async Task RejectIncorrectDurableCounts(long events, long streams, long identities)
    {
        var benchmark = new SqlServerConcurrentAppendBenchmarks(
            CreateStore(() => AppendResult.CreateSuccess(0, 1)),
            _ => Task.FromResult((events, streams, identities))) { WriterCount = 4 };
        benchmark.IterationSetup();
        await benchmark.ConcurrentAppendToDistinctAggregates();

        await Should.ThrowAsync<InvalidOperationException>(benchmark.IterationCleanup);
    }

    [Fact]
    public async Task RejectSkippedWork()
    {
        var benchmark = CreateBenchmark(CreateStore(() => AppendResult.CreateSuccess(0, 1)));
        benchmark.IterationSetup();
        await Should.ThrowAsync<InvalidOperationException>(benchmark.IterationCleanup);
    }

    [Fact]
    public async Task PropagateVerificationFailure()
    {
        var benchmark = new SqlServerConcurrentAppendBenchmarks(
            CreateStore(() => AppendResult.CreateSuccess(0, 1)),
            _ => throw new InvalidOperationException("database unavailable")) { WriterCount = 4 };
        benchmark.IterationSetup();
        await benchmark.ConcurrentAppendToDistinctAggregates();

        var error = await Should.ThrowAsync<InvalidOperationException>(benchmark.IterationCleanup);
        error.Message.ShouldBe("database unavailable");
    }

    [Fact]
    public async Task RejectThrownWriterFailures()
    {
        var benchmark = CreateBenchmark(CreateStore(() => throw new InvalidOperationException("write failed")));
        benchmark.IterationSetup();
        await Should.ThrowAsync<InvalidOperationException>(benchmark.ConcurrentAppendToDistinctAggregates);
        await Should.ThrowAsync<InvalidOperationException>(benchmark.IterationCleanup);
    }

    [Fact]
    public async Task UseFreshIdentitiesAndIsolateEachIteration()
    {
        var events = new ConcurrentBag<(string Type, TestDomainEvent Event)>();
        var store = A.Fake<IEventStore>();
        A.CallTo(() => store.AppendAsync(A<string>._, A<string>._, A<IEnumerable<IDomainEvent>>._, -1, CancellationToken.None))
            .ReturnsLazily((string aggregateId, string aggregateType, IEnumerable<IDomainEvent> batch, long version, CancellationToken token) =>
            {
                var @event = batch.Single().ShouldBeOfType<TestDomainEvent>();
                @event.AggregateId.ShouldBe(aggregateId);
                @event.Version.ShouldBe(0);
                events.Add((aggregateType, @event));
                return new ValueTask<AppendResult>(AppendResult.CreateSuccess(0, 1));
            });
        var benchmark = new SqlServerConcurrentAppendBenchmarks(store, type =>
        {
            var wave = events.Where(e => e.Type == type).Select(e => e.Event).ToArray();
            return Task.FromResult(((long)wave.Length, wave.Select(e => e.AggregateId).Distinct().LongCount(),
                wave.Select(e => e.EventId).Distinct().LongCount()));
        }) { WriterCount = 4 };

        for (var iteration = 0; iteration < 2; iteration++)
        {
            benchmark.IterationSetup();
            await benchmark.ConcurrentAppendToDistinctAggregates();
            await benchmark.IterationCleanup();
        }

        events.Select(e => e.Type).Distinct().Count().ShouldBe(2);
        events.Select(e => e.Event.AggregateId).Distinct().Count().ShouldBe(8);
        events.Select(e => e.Event.EventId).Distinct().Count().ShouldBe(8);
    }

    private static IEventStore CreateStore(Func<AppendResult> append)
    {
        var store = A.Fake<IEventStore>();
        A.CallTo(() => store.AppendAsync(A<string>._, A<string>._, A<IEnumerable<IDomainEvent>>._, -1, CancellationToken.None))
            .ReturnsLazily(() => new ValueTask<AppendResult>(append()));
        return store;
    }

    private static SqlServerConcurrentAppendBenchmarks CreateBenchmark(IEventStore store) =>
        new(store, _ => Task.FromResult((4L, 4L, 4L))) { WriterCount = 4 };
}
