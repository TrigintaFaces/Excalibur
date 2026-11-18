// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Collections.Concurrent;
using System.Threading.Channels;
using Microsoft.Extensions.Time.Testing;

namespace Excalibur.Dispatch.Tests.Messaging.Delivery;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
[Trait("Pattern", "Concurrency")]
public sealed class ScheduledOccurrenceIdentityShould
{
    [Fact]
    public async Task ExposeTheSameWorkAcrossRetryAndDifferentWorkAcrossFutureOccurrenceAndOtherSchedule()
    {
        var clock = new ObservedClock();
        await using var host = new ScheduleWorkerTestHost(clock: clock.Inner, timeProvider: clock);
        var row = await host.StoreRowAsync("identity-recurring");
        row.CorrelationId = "original-producer-correlation";
        await host.Store.Inner.StoreAsync(row, CancellationToken.None);
        var healthy = await host.StoreRowAsync("identity-other");
        healthy.Interval = null;
        healthy.CorrelationId = "other-producer-correlation";
        await host.Store.Inner.StoreAsync(healthy, CancellationToken.None);
        var seen = new ConcurrentQueue<Work>();
        host.Handler.ContextObserved = context => seen.Enqueue(Capture(context));
        var recurringCalls = 0;
        host.Handler.MessageCallback = (document, _) =>
        {
            if (document.Marker == "identity-recurring" && Interlocked.Increment(ref recurringCalls) == 1)
            {
                throw new InvalidOperationException("identity first attempt fails");
            }
            return Task.CompletedTask;
        };
        await host.Service.StartAsync(CancellationToken.None);
        await ReleasedAsync(host, row.Id, 1);
        await ReleasedAsync(host, healthy.Id, 1);
        await clock.NextPollAsync();
        clock.Inner.Advance(TimeSpan.FromMinutes(1));
        await ReleasedAsync(host, row.Id, 2);
        await clock.NextPollAsync();
        clock.Inner.Advance(TimeSpan.FromMinutes(1));
        await ReleasedAsync(host, row.Id, 3);
        var own = seen.Where(x => x.Schedule == row.Id).ToArray();
        own.Length.ShouldBe(3);
        own[0].ShouldBe(own[1], "retry preserves the complete stable occurrence key and producer correlation");
        own[2].Store.ShouldBe(own[0].Store);
        own[2].Generation.ShouldBe(own[0].Generation);
        own[2].Occurrence.ShouldBeGreaterThan(own[0].Occurrence);
        own.ShouldAllBe(x => x.Correlation == row.CorrelationId);
        var other = seen.Single(x => x.Schedule == healthy.Id);
        other.Store.ShouldBe(own[0].Store);
        other.Schedule.ShouldNotBe(own[0].Schedule);
        other.Correlation.ShouldBe(healthy.CorrelationId);
        host.Handler.Calls.ShouldBe(4);
    }

    [Fact]
    public async Task PreserveTheHandlerVisibleIdentityAcrossWorkerRestartOnTheSameAuthority()
    {
        var clock = new FakeTimeProvider(new DateTimeOffset(3000, 1, 1, 0, 0, 0, TimeSpan.Zero));
        var store = new ScheduleWorkerTestStore(clock);
        var observed = new ConcurrentQueue<Work>();
        Guid id;
        await using (var first = new ScheduleWorkerTestHost(store, borrowed: true, clock: clock))
        {
            var row = await first.StoreRowAsync("restart-original");
            row.CorrelationId = "producer-correlation";
            await store.Inner.StoreAsync(row, CancellationToken.None);
            id = row.Id;
            first.Handler.ContextObserved = context => observed.Enqueue(Capture(context));
            first.Handler.Callback = _ => throw new InvalidOperationException("first worker fails");
            await first.Service.StartAsync(CancellationToken.None);
            await ReleasedAsync(first, id, 1);
        }
        await using (var second = new ScheduleWorkerTestHost(store, borrowed: true, clock: clock))
        {
            second.Handler.ContextObserved = context => observed.Enqueue(Capture(context));
            await second.Service.StartAsync(CancellationToken.None);
            await ReleasedAsync(second, id, 2);
            second.Handler.Calls.ShouldBe(1);
        }
        observed.Count.ShouldBe(2);
        observed.First().ShouldBe(observed.Last());
        observed.Last().Correlation.ShouldBe("producer-correlation");
        store.Disposals.ShouldBe(0, "borrowed authority is not disposed by either worker host");
        await store.DisposeAsync();
        store.Disposals.ShouldBe(1);
    }

    private static Work Capture(IMessageContext context) => new(
        (Guid)context.Items["ScheduleStoreId"]!, (Guid)context.Items["ScheduleId"]!,
        (Guid)context.Items["ScheduleGeneration"]!, (DateTimeOffset)context.Items["ScheduleOccurrenceUtc"]!, context.CorrelationId);

    private static async Task ReleasedAsync(ScheduleWorkerTestHost host, Guid id, int count)
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        while (!host.Store.ReleasesById.TryGetValue(id, out var releases) || releases < count)
        {
            await Task.Delay(TimeSpan.FromMilliseconds(10), deadline.Token); // delay-ok: Observe actual releases for the exact schedule identity under the existing deadline.
        }
    }

    private sealed record Work(Guid Store, Guid Schedule, Guid Generation, DateTimeOffset Occurrence, string? Correlation);

    private sealed class ObservedClock : TimeProvider
    {
        private readonly Channel<byte> _polls = Channel.CreateBounded<byte>(4);
        public FakeTimeProvider Inner { get; } = new(new DateTimeOffset(3000, 1, 1, 0, 0, 0, TimeSpan.Zero));
        public override DateTimeOffset GetUtcNow() => Inner.GetUtcNow();
        public override long GetTimestamp() => Inner.GetTimestamp();
        public override long TimestampFrequency => Inner.TimestampFrequency;
        public override TimeZoneInfo LocalTimeZone => Inner.LocalTimeZone;
        public async Task NextPollAsync()
        {
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            await _polls.Reader.ReadAsync(deadline.Token);
        }
        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        {
            var timer = Inner.CreateTimer(callback, state, dueTime, period);
            if (dueTime == TimeSpan.FromSeconds(1)) { _polls.Writer.TryWrite(1); }
            return timer;
        }
    }
}
