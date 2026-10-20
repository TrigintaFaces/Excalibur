// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

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
        current.Enabled.ShouldBeFalse("an invalid schedule must stop being due on every poll");
        current.LastExecutionUtc.ShouldBeNull("a failed handler must not be recorded as successful");
        host.Handler.Calls.ShouldBe(1);
        host.Store.ProgressWrites.ShouldBe(1);
        host.Store.ProgressAccepted.ShouldBe((bool?)true);
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
        current.NextExecutionUtc.ShouldBe(host.Clock.GetUtcNow().AddMinutes(5));
        current.LastExecutionUtc.ShouldBeNull();
        host.Handler.Calls.ShouldBe(1);
        host.Store.ProgressWrites.ShouldBe(1);
        host.Store.ProgressAccepted.ShouldBe((bool?)true);
    }
}
