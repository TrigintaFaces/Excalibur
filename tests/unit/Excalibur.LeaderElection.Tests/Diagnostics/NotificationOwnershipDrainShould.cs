// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

namespace Excalibur.LeaderElection.Tests.Diagnostics;
[Trait("Category", "Unit")]
[Trait("Component", "LeaderElection")]
[Trait("Pattern", "Regression")]
public sealed class NotificationOwnershipDrainShould
{
    [Fact]
    public async Task SettleConsumerOwnershipBeforeABlockedFailureReporterReturns()
    {
        var reporterEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseReporter = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var failure = new InvalidOperationException("consumer failed");
        var notifications = new LeaderElectionNotifications(_ =>
        {
            reporterEntered.TrySetResult();
#pragma warning disable RS0030 // Foreign reporter remains blocked until the controller's finally releases it.
            releaseReporter.Task.GetAwaiter().GetResult();
#pragma warning restore RS0030
        });
        try
        {
            var ownership = notifications.Enqueue(() => throw failure);
            await reporterEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            await ownership.WaitAsync(TimeSpan.FromSeconds(5));
            notifications.DrainTask.IsCompletedSuccessfully.ShouldBeTrue();
            notifications.Seal();
            notifications.NotificationsCompleted.IsCompleted.ShouldBeFalse("terminal lifetime still owns the admitted blocked reporter");
        }
        finally
        {
            releaseReporter.TrySetResult();
            notifications.Seal();
            var terminal = await Record.ExceptionAsync(() => notifications.NotificationsCompleted.WaitAsync(TimeSpan.FromSeconds(5)));
            terminal.ShouldBeSameAs(failure);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RetainSchedulingFailureAfterAPriorSuccessfulOwnershipWave(bool throwsSynchronously)
    {
        var failure = new IOException("notification scheduling failed");
        var schedules = 0;
        var notifications = new LeaderElectionNotifications(scheduleWorker: operation =>
        {
            if (Interlocked.Increment(ref schedules) != 2) { return ScheduleAsync(operation); }
            if (throwsSynchronously) { throw failure; }
            return Task.FromException(failure);
        });
        await notifications.Enqueue(() => { }).WaitAsync(TimeSpan.FromSeconds(5));
        await notifications.WorkerObserverTask.WaitAsync(TimeSpan.FromSeconds(5));
        notifications.DrainTask.IsCompletedSuccessfully.ShouldBeTrue();
        var diagnosticCalls = 0;
        var immediate = Record.Exception(() => notifications.EnqueueDiagnostic(() => Interlocked.Increment(ref diagnosticCalls)));
        if (throwsSynchronously) { immediate.ShouldBeSameAs(failure); }
        else { immediate.ShouldBeNull(); }
        await notifications.WorkerObserverTask.WaitAsync(TimeSpan.FromSeconds(5));
        diagnosticCalls.ShouldBe(0, "a failed scheduler cannot be counted as delivered work");
        (await Record.ExceptionAsync(() => notifications.DrainTask.WaitAsync(TimeSpan.FromSeconds(5)))).ShouldBeSameAs(failure);
        var delivered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var next = notifications.Enqueue(() => delivered.TrySetResult());
        var later = await Record.ExceptionAsync(() => next.WaitAsync(TimeSpan.FromSeconds(5)));
        later.ShouldBeSameAs(failure);
        await delivered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        notifications.Seal();
        var terminal = await Record.ExceptionAsync(() => notifications.NotificationsCompleted.WaitAsync(TimeSpan.FromSeconds(5)));
        terminal.ShouldBeSameAs(failure);
    }

    [Fact]
    public async Task KeepNewerQueuedCallbacksWhenAnOlderWorkerFailsAfterDispatchReturns()
    {
        var failure = new IOException("old worker failed after dispatch");
        var firstDispatched = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var failFirst = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var secondEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseSecond = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var trailingDelivered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var schedules = 0;
        var notifications = new LeaderElectionNotifications(scheduleWorker: operation =>
        {
            var worker = ScheduleAsync(operation);
            return Interlocked.Increment(ref schedules) == 1 ? HoldFirstWorkerAsync(worker, firstDispatched, failFirst.Task) : worker;
        });
        var attempts = 0;
        try
        {
            await notifications.Enqueue(() => { }).WaitAsync(TimeSpan.FromSeconds(5));
            var oldObserver = notifications.WorkerObserverTask;
            await firstDispatched.Task.WaitAsync(TimeSpan.FromSeconds(5));
            _ = notifications.Enqueue(() =>
            {
                Interlocked.Increment(ref attempts);
                secondEntered.TrySetResult();
#pragma warning disable RS0030 // Hold the actual newer dispatch while the older worker's separate completion faults.
                releaseSecond.Task.GetAwaiter().GetResult();
#pragma warning restore RS0030
            });
            await secondEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            var trailing = notifications.Enqueue(() => { Interlocked.Increment(ref attempts); trailingDelivered.TrySetResult(); });
            failFirst.TrySetException(failure);
            await oldObserver.WaitAsync(TimeSpan.FromSeconds(5));
            (await Record.ExceptionAsync(() => notifications.DrainTask.WaitAsync(TimeSpan.FromSeconds(5)))).ShouldBeSameAs(failure);
            releaseSecond.TrySetResult();
            await trailingDelivered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            (await Record.ExceptionAsync(() => trailing.WaitAsync(TimeSpan.FromSeconds(5)))).ShouldBeSameAs(failure);
            Volatile.Read(ref attempts).ShouldBe(2, "a stale observer must not clear the newer worker's queued callback");
        }
        finally
        {
            releaseSecond.TrySetResult();
            failFirst.TrySetException(failure);
            notifications.Seal();
            (await Record.ExceptionAsync(() => notifications.NotificationsCompleted.WaitAsync(TimeSpan.FromSeconds(5)))).ShouldBeSameAs(failure);
        }
    }

    private static async Task HoldFirstWorkerAsync(Task dispatch, TaskCompletionSource dispatched, Task release)
    {
        await dispatch.ConfigureAwait(false);
        dispatched.TrySetResult();
        await release.ConfigureAwait(false);
    }

#pragma warning disable RS0030 // Match production's asynchronous LongRunning scheduler; never invoke callbacks inline under the notification gate.
    private static Task ScheduleAsync(Func<Task> operation) => Task.Factory.StartNew(operation,
        CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default).Unwrap();
#pragma warning restore RS0030

    [Fact]
    public async Task ExcludeHeldDiagnosticsButIncludeQueuedAndExecutingOwnershipCallbacks()
    {
        var notifications = new LeaderElectionNotifications();
        var diagnosticEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseDiagnostic = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var ownershipEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseOwnership = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        notifications.EnqueueDiagnostic(() =>
        {
            diagnosticEntered.TrySetResult();
#pragma warning disable RS0030 // Barrier controller releases this synchronous callback in finally.
            releaseDiagnostic.Task.GetAwaiter().GetResult();
#pragma warning restore RS0030
        });
        try
        {
            await diagnosticEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            notifications.DrainTask.IsCompletedSuccessfully.ShouldBeTrue();
            var drain = notifications.Enqueue(() =>
            {
                ownershipEntered.TrySetResult();
#pragma warning disable RS0030 // Barrier controller releases this synchronous callback in finally.
                releaseOwnership.Task.GetAwaiter().GetResult();
#pragma warning restore RS0030
            });
            drain.IsCompleted.ShouldBeFalse("queued ownership behind diagnostics is outstanding history");
            releaseDiagnostic.TrySetResult();
            await ownershipEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            drain.IsCompleted.ShouldBeFalse("dequeue alone does not settle an executing consumer");
            releaseOwnership.TrySetResult();
            await drain.WaitAsync(TimeSpan.FromSeconds(5));
        }
        finally
        {
            releaseDiagnostic.TrySetResult();
            releaseOwnership.TrySetResult();
            notifications.Seal();
            await notifications.NotificationsCompleted.WaitAsync(TimeSpan.FromSeconds(5));
        }
    }
}
