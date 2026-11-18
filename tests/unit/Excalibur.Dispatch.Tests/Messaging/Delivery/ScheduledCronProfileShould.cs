// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0


using System.Collections.Concurrent;
using System.Threading.Channels;
using Excalibur.Dispatch.Delivery;
using Excalibur.Dispatch.Tests.Delivery.Scheduling;
using Microsoft.Extensions.Time.Testing;

namespace Excalibur.Dispatch.Tests.Messaging.Delivery;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
[Trait("Pattern", "Concurrency")]
public sealed class ScheduledCronProfileShould
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CoalesceGapAliasesWithinOneScheduleAndKeepSeparateSchedulesIndependent(bool separate)
    {
        var zone = CronRecurrenceProfileShould.InstalledZone();
        var clock = new PollClock(new(2026, 3, 8, 6, 59, 0, TimeSpan.Zero));
        await using var host = new ScheduleWorkerTestHost(clock: clock.Inner, timeProvider: clock);
        Configure(host);
        IDispatchScheduler scheduler = host.Scheduler;
        foreach (var expression in separate ? new[] { "0 2 * * *", "30 2 * * *", "0 3 * * *" } : ["0,30 2,3 * * *"])
        {
            await scheduler.ScheduleRecurringAsync(expression, zone, CronRecurrenceProfile.CronosCompatible,
                new ScheduleWorkerTestDocument(expression), CancellationToken.None);
        }
        var rows = (await host.Store.GetExecutionSnapshotsAsync(CancellationToken.None)).ToArray();
        rows.Length.ShouldBe(separate ? 3 : 1);
        var seen = new ConcurrentQueue<Invocation>();
        host.Handler.ContextObserved = context => seen.Enqueue(Capture(context));
        await host.Service.StartAsync(CancellationToken.None);
        await clock.NextPollAsync();
        clock.Inner.Advance(TimeSpan.FromMinutes(1));
        foreach (var row in rows) { await ReleasedAsync(host.Store, row.Message.Id, 1); }
        await clock.NextPollAsync();
        var boundary = new DateTimeOffset(2026, 3, 8, 7, 0, 0, TimeSpan.Zero);
        seen.Count.ShouldBe(separate ? 3 : 1);
        seen.ShouldAllBe(x => x.Occurrence == boundary);
        seen.Select(x => x.Id).Distinct().Count().ShouldBe(rows.Length);
        clock.Inner.Advance(TimeSpan.FromMinutes(30));
        if (!separate) { await ReleasedAsync(host.Store, rows[0].Message.Id, 2); }
        await clock.NextPollAsync();
        seen.Count.ShouldBe(separate ? 3 : 2);
        if (!separate)
        {
            seen.Select(x => x.Occurrence).ShouldBe([boundary, boundary.AddMinutes(30)]);
            seen.Select(x => x.Generation).Distinct().Count().ShouldBe(1);
        }
        foreach (var row in await host.Store.GetAllAsync(CancellationToken.None))
        {
            row.CronRecurrenceProfileId.ShouldBe("cronos-compatible");
            row.CronRecurrenceProfileVersion.ShouldBe(1);
            row.TimeZoneId.ShouldBe(zone.Id);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DispatchTheLiteralFoldSeriesAndNoExtraAliases(bool interval)
    {
        var zone = CronRecurrenceProfileShould.InstalledZone();
        var clock = new PollClock(new(2026, 11, 1, 4, 59, 0, TimeSpan.Zero));
        await using var host = new ScheduleWorkerTestHost(clock: clock.Inner, timeProvider: clock);
        Configure(host);
        IDispatchScheduler scheduler = host.Scheduler;
        await scheduler.ScheduleRecurringAsync(interval ? "*/30 1 * * *" : "30 1 * * *", zone,
            CronRecurrenceProfile.CronosCompatible, new ScheduleWorkerTestDocument("fold"), CancellationToken.None);
        var id = (await host.Store.GetAllAsync(CancellationToken.None)).Single().Id;
        var seen = new ConcurrentQueue<Invocation>();
        host.Handler.ContextObserved = context => seen.Enqueue(Capture(context));
        await host.Service.StartAsync(CancellationToken.None);
        await clock.NextPollAsync();
        var expected = interval
            ? new[] { Utc(5, 0), Utc(5, 30), Utc(6, 0), Utc(6, 30) }
            : [Utc(5, 30)];
        for (var index = 0; index < expected.Length; index++)
        {
            clock.Inner.Advance(expected[index] - clock.GetUtcNow());
            await ReleasedAsync(host.Store, id, index + 1);
            await clock.NextPollAsync();
        }
        clock.Inner.Advance(Utc(7, 1) - clock.GetUtcNow());
        await clock.NextPollAsync();
        seen.Select(x => x.Occurrence).ShouldBe(expected);
        seen.Select(x => x.Generation).Distinct().Count().ShouldBe(1);
        host.Handler.Calls.ShouldBe(expected.Length);
    }

    [Fact]
    public async Task PreserveBothFoldSeriesAcrossWorkerRestartOnTheSameStoredProfile()
    {
        var zone = CronRecurrenceProfileShould.InstalledZone();
        var clock = new PollClock(new(2026, 11, 1, 4, 59, 0, TimeSpan.Zero));
        await using var store = new ScheduleWorkerTestStore(clock);
        var seen = new ConcurrentQueue<Invocation>();
        Guid id;
        Guid generation;
        await using (var first = new ScheduleWorkerTestHost(store, borrowed: true, clock: clock.Inner, timeProvider: clock))
        {
            Configure(first);
            IDispatchScheduler scheduler = first.Scheduler;
            await scheduler.ScheduleRecurringAsync("*/30 1 * * *", zone, CronRecurrenceProfile.CronosCompatible,
                new ScheduleWorkerTestDocument("restart"), CancellationToken.None);
            var stored = (await store.GetExecutionSnapshotsAsync(CancellationToken.None)).Single();
            id = stored.Message.Id;
            generation = stored.Generation;
            first.Handler.ContextObserved = context => seen.Enqueue(Capture(context));
            await first.Service.StartAsync(CancellationToken.None);
            await clock.NextPollAsync();
            clock.Inner.Advance(Utc(5, 30) - clock.GetUtcNow());
            await ReleasedAsync(store, id, 2);
            await clock.NextPollAsync();
        }
        var retained = (await store.GetExecutionSnapshotsAsync(CancellationToken.None)).Single();
        retained.Generation.ShouldBe(generation);
        retained.Message.NextExecutionUtc.ShouldBe(Utc(6, 0));
        retained.Message.TimeZoneId.ShouldBe(zone.Id);
        retained.Message.CronRecurrenceProfileId.ShouldBe("cronos-compatible");
        retained.Message.CronRecurrenceProfileVersion.ShouldBe(1);
        await using (var second = new ScheduleWorkerTestHost(store, borrowed: true, clock: clock.Inner, timeProvider: clock))
        {
            Configure(second);
            second.Handler.ContextObserved = context => seen.Enqueue(Capture(context));
            await second.Service.StartAsync(CancellationToken.None);
            await clock.NextPollAsync();
            clock.Inner.Advance(Utc(6, 30) - clock.GetUtcNow());
            await ReleasedAsync(store, id, 4);
            await clock.NextPollAsync();
            clock.Inner.Advance(Utc(7, 1) - clock.GetUtcNow());
            await clock.NextPollAsync();
        }
        seen.Select(x => x.Occurrence).ShouldBe([Utc(5, 0), Utc(5, 30), Utc(6, 0), Utc(6, 30)]);
        seen.ShouldAllBe(x => x.Id == id && x.Generation == generation);
        store.Disposals.ShouldBe(0, "both hosts borrow the same authority");
    }

    [Fact]
    public async Task CancelBeforeTheSecondFoldAdmissionWithoutBlockingAnOrdinarySchedule()
    {
        var zone = CronRecurrenceProfileShould.InstalledZone();
        var clock = new PollClock(new(2026, 11, 1, 4, 59, 0, TimeSpan.Zero));
        await using var host = new ScheduleWorkerTestHost(clock: clock.Inner, timeProvider: clock);
        Configure(host);
        IDispatchScheduler scheduler = host.Scheduler;
        await scheduler.ScheduleRecurringAsync("*/30 1 * * *", zone, CronRecurrenceProfile.CronosCompatible,
            new ScheduleWorkerTestDocument("cancel"), CancellationToken.None);
        var id = (await host.Store.GetAllAsync(CancellationToken.None)).Single().Id;
        var seen = new ConcurrentQueue<Invocation>();
        host.Handler.ContextObserved = context => seen.Enqueue(Capture(context));
        await host.Service.StartAsync(CancellationToken.None);
        await clock.NextPollAsync();
        clock.Inner.Advance(Utc(5, 30) - clock.GetUtcNow());
        await ReleasedAsync(host.Store, id, 2);
        await clock.NextPollAsync();
        await host.Scheduler.CancelScheduleAsync(id, CancellationToken.None);
        // Public producer schedules a distinct ordinary UTC occurrence after cancellation.
        await scheduler.ScheduleOnceAsync(Utc(6, 30), new ScheduleWorkerTestDocument("healthy"), CancellationToken.None);
        var healthy = (await host.Store.GetAllAsync(CancellationToken.None)).Single(x => x.Id != id);
        clock.Inner.Advance(Utc(6, 30) - clock.GetUtcNow());
        await ReleasedAsync(host.Store, healthy.Id, 1);
        await clock.NextPollAsync();
        seen.Where(x => x.Id == id).Select(x => x.Occurrence).ShouldBe([Utc(5, 0), Utc(5, 30)]);
        seen.Count(x => x.Id == healthy.Id).ShouldBe(1);
        (await host.Store.GetAllAsync(CancellationToken.None)).Single(x => x.Id == id).Enabled.ShouldBeFalse();
    }

    [Fact]
    public async Task KeepTwoOrdinaryUtcFiringsWithoutTransitionAliases()
    {
        var clock = new PollClock(new(2026, 11, 2, 0, 59, 0, TimeSpan.Zero));
        await using var host = new ScheduleWorkerTestHost(clock: clock.Inner, timeProvider: clock);
        Configure(host);
        IDispatchScheduler scheduler = host.Scheduler;
        await scheduler.ScheduleRecurringAsync("*/30 1 * * *", TimeZoneInfo.Utc,
            CronRecurrenceProfile.CronosCompatible, new ScheduleWorkerTestDocument("ordinary"), CancellationToken.None);
        var id = (await host.Store.GetAllAsync(CancellationToken.None)).Single().Id;
        var seen = new ConcurrentQueue<Invocation>();
        host.Handler.ContextObserved = context => seen.Enqueue(Capture(context));
        await host.Service.StartAsync(CancellationToken.None);
        await clock.NextPollAsync();
        clock.Inner.Advance(TimeSpan.FromMinutes(32));
        await ReleasedAsync(host.Store, id, 2);
        await clock.NextPollAsync();
        seen.Select(x => x.Occurrence).ShouldBe([
            new DateTimeOffset(2026, 11, 2, 1, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 11, 2, 1, 30, 0, TimeSpan.Zero)]);
        host.Handler.Calls.ShouldBe(2);
    }

    private static void Configure(ScheduleWorkerTestHost host)
    {
        host.CronOptions.MissedExecutionBehavior = MissedExecutionBehavior.ExecuteAllMissed;
        host.CronOptions.MaxMissedExecutions = 10;
    }

    private static DateTimeOffset Utc(int hour, int minute) => new(2026, 11, 1, hour, minute, 0, TimeSpan.Zero);
    private static Invocation Capture(IMessageContext context) => new((Guid)context.Items["ScheduleId"]!,
        (Guid)context.Items["ScheduleGeneration"]!, (DateTimeOffset)context.Items["ScheduleOccurrenceUtc"]!);
    private sealed record Invocation(Guid Id, Guid Generation, DateTimeOffset Occurrence);

    private static async Task ReleasedAsync(ScheduleWorkerTestStore store, Guid id, int expected)
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        while (!store.ReleasesById.TryGetValue(id, out var count) || count < expected)
        {
            await Task.Delay(TimeSpan.FromMilliseconds(10), deadline.Token); // delay-ok: Observe completed releases for the exact schedule; the injected clock alone controls cron due-ness.
        }
    }

    private sealed class PollClock(DateTimeOffset start) : TimeProvider
    {
        public FakeTimeProvider Inner { get; } = new(start);
        private readonly Channel<bool> _armed = Channel.CreateUnbounded<bool>();
        public override DateTimeOffset GetUtcNow() => Inner.GetUtcNow();
        public override long GetTimestamp() => Inner.GetTimestamp();
        public override long TimestampFrequency => Inner.TimestampFrequency;
        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        {
            // Signal only AFTER the real injected timer has been registered.
            var timer = Inner.CreateTimer(callback, state, dueTime, period);
            if (dueTime == TimeSpan.FromSeconds(1)) { _armed.Writer.TryWrite(true); }
            return timer;
        }
        public async Task NextPollAsync()
        {
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            _ = await _armed.Reader.ReadAsync(deadline.Token);
        }
    }
}
