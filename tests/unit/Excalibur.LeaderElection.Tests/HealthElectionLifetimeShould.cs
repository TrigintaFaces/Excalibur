// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0
using System.Transactions;

namespace Excalibur.LeaderElection.Tests;

[Trait("Category", "Unit")]
[Trait("Component", "LeaderElection")]
public sealed class HealthElectionLifetimeShould
{
    [Fact]
    public async Task RetireImmediatelyButJoinHeldMutationAndReadBeforeDisposalCompletes()
    {
        var mutation = Signal(); var read = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        var retired = Signal();
        var owner = Owner(dispose: () => { retired.TrySetResult(); return ValueTask.CompletedTask; });
        var updating = owner.MutateAsync(_ => mutation.Task, null, CancellationToken.None);
        var reading = owner.ReadAsync(_ => read.Task, CancellationToken.None);
        var disposal = owner.DisposeAsync().AsTask();
        await retired.Task.WaitAsync(TimeSpan.FromSeconds(5));
        disposal.IsCompleted.ShouldBeFalse();
        mutation.TrySetResult(); await updating;
        disposal.IsCompleted.ShouldBeFalse();
        read.TrySetResult(7); (await reading).ShouldBe(7);
        await disposal.WaitAsync(TimeSpan.FromSeconds(5));
        await Should.ThrowAsync<ObjectDisposedException>(() => owner.ReadAsync(_ => Task.FromResult(8), CancellationToken.None));
    }

    [Fact]
    public async Task StopCannotFinishBeforeAPendingDdlStartHasSettled()
    {
        var ddl = Signal(); var entered = Signal(); var admissions = 0; var stopped = 0;
        var owner = Owner(start: async token =>
        {
            entered.TrySetResult(); await ddl.Task;
            token.ThrowIfCancellationRequested(); Interlocked.Increment(ref admissions);
        }, stop: _ => { Interlocked.Increment(ref stopped); return Task.CompletedTask; });
        var starting = owner.StartAsync(CancellationToken.None);
        await entered.Task;
        var stopping = owner.StopAsync(CancellationToken.None);
        stopping.IsCompleted.ShouldBeFalse(); stopped.ShouldBe(1);
        ddl.TrySetResult();
        await Should.ThrowAsync<OperationCanceledException>(() => starting);
        await stopping.WaitAsync(TimeSpan.FromSeconds(5));
        admissions.ShouldBe(0); stopped.ShouldBe(2);
        await owner.DisposeAsync();
    }

    [Fact]
    public async Task RetainThePostSdkAuthorityEffectBeforeRemoveAndFreshUpdateAdmission()
    {
        var authority = Signal(); var entered = Signal(); var removed = false; var newUpdate = false;
        var owner = Owner(remove: _ => { removed = true; return Task.CompletedTask; });
        var old = owner.MutateAsync(_ => Task.CompletedTask, async () => { entered.TrySetResult(); await authority.Task; }, CancellationToken.None);
        await entered.Task;
        var stopping = owner.StopAsync(CancellationToken.None);
        var fresh = owner.MutateAsync(_ => { removed.ShouldBeTrue(); newUpdate = true; return Task.CompletedTask; }, null, CancellationToken.None);
        stopping.IsCompleted.ShouldBeFalse(); removed.ShouldBeFalse(); newUpdate.ShouldBeFalse();
        authority.TrySetResult(); await old; await stopping; await fresh;
        removed.ShouldBeTrue(); newUpdate.ShouldBeTrue();
        await owner.DisposeAsync();
    }

    [Fact]
    public async Task LeaveReadOnlyCallsAvailableDuringStopButJoinThemOnDispose()
    {
        var mutation = Signal(); var read = Signal();
        var owner = Owner();
        var updating = owner.MutateAsync(_ => mutation.Task, null, CancellationToken.None);
        var stopping = owner.StopAsync(CancellationToken.None);
        var reading = owner.ReadAsync(async _ => { await read.Task; return 42; }, CancellationToken.None);
        mutation.TrySetResult(); await updating; await stopping;
        reading.IsCompleted.ShouldBeFalse();
        var disposing = owner.DisposeAsync().AsTask(); disposing.IsCompleted.ShouldBeFalse();
        read.TrySetResult(); (await reading).ShouldBe(42); await disposing;
    }

    [Fact]
    public async Task MemoizeTheActualPendingAndFaultedDisposal()
    {
        var reply = Signal(); var failure = new IOException("physical disposal failed"); var calls = 0;
        var owner = Owner(dispose: async () => { Interlocked.Increment(ref calls); await reply.Task; throw failure; });
        var first = owner.DisposeAsync().AsTask(); var second = owner.DisposeAsync().AsTask();
        first.ShouldBeSameAs(second); first.IsCompleted.ShouldBeFalse(); calls.ShouldBe(1);
        reply.TrySetResult();
        (await Should.ThrowAsync<IOException>(() => first)).ShouldBeSameAs(failure);
        (await Should.ThrowAsync<IOException>(() => owner.DisposeAsync().AsTask())).ShouldBeSameAs(failure);
        calls.ShouldBe(1);
    }

    [Fact]
    public async Task CancelTheCallerWaitWithoutAbandoningHeldNativeWork()
    {
        var reply = Signal(); using var caller = new CancellationTokenSource();
        var owner = Owner();
        var updating = owner.MutateAsync(_ => reply.Task, null, caller.Token);
        caller.Cancel(); await Should.ThrowAsync<OperationCanceledException>(() => updating);
        var disposal = owner.DisposeAsync().AsTask(); disposal.IsCompleted.ShouldBeFalse();
        reply.TrySetResult(); await disposal;
    }

    [Fact]
    public async Task RejectSameNativeOperationLifecycleReentryBeforeSealingTheOwner()
    {
        var owner = Owner();
        await owner.MutateAsync(async _ =>
        {
            await Should.ThrowAsync<InvalidOperationException>(() => owner.StopAsync(CancellationToken.None));
            await Should.ThrowAsync<InvalidOperationException>(() => owner.DisposeAsync().AsTask());
        }, null, CancellationToken.None);
        (await owner.ReadAsync(_ => Task.FromResult(1), CancellationToken.None)).ShouldBe(1);
        await owner.StopAsync(CancellationToken.None); await owner.DisposeAsync();
    }

    [Fact]
    public async Task PreserveCallerBusinessTransactionAcrossHealthDml()
    {
        var owner = Owner();
        using (var business = new TransactionScope(TransactionScopeOption.Required, TransactionScopeAsyncFlowOption.Enabled))
        {
            var original = Transaction.Current.ShouldNotBeNull();
            await owner.MutateAsync(_ => { Transaction.Current.ShouldBeSameAs(original); return Task.CompletedTask; }, null, CancellationToken.None);
            (await owner.ReadAsync(_ => Task.FromResult(ReferenceEquals(Transaction.Current, original)), CancellationToken.None)).ShouldBeTrue();
            Transaction.Current.ShouldBeSameAs(original);
        }
        await owner.DisposeAsync();
    }

    [Fact]
    public async Task RetainAnUnrelatedCancellationFaultDuringOwnedShutdown()
    {
        var reply = Signal(); var failure = new OperationCanceledException(new CancellationToken(true));
        var owner = Owner();
        var updating = owner.MutateAsync(async _ => { await reply.Task; throw failure; }, null, CancellationToken.None);
        var disposal = owner.DisposeAsync().AsTask();
        reply.TrySetResult();
        // Shouldly 4.3 reconstructs canceled tasks as TaskCanceledException, losing identity.
        // Await directly so this control observes the actual exceptions from both operations.
        Exception? updateFailure = null;
        try { await updating; } catch (Exception error) { updateFailure = error; }
        Exception? disposalFailure = null;
        try { await disposal; } catch (Exception error) { disposalFailure = error; }
        updateFailure.ShouldBeSameAs(failure);
        disposalFailure.ShouldBeSameAs(failure);
        ((OperationCanceledException)updateFailure!).CancellationToken.ShouldBe(failure.CancellationToken);
        ((OperationCanceledException)disposalFailure!).CancellationToken.ShouldBe(failure.CancellationToken);
    }

    [Fact]
    public async Task IgnoreOnlyMatchingOwnedAbortInTheCleanupOutcome()
    {
        var entered = Signal();
        var owner = Owner();
        var updating = owner.MutateAsync(async token => { entered.TrySetResult(); await Task.Delay(Timeout.InfiniteTimeSpan, token); }, null, CancellationToken.None);
        await entered.Task;
        await owner.DisposeAsync();
        await Should.ThrowAsync<OperationCanceledException>(() => updating);
    }

    [Fact]
    public async Task ExpireNativeReentryGuardForAnEscapedExecutionContextAfterTheSdkExits()
    {
        var release = Signal();
        var owner = Owner();
        Task descendant = Task.CompletedTask;
        await owner.MutateAsync(_ =>
        {
#pragma warning disable RS0030 // Deliberately capture an active native scope into an escaped descendant to test its expiration.
            descendant = Task.Run(async () => { await release.Task; await owner.StopAsync(CancellationToken.None); });
#pragma warning restore RS0030
            return Task.CompletedTask;
        }, null, CancellationToken.None);
        release.TrySetResult();
        await descendant.WaitAsync(TimeSpan.FromSeconds(5));
        await owner.DisposeAsync();
    }

    [Fact]
    public async Task JoinCompletionWithoutRecancelingAnAlreadyDisposedWorkTokenSource()
    {
        var cleaned = Signal(); var release = Signal();
        var owner = new HealthElectionLifetime(_ => Task.CompletedTask, _ => Task.CompletedTask,
            () => ValueTask.CompletedTask, _ => Task.CompletedTask,
            afterWorkCleanup: async () => { cleaned.TrySetResult(); await release.Task; });
        var updating = owner.MutateAsync(_ => Task.CompletedTask, null, CancellationToken.None);
        await cleaned.Task;
        var stopping = owner.StopAsync(CancellationToken.None);
        stopping.IsCompleted.ShouldBeFalse();
        release.TrySetResult(); await updating; await stopping.WaitAsync(TimeSpan.FromSeconds(5));
        await owner.DisposeAsync();
    }

    [Fact]
    public async Task CoalesceStartsAndRetainShutdownWhenAnyAdmittedWaiterCancels()
    {
        var reply = Signal(); var attempts = 0;
        var owner = Owner(start: async token => { Interlocked.Increment(ref attempts); await reply.Task; token.ThrowIfCancellationRequested(); });
        var first = owner.StartAsync(CancellationToken.None);
        using var waiter = new CancellationTokenSource();
        var second = owner.StartAsync(waiter.Token);
        waiter.Cancel(); await Should.ThrowAsync<OperationCanceledException>(() => second);
        attempts.ShouldBe(1); first.IsCompleted.ShouldBeFalse();
        reply.TrySetResult(); await Should.ThrowAsync<OperationCanceledException>(() => first);
        await owner.StopAsync(CancellationToken.None);
        await owner.StartAsync(CancellationToken.None); attempts.ShouldBe(2);
        await owner.DisposeAsync();
    }

    [Fact]
    public async Task PermitAnActiveOwnerSdkToStopAnIndependentOwner()
    {
        var first = Owner(); var second = Owner();
        await first.MutateAsync(_ => second.StopAsync(CancellationToken.None), null, CancellationToken.None);
        await first.DisposeAsync(); await second.DisposeAsync();
    }

    [Fact]
    public async Task RejectStoppingAnActiveAncestorOwnerThroughAnotherOwnersSdk()
    {
        var first = Owner(); var second = Owner();
        await first.MutateAsync(_ => second.MutateAsync(async _ =>
        {
            await Should.ThrowAsync<InvalidOperationException>(() => first.StopAsync(CancellationToken.None));
        }, null, CancellationToken.None), null, CancellationToken.None);
        await first.DisposeAsync(); await second.DisposeAsync();
    }

    [Fact]
    public async Task RefuseScopeRegistrationOnAFinishedCapturedStartWorkAndExpireTheLocalScope()
    {
        var afterStart = Signal(); var afterNative = Signal();
        Task descendant = Task.CompletedTask; Task later = Task.CompletedTask;
        HealthElectionLifetime owner = null!;
        owner = Owner(start: _ =>
        {
#pragma warning disable RS0030 // Deliberately capture finished Start execution context and then a native descendant.
            descendant = Task.Run(async () =>
            {
                await afterStart.Task;
                await owner.NativeAsync(_ =>
                {
                    later = Task.Run(async () => { await afterNative.Task; await owner.StopAsync(CancellationToken.None); });
                    return Task.CompletedTask;
                }, CancellationToken.None);
                afterNative.TrySetResult();
                await later;
            });
#pragma warning restore RS0030
            return Task.CompletedTask;
        });
        await owner.StartAsync(CancellationToken.None);
        afterStart.TrySetResult();
        await descendant.WaitAsync(TimeSpan.FromSeconds(5));
        await owner.DisposeAsync();
    }

    private static TaskCompletionSource Signal() => new(TaskCreationOptions.RunContinuationsAsynchronously);
    private static HealthElectionLifetime Owner(Func<CancellationToken, Task>? start = null,
        Func<CancellationToken, Task>? stop = null, Func<ValueTask>? dispose = null,
        Func<CancellationToken, Task>? remove = null) =>
        new(start ?? (_ => Task.CompletedTask), stop ?? (_ => Task.CompletedTask),
            dispose ?? (() => ValueTask.CompletedTask), remove ?? (_ => Task.CompletedTask));
}
