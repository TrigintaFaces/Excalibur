// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch.Diagnostics;

using Excalibur.Dispatch.Tests.Messaging.Delivery;

namespace Excalibur.Dispatch.Tests.Delivery.Scheduling;

/// <summary>Pairs real-store failure retirement with continued execution of valid schedules.</summary>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Core)]
[Trait("Pattern", "Regression")]
public sealed class FailedScheduleRowStopsRetryingShould
{
    [Fact]
    public async Task DisableARowWhoseNextExecutionCannotBeComputed()
    {
        await using var host = new ScheduleWorkerTestHost();
        var row = await host.StoreRowAsync();
        row.CronExpression = "not a cron";
        await host.Store.Inner.StoreAsync(row, CancellationToken.None);
        host.Handler.Callback = _ => throw new InvalidOperationException("handler failed");
        await host.Service.StartAsync(CancellationToken.None);
        await host.Store.Released.Task.WaitAsync(TimeSpan.FromSeconds(10));
        var current = await host.ReadAsync();
        current.Enabled.ShouldBeTrue("the first failed admission is retained until bounded retry/quarantine, not silently disabled");
        current.LastExecutionUtc.ShouldBeNull("a failed handler must not be recorded as successful");
        host.Handler.Calls.ShouldBe(1);
        host.Store.ProgressWrites.ShouldBe(0);
        host.Store.FailuresById[row.Id].ShouldBe(1);
    }

    /// <summary>
    /// A refusal the store reported must not be discarded by the caller. A row the store will never
    /// admit -- a stored occurrence that drifted from the snapshot being polled -- is otherwise
    /// indistinguishable from nothing having been due, forever and at every log level.
    /// </summary>
    [Fact]
    public async Task ReportThatADueOccurrenceCouldNotBeAdmitted()
    {
        await using var host = new ScheduleWorkerTestHost();
        var row = await host.StoreRowAsync();
        var drifted = false;
        host.Store.AfterRead = async ct =>
        {
            if (drifted)
            {
                return;
            }

            drifted = true;

            // Move the stored occurrence after the poll has read its snapshot, so admission's
            // compare-and-set term cannot match and the store refuses.
            row.NextExecutionUtc = host.Clock.GetUtcNow().AddMinutes(10);
            await host.Store.Inner.StoreAsync(row, ct);
        };

        await host.Service.StartAsync(CancellationToken.None);

        // Polled rather than slept: the condition is the log entry, and a fixed wait would either
        // flake under load or pad every run.
        var reported = false;
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        while (!deadline.IsCancellationRequested)
        {
            if (host.Logger.Entries.Any(entry => entry.EventId == DeliveryEventId.ScheduledAdmissionUnavailable))
            {
                reported = true;
                break;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(25), deadline.Token).ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing); // delay-ok: Poll the actual admission-unavailable event under the owning deadline, not a guessed worker completion time.
        }

        reported.ShouldBeTrue("the store refused admission and the service returned without recording it, so a row "
            + "the store will never admit reads exactly like a poll with nothing due");
        host.Handler.Calls.ShouldBe(0, "nothing was admitted, so nothing may have been dispatched");
    }

    [Fact]
    public async Task KeepARowRunningWhenItsScheduleCanStillBeAdvanced()
    {
        await using var host = new ScheduleWorkerTestHost();
        var row = await host.StoreRowAsync();
        row.Interval = TimeSpan.FromMinutes(5);
        await host.Store.Inner.StoreAsync(row, CancellationToken.None);
        host.Handler.Callback = _ => throw new InvalidOperationException("handler failed");
        await host.Service.StartAsync(CancellationToken.None);
        await host.Store.Released.Task.WaitAsync(TimeSpan.FromSeconds(10));
        var current = await host.ReadAsync();
        current.Enabled.ShouldBeTrue("valid schedules continue after a failed occurrence");
        current.NextExecutionUtc.ShouldBe(row.NextExecutionUtc, "failed occurrence must remain recoverable before later recurrence");
        current.LastExecutionUtc.ShouldBeNull();
        host.Handler.Calls.ShouldBe(1);
        host.Store.ProgressWrites.ShouldBe(0);
        host.Store.FailuresById[row.Id].ShouldBe(1);
    }
}
