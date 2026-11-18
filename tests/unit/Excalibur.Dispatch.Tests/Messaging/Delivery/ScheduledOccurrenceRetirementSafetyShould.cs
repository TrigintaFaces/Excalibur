// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Microsoft.Extensions.Time.Testing;

namespace Excalibur.Dispatch.Tests.Messaging.Delivery;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
[Trait("Pattern", "Regression")]
public sealed class ScheduledOccurrenceRetirementSafetyShould
{
    [Fact]
    public async Task RefuseToAdvanceAnUnpreservedFailedOccurrenceBeforeLaterSuccessOverwritesItsHistory()
    {
        var trace = await ExerciseAsync(failFirst: true);
        trace.FailedCalls.ShouldBe(1);
        trace.SuccessfulCalls.ShouldBe(1);
        trace.HealthyCalls.ShouldBe(1);
        trace.FirstGeneration.ShouldBe(trace.FinalGeneration);
        trace.LastAfterFailure.ShouldBeNull();
        trace.LastAfterSuccess.ShouldBe(trace.LaterClock);
        trace.PayloadAfterSuccess.ShouldBe(trace.OriginalPayload);
        trace.NextAfterSuccess.ShouldBeGreaterThan(trace.LaterClock);
        Console.WriteLine($"R11 CURRENT-API TRACE occurrence={trace.OriginalOccurrence:O}; " +
            $"afterFailureNext={trace.NextAfterFailure:O}; afterSuccessLast={trace.LastAfterSuccess:O}; " +
            $"generation={trace.FinalGeneration}; failed=1; laterSuccess=1; healthy=1");

        // This current store has no outcome/quarantine capability. Advancing this occurrence loses its
        // recoverable identity; a future capability test must separately prove atomic preservation.
        trace.NextAfterFailure.ShouldBe(trace.OriginalOccurrence,
            "R11 NO-LOSS: a failed recurring occurrence must remain due when no durable terminal outcome or quarantine preserved it");
    }

    [Fact]
    public async Task ContinueTheHealthyRowAfterARecurringFailureWithoutPretendingThatFailureSucceeded()
    {
        var trace = await ExerciseAsync(failFirst: true);
        trace.FailedCalls.ShouldBe(1);
        trace.HealthyCalls.ShouldBe(1);
        trace.SuccessfulCalls.ShouldBe(1);
        trace.LastAfterFailure.ShouldBeNull();
        trace.LastAfterSuccess.ShouldBe(trace.LaterClock);
    }

    [Fact]
    public async Task FulfillTwoNormalRecurrencesAndTheIndependentHealthyRow()
    {
        var trace = await ExerciseAsync(failFirst: false);
        trace.FailedCalls.ShouldBe(0);
        trace.SuccessfulCalls.ShouldBe(2);
        trace.HealthyCalls.ShouldBe(1);
        trace.LastAfterFailure.ShouldBe(trace.OriginalOccurrence);
        trace.LastAfterSuccess.ShouldBe(trace.LaterClock);
        trace.NextAfterSuccess.ShouldBeGreaterThan(trace.LaterClock);
        trace.FirstGeneration.ShouldBe(trace.FinalGeneration);
    }

    private static async Task<Trace> ExerciseAsync(bool failFirst)
    {
        var clock = new PollObservedClock();
        await using var host = new ScheduleWorkerTestHost(clock: clock.Inner, timeProvider: clock);
        var recurring = await host.StoreRowAsync("retirement-recurring");
        var healthy = await host.StoreRowAsync("retirement-healthy");
        healthy.Interval = null;
        await host.Store.Inner.StoreAsync(healthy, CancellationToken.None);
        var originalOccurrence = recurring.NextExecutionUtc.ShouldNotBeNull();
        var originalPayload = recurring.MessageBody;
        var original = (await host.Store.GetExecutionSnapshotsAsync(CancellationToken.None))
            .Single(x => x.Message.Id == recurring.Id);
        var recurringCalls = 0;
        var failedCalls = 0;
        var successfulCalls = 0;
        var healthyCalls = 0;
        host.Handler.MessageCallback = (document, _) =>
        {
            if (document.Marker == "retirement-healthy")
            {
                Interlocked.Increment(ref healthyCalls);
                return Task.CompletedTask;
            }

            document.Marker.ShouldBe("retirement-recurring");
            var attempt = Interlocked.Increment(ref recurringCalls);
            if (failFirst && attempt == 1)
            {
                Interlocked.Increment(ref failedCalls);
                throw new InvalidOperationException("R11 deliberate first-occurrence handler failure");
            }

            Interlocked.Increment(ref successfulCalls);
            return Task.CompletedTask;
        };
        await host.Service.StartAsync(CancellationToken.None);
        await WaitForReleasesAsync(host, recurring.Id, 1);
        await WaitForReleasesAsync(host, healthy.Id, 1);
        var afterFailure = (await host.Store.GetExecutionSnapshotsAsync(CancellationToken.None))
            .Single(x => x.Message.Id == recurring.Id);
        var nextAfterFailure = afterFailure.Message.NextExecutionUtc.ShouldNotBeNull();
        var lastAfterFailure = afterFailure.Message.LastExecutionUtc;
        await clock.PollArmed.Task.WaitAsync(TimeSpan.FromSeconds(10));
        host.Clock.Advance(TimeSpan.FromMinutes(1));
        var laterClock = host.Clock.GetUtcNow();
        await WaitForReleasesAsync(host, recurring.Id, 2);
        var afterSuccess = (await host.Store.GetExecutionSnapshotsAsync(CancellationToken.None))
            .Single(x => x.Message.Id == recurring.Id);
        return new Trace(originalOccurrence, nextAfterFailure, lastAfterFailure,
            afterSuccess.Message.NextExecutionUtc.ShouldNotBeNull(), afterSuccess.Message.LastExecutionUtc,
            laterClock, original.Generation, afterSuccess.Generation, originalPayload,
            afterSuccess.Message.MessageBody, Volatile.Read(ref failedCalls),
            Volatile.Read(ref successfulCalls), Volatile.Read(ref healthyCalls));
    }

    private static async Task WaitForReleasesAsync(ScheduleWorkerTestHost host, Guid id, int count)
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        // This delay only observes completed real worker operations; the injected clock alone controls due-ness.
        while (!host.Store.ReleasesById.TryGetValue(id, out var observed) || observed < count)
        {
            await Task.Delay(TimeSpan.FromMilliseconds(10), deadline.Token); // delay-ok: Observe completed real worker operations; the injected clock alone controls due-ness.
        }
    }

    private sealed class PollObservedClock : TimeProvider
    {
        public FakeTimeProvider Inner { get; } = new(new DateTimeOffset(3000, 1, 1, 0, 0, 0, TimeSpan.Zero));
        public TaskCompletionSource PollArmed { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public override DateTimeOffset GetUtcNow() => Inner.GetUtcNow();
        public override long GetTimestamp() => Inner.GetTimestamp();
        public override long TimestampFrequency => Inner.TimestampFrequency;
        public override TimeZoneInfo LocalTimeZone => Inner.LocalTimeZone;
        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        {
            var timer = Inner.CreateTimer(callback, state, dueTime, period);
            if (dueTime == TimeSpan.FromSeconds(1)) { PollArmed.TrySetResult(); }
            return timer;
        }
    }

    private sealed record Trace(DateTimeOffset OriginalOccurrence, DateTimeOffset NextAfterFailure,
        DateTimeOffset? LastAfterFailure, DateTimeOffset NextAfterSuccess, DateTimeOffset? LastAfterSuccess,
        DateTimeOffset LaterClock, Guid FirstGeneration, Guid FinalGeneration, string OriginalPayload,
        string PayloadAfterSuccess, int FailedCalls, int SuccessfulCalls, int HealthyCalls);
}
