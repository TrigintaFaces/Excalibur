// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0
using Excalibur.Dispatch.LeaderElection;
using Excalibur.LeaderElection.InMemory;

namespace Excalibur.LeaderElection.Tests;

[Trait("Category", "Unit")]
[Trait("Component", "LeaderElection")]
[Trait("Pattern", "Regression")]
public sealed class NotificationCompletionShould
{
    [Fact]
    public async Task KeepOneTerminalTaskAcrossStopAndRestartAndDrainHeldCallbacksAfterResourceDisposal()
    {
        await using var election = Create();
        var terminal = election.NotificationsCompleted;
        terminal.IsCompleted.ShouldBeFalse();
        var firstLossDelivered = Signal();
        election.LeaderChanged += (_, transition) =>
        {
            if (transition.NewLeaderId is null) { firstLossDelivered.TrySetResult(); }
        };
        await election.StartAsync(CancellationToken.None);
        await election.StopAsync(CancellationToken.None);
        await firstLossDelivered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        election.NotificationsCompleted.ShouldBeSameAs(terminal);
        terminal.IsCompleted.ShouldBeFalse();

        var entered = Signal();
        using var release = new ManualResetEventSlim();
        election.BecameLeader += (_, _) =>
        {
            entered.TrySetResult();
#pragma warning disable RS0030 // Adversarial synchronous consumer held independently of resource disposal.
            release.Wait();
#pragma warning restore RS0030
        };
#pragma warning disable RS0030 // Own the potentially synchronous legacy Start call so its callback cannot block the test driver.
        var starting = Task.Run(() => election.StartAsync(CancellationToken.None));
#pragma warning restore RS0030
        Task? disposing = null;
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            disposing = election.DisposeAsync().AsTask();
            await disposing.WaitAsync(TimeSpan.FromSeconds(5));
            election.IsLeader.ShouldBeFalse();
            election.NotificationsCompleted.ShouldBeSameAs(terminal);
            terminal.IsCompleted.ShouldBeFalse();
        }
        finally
        {
            release.Set();
            await starting.WaitAsync(TimeSpan.FromSeconds(5));
            await (disposing ?? election.DisposeAsync().AsTask()).WaitAsync(TimeSpan.FromSeconds(5));
            await terminal.WaitAsync(TimeSpan.FromSeconds(5));
        }
        await Should.ThrowAsync<ObjectDisposedException>(() => election.StartAsync(CancellationToken.None));
    }

    [Fact]
    public async Task PermitIndependentCandidateStartInsideAnotherCandidatesNotification()
    {
        var first = Create();
        var independent = Create();
        var observed = new TaskCompletionSource<Exception?>(TaskCreationOptions.RunContinuationsAsynchronously);
        first.BecameLeader += (_, _) =>
        {
            try
            {
#pragma warning disable RS0030 // Exercise actual synchronous subscriber scope against an independent provider domain.
                independent.StartAsync(CancellationToken.None).GetAwaiter().GetResult();
#pragma warning restore RS0030
                observed.TrySetResult(null);
            }
            catch (Exception error) { observed.TrySetResult(error); }
        };
        try
        {
            await first.StartAsync(CancellationToken.None);
            (await observed.Task.WaitAsync(TimeSpan.FromSeconds(5))).ShouldBeNull();
            independent.IsLeader.ShouldBeTrue();
        }
        finally
        {
            await first.DisposeAsync();
            await independent.DisposeAsync();
            await Task.WhenAll(first.NotificationsCompleted, independent.NotificationsCompleted).WaitAsync(TimeSpan.FromSeconds(5));
        }
    }

    private static InMemoryLeaderElection Create() => new("notification-" + Guid.NewGuid().ToString("N"),
        Options.Create(new LeaderElectionOptions()), logger: null);
    private static TaskCompletionSource Signal() => new(TaskCreationOptions.RunContinuationsAsynchronously);
}
