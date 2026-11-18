// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Threading.Channels;
using Excalibur.Dispatch.Delivery;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Time.Testing;

namespace Excalibur.Dispatch.Tests.Messaging.Delivery;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
[Trait("Pattern", "Concurrency")]
public sealed class ScheduledDispositionPersistenceShould
{
    [Fact]
    public async Task ReconcileLateCommittedQuarantineBeforeSkippingItsDisabledRow()
    {
        var clock = new PollClock();
        await using var host = new ScheduleWorkerTestHost(maxAdmissionAttemptsPerOccurrence: 1, clock: clock.Inner, timeProvider: clock);
        var row = await host.StoreRowAsync("failing");
        var healthy = await HealthyAsync(host);
        host.Handler.MessageCallback = (document, _) => document.Marker == "failing"
            ? throw new InvalidOperationException("handler failed") : Task.CompletedTask;
        host.Store.FailureRecord = async (lease, failure, ct) =>
        {
            var committed = await host.Store.Inner.TryRecordFailureAsync(lease, failure, ct);
            if (failure.Disposition == ScheduleFailureDisposition.Quarantined) { throw new IOException("lost quarantine acknowledgment after commit"); }
            return committed;
        };
        await host.Service.StartAsync(CancellationToken.None);
        await ReleasesAsync(host, row.Id, 1);
        await ReleasesAsync(host, healthy, 1);
        await clock.NextAsync();
        clock.Inner.Advance(TimeSpan.FromSeconds(1));
        await ReleasesAsync(host, row.Id, 2);
        await clock.NextAsync();
        (await host.Health.CheckHealthAsync(new HealthCheckContext())).Status.ShouldBe(HealthStatus.Degraded);
        host.Handler.Calls.ShouldBe(2, "the over-bound grant does not dispatch another handler");
        clock.Inner.Advance(TimeSpan.FromSeconds(1));
        await clock.NextAsync();
        (await host.Health.CheckHealthAsync(new HealthCheckContext())).Status.ShouldBe(HealthStatus.Healthy);
        var current = (await host.Store.Inner.GetAllAsync(CancellationToken.None)).Single(x => x.Id == row.Id);
        current.Enabled.ShouldBeFalse();
        current.NextExecutionUtc.ShouldBe(row.NextExecutionUtc);
        host.Handler.Calls.ShouldBe(2);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task BoundUnconfirmedDispositionRetriesWithoutRedispatchingTheHandler(bool throwInsteadOfFalse)
    {
        var clock = new PollClock();
        await using var host = new ScheduleWorkerTestHost(maxAdmissionAttemptsPerOccurrence: 1, clock: clock.Inner, timeProvider: clock);
        var row = await host.StoreRowAsync("failing");
        var healthy = await HealthyAsync(host);
        host.Handler.MessageCallback = (document, _) => document.Marker == "failing"
            ? throw new InvalidOperationException("handler failed") : Task.CompletedTask;
        host.Store.FailureRecord = (lease, failure, ct) => failure.Disposition == ScheduleFailureDisposition.Retrying
            ? host.Store.Inner.TryRecordFailureAsync(lease, failure, ct)
            : throwInsteadOfFalse ? throw new IOException("quarantine persistence rejected") : Task.FromResult(false);
        await host.Service.StartAsync(CancellationToken.None);
        await ReleasesAsync(host, row.Id, 1);
        await ReleasesAsync(host, healthy, 1);
        await clock.NextAsync();
        clock.Inner.Advance(TimeSpan.FromSeconds(1));
        await ReleasesAsync(host, row.Id, 2);
        await clock.NextAsync();
        clock.Inner.Advance(TimeSpan.FromSeconds(1));
        await ReleasesAsync(host, row.Id, 3);
        await clock.NextAsync();
        var writes = host.Store.FailuresById[row.Id];
        var reads = host.Store.FailureReads;
        clock.Inner.Advance(TimeSpan.FromSeconds(1));
        await clock.NextAsync();
        host.Store.FailuresById[row.Id].ShouldBe(writes, "backoff must bound disposition writes");
        host.Store.FailureReads.ShouldBe(reads, "reconciliation reads must honor the same backoff");
        host.Handler.Calls.ShouldBe(2, "failed disposition persistence cannot grant another handler invocation");
        (await host.Health.CheckHealthAsync(new HealthCheckContext())).Status.ShouldBe(HealthStatus.Degraded);
        var current = (await host.Store.Inner.GetAllAsync(CancellationToken.None)).Single(x => x.Id == row.Id);
        current.Enabled.ShouldBeTrue("unconfirmed quarantine must not be claimed as durable disable");
        current.NextExecutionUtc.ShouldBe(row.NextExecutionUtc);
        current.LastExecutionUtc.ShouldBeNull();
    }

    [Fact]
    public async Task RetainExecuteAllBacklogAfterTheFailedOriginalOccurrenceFinallySucceeds()
    {
        var clock = new PollClock();
        await using var host = new ScheduleWorkerTestHost(clock: clock.Inner, timeProvider: clock);
        host.CronOptions.MaxMissedExecutions = 2;
        var row = await host.StoreRowAsync("backlog");
        row.Interval = null;
        row.CronExpression = "* * * * *";
        row.NextExecutionUtc = host.Clock.GetUtcNow().AddMinutes(-3);
        row.MissedExecutionBehavior = MissedExecutionBehavior.ExecuteAllMissed;
        await host.Store.Inner.StoreAsync(row, CancellationToken.None);
        var calls = 0;
        host.Handler.Callback = _ => Interlocked.Increment(ref calls) == 1
            ? throw new InvalidOperationException("first backlog attempt fails") : Task.CompletedTask;
        await host.Service.StartAsync(CancellationToken.None);
        await ReleasesAsync(host, row.Id, 1);
        await clock.NextAsync();
        clock.Inner.Advance(TimeSpan.FromSeconds(1));
        await ReleasesAsync(host, row.Id, 2);
        await clock.NextAsync();
        var current = (await host.Store.Inner.GetAllAsync(CancellationToken.None)).Single();
        current.NextExecutionUtc.ShouldBe(row.NextExecutionUtc!.Value.AddMinutes(1),
            "ExecuteAll preserves the still-due backlog after retrying the original work");
        current.NextExecutionUtc.HasValue.ShouldBeTrue();
        current.NextExecutionUtc!.Value.ShouldBeLessThan(host.Clock.GetUtcNow());
        calls.ShouldBe(2, "one retry is settled in this poll; later backlog remains for the next bounded poll");
    }

    [Fact]
    public async Task ReportUnsupportedConsumedLegacyHistoryAsDegradedWithoutExecutingOrErasingIt()
    {
        var clock = new PollClock();
        await using var host = new ScheduleWorkerTestHost(clock: clock.Inner, timeProvider: clock);
        var row = await host.StoreRowAsync("legacy-unbudgeted");
        var snapshot = (await host.Store.Inner.GetExecutionSnapshotsAsync(CancellationToken.None)).Single();
        var legacy = (await host.Store.Inner.TryAcquireExecutionAsync(row.Id, snapshot.Generation,
            row.NextExecutionUtc!.Value, CancellationToken.None)).ShouldNotBeNull();
        await host.Store.Inner.ReleaseExecutionAsync(legacy, CancellationToken.None);
        await host.Service.StartAsync(CancellationToken.None);
        await clock.NextAsync();
        (await host.Health.CheckHealthAsync(new HealthCheckContext())).Status.ShouldBe(HealthStatus.Degraded);
        host.Handler.Calls.ShouldBe(0);
        var current = (await host.Store.Inner.GetAllAsync(CancellationToken.None)).Single();
        current.NextExecutionUtc.ShouldBe(row.NextExecutionUtc);
        current.Enabled.ShouldBeTrue();
        (await host.Store.Inner.GetOccurrenceFailureAsync(row.Id, snapshot.Generation, row.NextExecutionUtc!.Value,
            row.TenantId, CancellationToken.None)).ShouldBeNull("unsupported history is not falsely called quarantine");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DoNotUseAnOlderRetryRecordToConfirmUnknownNewAdmissionOrProgress(bool failProgress)
    {
        var clock = new PollClock();
        await using var host = new ScheduleWorkerTestHost(clock: clock.Inner, timeProvider: clock);
        var row = await host.StoreRowAsync("old-retry");
        var generation = (await host.Store.Inner.GetExecutionSnapshotsAsync(CancellationToken.None)).Single().Generation;
        var prior = (await host.Store.Inner.TryAcquireExecutionAsync(row.Id, generation, row.NextExecutionUtc!.Value, 10, CancellationToken.None)).ShouldNotBeNull();
        await host.Store.Inner.TryRecordFailureAsync(prior.Lease,
            new(ScheduleFailureReason.HandlerFailed, ScheduleFailureDisposition.Retrying), CancellationToken.None);
        await host.Store.Inner.ReleaseExecutionAsync(prior.Lease, CancellationToken.None);
        if (failProgress)
        {
            host.Store.Completion = (_, _, _, _) => throw new IOException("new progress reply unknown");
        }
        else
        {
            host.Store.BudgetedAdmission = (_, _, _, _, _) => throw new IOException("new admission reply unknown");
        }
        await host.Service.StartAsync(CancellationToken.None);
        await clock.NextAsync();
        clock.Inner.Advance(TimeSpan.FromSeconds(1));
        await clock.NextAsync();
        var attempts = host.Store.Acquisitions;
        clock.Inner.Advance(TimeSpan.FromSeconds(1));
        await clock.NextAsync();
        host.Store.Acquisitions.ShouldBe(attempts,
            "an older retry observation cannot reset unknown-operation health/backoff every poll");
        (await host.Health.CheckHealthAsync(new HealthCheckContext())).Status.ShouldBe(HealthStatus.Degraded);
        (await host.Store.Inner.GetAllAsync(CancellationToken.None)).Single().NextExecutionUtc.ShouldBe(row.NextExecutionUtc);
        host.Store.Completion = null;
        host.Store.BudgetedAdmission = null;
        clock.Inner.Advance(TimeSpan.FromSeconds(3));
        await clock.NextAsync();
        (await host.Health.CheckHealthAsync(new HealthCheckContext())).Status.ShouldBe(HealthStatus.Healthy);
        host.Handler.Calls.ShouldBe(failProgress ? 3 : 1, "actual later fulfillment, not old retry state, clears the degraded observation");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ObserveFalseCompletionAsUnresolvedWithoutTrustingOlderRetryHistory(bool existingRetryHistory)
    {
        var clock = new PollClock();
        await using var host = new ScheduleWorkerTestHost(clock: clock.Inner, timeProvider: clock);
        var row = await host.StoreRowAsync("completion-false");
        var generation = (await host.Store.Inner.GetExecutionSnapshotsAsync(CancellationToken.None)).Single().Generation;
        if (existingRetryHistory)
        {
            var prior = (await host.Store.Inner.TryAcquireExecutionAsync(row.Id, generation,
                row.NextExecutionUtc!.Value, 10, CancellationToken.None)).ShouldNotBeNull();
            (await host.Store.Inner.TryRecordFailureAsync(prior.Lease,
                new(ScheduleFailureReason.HandlerFailed, ScheduleFailureDisposition.Retrying), CancellationToken.None)).ShouldBeTrue();
            await host.Store.Inner.ReleaseExecutionAsync(prior.Lease, CancellationToken.None);
        }
        var healthy = await HealthyAsync(host);
        var healthyCalls = 0;
        host.Handler.MessageCallback = (document, _) =>
        {
            if (document.Marker == "healthy") { Interlocked.Increment(ref healthyCalls); }
            return Task.CompletedTask;
        };
        host.Store.Completion = (lease, result, disposition, ct) => lease.ScheduleId == row.Id
            ? Task.FromResult(false) : host.Store.Inner.TryCompleteExecutionAsync(lease, result, disposition, ct);
        await host.Service.StartAsync(CancellationToken.None);
        await ReleasesAsync(host, row.Id, 1);
        await ReleasesAsync(host, healthy, 1);
        await clock.NextAsync();
        (await host.Health.CheckHealthAsync(new HealthCheckContext())).Status.ShouldBe(HealthStatus.Degraded,
            "false conditional completion is unresolved, not proof of failure or a successful settlement");
        healthyCalls.ShouldBe(1, "another healthy row must still execute and settle");
        (await host.Store.Inner.GetAllAsync(CancellationToken.None)).Single(x => x.Id == healthy).Enabled.ShouldBeFalse();
        clock.Inner.Advance(TimeSpan.FromSeconds(1));
        await ReleasesAsync(host, row.Id, 2);
        await clock.NextAsync();
        var acquisitions = host.Store.Acquisitions;
        var reads = host.Store.FailureReads;
        clock.Inner.Advance(TimeSpan.FromSeconds(1));
        await clock.NextAsync();
        host.Store.Acquisitions.ShouldBe(acquisitions, "existing capped backoff must bound false-completion attempts");
        host.Store.FailureReads.ShouldBe(reads, "an old Retrying record cannot clear generic unknown-operation backoff");
        (await host.Health.CheckHealthAsync(new HealthCheckContext())).Status.ShouldBe(HealthStatus.Degraded);
        var retained = (await host.Store.Inner.GetAllAsync(CancellationToken.None)).Single(x => x.Id == row.Id);
        retained.NextExecutionUtc.ShouldBe(row.NextExecutionUtc);
        retained.LastExecutionUtc.ShouldBeNull();
        retained.Enabled.ShouldBeTrue();
        // A later authoritative conditional completion of the already-observed successful
        // handler work advances the original O. Inventory, not old retry history, clears health.
        var authority = (await host.Store.Inner.TryAcquireExecutionAsync(row.Id, generation,
            row.NextExecutionUtc!.Value, 10, CancellationToken.None)).ShouldNotBeNull();
        var next = clock.Inner.GetUtcNow().AddMinutes(1);
        (await host.Store.Inner.TryCompleteExecutionAsync(authority.Lease,
            new(clock.Inner.GetUtcNow(), next), ScheduleExecutionCompletionDisposition.Fulfilled, CancellationToken.None)).ShouldBeTrue();
        await host.Store.Inner.ReleaseExecutionAsync(authority.Lease, CancellationToken.None);
        var calls = host.Handler.Calls;
        clock.Inner.Advance(TimeSpan.FromSeconds(3));
        await clock.NextAsync();
        (await host.Health.CheckHealthAsync(new HealthCheckContext())).Status.ShouldBe(HealthStatus.Healthy);
        host.Handler.Calls.ShouldBe(calls, "confirmed later progress must not redispatch the fulfilled original O");
        (await host.Store.Inner.GetAllAsync(CancellationToken.None)).Single(x => x.Id == row.Id).NextExecutionUtc.ShouldBe(next);
    }

    private static async Task<Guid> HealthyAsync(ScheduleWorkerTestHost host)
    {
        var row = await host.StoreRowAsync("healthy");
        row.Interval = null;
        await host.Store.Inner.StoreAsync(row, CancellationToken.None);
        return row.Id;
    }

    private static async Task ReleasesAsync(ScheduleWorkerTestHost host, Guid id, int count)
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        while (!host.Store.ReleasesById.TryGetValue(id, out var releases) || releases < count)
        {
            await Task.Delay(TimeSpan.FromMilliseconds(10), deadline.Token); // delay-ok: Observe completed per-schedule releases; the injected clock alone controls scheduling.
        }
    }

    private sealed class PollClock : TimeProvider
    {
        private readonly Channel<byte> _polls = Channel.CreateBounded<byte>(8);
        public FakeTimeProvider Inner { get; } = new(new DateTimeOffset(3000, 1, 1, 0, 0, 0, TimeSpan.Zero));
        public override DateTimeOffset GetUtcNow() => Inner.GetUtcNow();
        public override long GetTimestamp() => Inner.GetTimestamp();
        public override long TimestampFrequency => Inner.TimestampFrequency;
        public override TimeZoneInfo LocalTimeZone => Inner.LocalTimeZone;
        public async Task NextAsync()
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
