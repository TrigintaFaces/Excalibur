// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch;

namespace Excalibur.EventSourcing.Tests.InMemory;

[Trait("Category", "Unit")]
[Trait("Component", "EventSourcing")]
[Trait("Pattern", "Behavior")]
public sealed class InMemoryEventStorePartialLoadShould
{
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(16)]
    [InlineData(1000)]
    public async Task MatchAnIndependentLinearPredicateAtEveryThreshold(int count)
    {
        var store = new InMemoryEventStore(UntenantedContext.Instance);
        var id = Guid.NewGuid().ToString();
        var events = CreateEvents(id, count);
        (await store.AppendAsync(id, "Order", events, -1, CancellationToken.None)).Success.ShouldBeTrue();

        await AssertSuffixes(store, id);
    }

    [Fact]
    public async Task PreserveSuffixesAfterReplayConflictAppendAndErasure()
    {
        var store = new InMemoryEventStore(UntenantedContext.Instance);
        var id = Guid.NewGuid().ToString();
        var events = CreateEvents(id, 17);
        (await store.AppendAsync(id, "Order", events, -1, CancellationToken.None)).Success.ShouldBeTrue();
        (await store.AppendAsync(id, "Order", events, -1, CancellationToken.None)).Success.ShouldBeTrue();
        (await store.AppendAsync(id, "Order", CreateEvents(id, 1), -1, CancellationToken.None)).IsConcurrencyConflict.ShouldBeTrue();
        (await store.AppendAsync(id, "Order", CreateEvents(id, 4), 16, CancellationToken.None)).Success.ShouldBeTrue();
        await AssertSuffixes(store, id);

        (await ((IEventStoreErasure)store).EraseEventsAsync(id, "Order", Guid.NewGuid(), CancellationToken.None)).ShouldBe(21);
        await AssertSuffixes(store, id);
        (await store.LoadAsync(id, "Order", 18, CancellationToken.None)).ShouldAllBe(row => row.EventType == "$erased");
    }

    [Fact]
    public async Task PreserveExistingEmptyStreamContainersAfterAnAppendConflict()
    {
        var store = new InMemoryEventStore(UntenantedContext.Instance);
        var id = Guid.NewGuid().ToString();
        // The rejected append creates the stream before comparing its current version.
        (await store.AppendAsync(id, "Order", CreateEvents(id, 1), 0, CancellationToken.None)).IsConcurrencyConflict.ShouldBeTrue();

        var first = await store.LoadAsync(id, "Order", 0, CancellationToken.None);
        var second = await store.LoadAsync(id, "Order", long.MaxValue, CancellationToken.None);
        first.ShouldBeEmpty();
        second.ShouldBeEmpty();
        first.ShouldNotBeSameAs(second);
        first.ShouldNotBeSameAs(Array.Empty<StoredEvent>());
    }

    [Fact]
    public async Task PreserveSharedEmptyResultsForMissingAndExhaustedStreams()
    {
        var store = new InMemoryEventStore(UntenantedContext.Instance);
        var id = Guid.NewGuid().ToString();
        (await store.LoadAsync(id, "Order", 0, CancellationToken.None)).ShouldBeSameAs(Array.Empty<StoredEvent>());
        (await store.AppendAsync(id, "Order", CreateEvents(id, 1), -1, CancellationToken.None)).Success.ShouldBeTrue();
        (await store.LoadAsync(id, "Order", 0, CancellationToken.None)).ShouldBeSameAs(Array.Empty<StoredEvent>());
        (await store.LoadAsync(id, "Order", long.MaxValue, CancellationToken.None)).ShouldBeSameAs(Array.Empty<StoredEvent>());
    }

    [Fact]
    public async Task HonorCancellationBeforeSearchingEvenForAMissingStream()
    {
        var store = new InMemoryEventStore(UntenantedContext.Instance);
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();
        await Should.ThrowAsync<OperationCanceledException>(() => store.LoadAsync("missing", "Order", long.MaxValue, cancellation.Token).AsTask());
    }

    private static async Task AssertSuffixes(InMemoryEventStore store, string id)
    {
        var full = await store.LoadAsync(id, "Order", CancellationToken.None);
        var thresholds = Enumerable.Range(0, full.Count + 1).Select(index => (long)index)
            .Concat([long.MinValue, -2, -1, long.MaxValue]);
        foreach (var threshold in thresholds)
        {
            var expected = full.Where(row => threshold < 0 || row.Version > threshold).ToArray();
            var actual = await store.LoadAsync(id, "Order", threshold, CancellationToken.None);
            actual.Count.ShouldBe(expected.Length);
            for (var index = 0; index < expected.Length; index++)
            {
                actual[index].ShouldBeSameAs(expected[index]);
            }

            if (actual.Count > 0)
            {
                var second = await store.LoadAsync(id, "Order", threshold, CancellationToken.None);
                actual.ShouldNotBeSameAs(second);
                var array = actual.ShouldBeOfType<StoredEvent[]>();
                array[0] = null!;
                second[0].ShouldBeSameAs(expected[0]);
                (await store.LoadAsync(id, "Order", threshold, CancellationToken.None))[0].ShouldBeSameAs(expected[0]);
            }
        }
    }

    private static IReadOnlyList<IDomainEvent> CreateEvents(string id, int count) => Enumerable.Range(0, count)
        .Select(index => (IDomainEvent)new SearchEvent
        {
            EventId = Guid.NewGuid().ToString(),
            AggregateId = id,
            Version = long.MaxValue - index,
        }).ToArray();

    [MessageName("Test.EventStore.PartialLoad.SearchEvent")]
    private sealed class SearchEvent : IDomainEvent
    {
        public required string EventId { get; init; }
        public required string AggregateId { get; init; }
        public required long Version { get; init; }
        public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;
        public IDictionary<string, object>? Metadata { get; init; }
    }
}
