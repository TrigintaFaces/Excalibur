// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0
using System.Net;
using Excalibur.Dispatch.LeaderElection;
using Excalibur.LeaderElection.Diagnostics;
using Excalibur.LeaderElection.Kubernetes;
using k8s;
using k8s.Models;
using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.LeaderElection.Tests.Kubernetes;

[Trait("Category", "Unit")]
[Trait("Component", "LeaderElection")]
[Trait("Pattern", "Regression")]
public sealed class KubernetesBoundedCallerShould
{
    // A BACKSTOP against a hang, not a budget for how long the work may take. At five seconds it
    // fired: PreserveTheIndependentCallerCancellationOrDeadlineWinner failed 1 in 5 filtered runs
    // locally and 2 of 6 on a two-core Linux container, surfacing as a bare TimeoutException whose
    // stack frames are the WaitAsync lines themselves -- duration-equals-cap is the tell that the
    // bound expired rather than anything being cancelled. The Clock.Advance calls below are NOT
    // this: they drive fake logical time and changing them would change what the arms assert.
    private static TimeSpan Backstop => TimeSpan.FromSeconds(30);

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task PreserveTheIndependentCallerCancellationOrDeadlineWinner(bool cancelFirst)
    {
        var fixture = new KubeTenureFixture(); await fixture.StartAsync();
        var reply = new TaskCompletionSource<V1Lease>(TaskCreationOptions.RunContinuationsAsynchronously);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var caller = new CancellationTokenSource();
        try
        {
            fixture.Read = _ => { entered.TrySetResult(); return reply.Task; };
            fixture.Clock.Advance(TimeSpan.FromSeconds(5)); await entered.Task.WaitAsync(Backstop);
            var count = fixture.Calls;
            var stopping = fixture.Sut.StopAsync(caller.Token);
            if (cancelFirst)
            {
                caller.Cancel();
                await TerminalAsync(stopping);
                var error = await Should.ThrowAsync<OperationCanceledException>(() => stopping);
                error.CancellationToken.ShouldBe(caller.Token);
                fixture.Clock.Advance(TimeSpan.FromSeconds(10));
            }
            else
            {
                fixture.Clock.Advance(TimeSpan.FromSeconds(10));
                await TerminalAsync(stopping);
                await Should.ThrowAsync<TimeoutException>(() => stopping);
                caller.Cancel();
                await Should.ThrowAsync<TimeoutException>(() => stopping);
            }
            fixture.Sut.IsLeader.ShouldBeFalse(); fixture.Calls.ShouldBe(count);
            var disposing = fixture.Sut.DisposeAsync().AsTask(); disposing.IsCompleted.ShouldBeFalse();
            fixture.Read = _ => Task.FromResult(fixture.Snapshot()); reply.TrySetResult(fixture.Snapshot());
            await disposing.WaitAsync(Backstop);
        }
        finally
        {
            var cleanup = fixture.Sut.DisposeAsync().AsTask();
            fixture.Read = _ => Task.FromResult(fixture.Snapshot()); reply.TrySetResult(fixture.Snapshot());
            await cleanup.WaitAsync(Backstop); await fixture.DisposeAsync();
        }
    }

    [Fact]
    public async Task KeepOneTotalStartBudgetAcrossSettledRetriesAndOneUnknownRequest()
    {
        var fixture = new KubeTenureFixture();
        var provider = fixture.CreateProvider(maxRetries: 3);
        var reply = new TaskCompletionSource<V1Lease>(TaskCreationOptions.RunContinuationsAsynchronously);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var reads = 0;
        try
        {
            fixture.Clock.Advance(TimeSpan.FromDays(30));
            fixture.Read = _ =>
            {
                if (Interlocked.Increment(ref reads) <= 2)
                {
                    fixture.Clock.Advance(TimeSpan.FromSeconds(4));
                    return Task.FromException<V1Lease>(KubeTenureFixture.Status(HttpStatusCode.ServiceUnavailable));
                }
                entered.TrySetResult(); return reply.Task;
            };
            var starting = provider.StartAsync(CancellationToken.None);
            await entered.Task.WaitAsync(Backstop);
            reads.ShouldBe(3); fixture.Clock.Advance(TimeSpan.FromSeconds(2));
            await TerminalAsync(starting); await Should.ThrowAsync<TimeoutException>(() => starting);
            fixture.Clock.Advance(TimeSpan.FromSeconds(30));
            reads.ShouldBe(3); provider.IsLeader.ShouldBeFalse(); fixture.OwnershipWrites.ShouldBe(0);
            var disposing = provider.DisposeAsync().AsTask(); disposing.IsCompleted.ShouldBeFalse();
            fixture.Read = _ => Task.FromResult(fixture.Snapshot()); reply.TrySetResult(fixture.Snapshot());
            await disposing.WaitAsync(Backstop); fixture.OwnershipWrites.ShouldBe(0);
        }
        finally
        {
            var cleanup = provider.DisposeAsync().AsTask();
            fixture.Read = _ => Task.FromResult(fixture.Snapshot()); reply.TrySetResult(fixture.Snapshot());
            await cleanup.WaitAsync(Backstop); await provider.NotificationsCompleted.WaitAsync(Backstop);
            await fixture.DisposeAsync();
        }
    }

    [Fact]
    public async Task PropagateTimeoutThroughThePublicRegistrationAndTelemetryWhileRetainingDisposal()
    {
        var fixture = new KubeTenureFixture();
        var services = new ServiceCollection(); services.AddLogging();
        services.AddExcalibur(builder => builder.AddLeaderElection(election => election.UseKubernetes(kubernetes =>
            kubernetes.Namespace("test").LeaseName("orders").LeaseIdentity("candidate"))));
        services.Configure<KubernetesLeaderElectionOptions>(options =>
        {
            options.LeaseDuration = TimeSpan.FromSeconds(15); options.RenewInterval = TimeSpan.FromMilliseconds(500);
            options.GracePeriod = TimeSpan.FromMilliseconds(500); options.RetryInterval = TimeSpan.FromMilliseconds(100);
            options.MaxRetries = 0;
        });
        services.AddSingleton<IKubernetes>(fixture.Client); // The actual public borrowed-client override.
        var container = services.BuildServiceProvider();
        var election = container.GetRequiredService<ILeaderElection>();
        var reply = new TaskCompletionSource<V1Lease>(TaskCreationOptions.RunContinuationsAsynchronously);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        try
        {
            election.ShouldBeOfType<TelemetryLeaderElection>();
            await election.StartAsync(CancellationToken.None).WaitAsync(Backstop);
            fixture.Read = _ => { entered.TrySetResult(); return reply.Task; };
            await entered.Task.WaitAsync(Backstop); // Real System clock: factory has no clock hook.
            var stopping = election.StopAsync(CancellationToken.None);
            await TerminalAsync(stopping); await Should.ThrowAsync<TimeoutException>(() => stopping);
            election.IsLeader.ShouldBeFalse();
            var disposing = election.DisposeAsync().AsTask(); disposing.IsCompleted.ShouldBeFalse();
            fixture.Read = _ => Task.FromResult(fixture.Snapshot()); reply.TrySetResult(fixture.Snapshot());
            await disposing.WaitAsync(Backstop);
            await container.DisposeAsync(); A.CallTo(() => fixture.Client.Dispose()).MustNotHaveHappened();
        }
        finally
        {
            var cleanup = election.DisposeAsync().AsTask();
            fixture.Read = _ => Task.FromResult(fixture.Snapshot()); reply.TrySetResult(fixture.Snapshot());
            await cleanup.WaitAsync(Backstop); await container.DisposeAsync();
            await fixture.DisposeAsync();
        }
    }

    [Fact]
    public async Task RetainAHeldFaultingCancellationCallbackAfterTheStopWaitTimesOut()
    {
        var fixture = new KubeTenureFixture(); await fixture.StartAsync();
        var reply = new TaskCompletionSource<V1Lease>(TaskCreationOptions.RunContinuationsAsynchronously);
        var sdkEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var callbackEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var callbackExited = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var callbackGate = new KubeTenureClock();
        var expected = new IOException("retained actual cancellation callback failure");
        CancellationToken nativeToken = default; CancellationTokenRegistration registration = default; var reads = 0;
        bool ContainsExpected(Exception error) => ReferenceEquals(error, expected) ||
            error is AggregateException aggregate && aggregate.InnerExceptions.Any(ContainsExpected);
        bool OnlyExpected(Exception error) => ReferenceEquals(error, expected) ||
            error is OperationCanceledException canceled && canceled.CancellationToken == nativeToken ||
            error is AggregateException aggregate && aggregate.InnerExceptions.All(OnlyExpected);
        try
        {
            fixture.Read = token =>
            {
                if (Interlocked.Increment(ref reads) != 1) { return Task.FromResult(fixture.Snapshot()); }
                nativeToken = token;
                registration = token.Register(() =>
                {
                    try
                    {
                        // Reuse the existing vetted test clock gate on a SEPARATE clock:
                        // provider/Stop clock reads cannot consume this callback's barrier.
                        callbackGate.ArmTimestampBarrier(1);
                        callbackEntered.TrySetResult();
                        _ = callbackGate.GetTimestamp();
                        throw expected;
                    }
                    finally { callbackExited.TrySetResult(); }
                });
                sdkEntered.TrySetResult(); return reply.Task;
            };
            fixture.Clock.Advance(TimeSpan.FromSeconds(5)); await sdkEntered.Task.WaitAsync(Backstop);
            var stopping = fixture.Sut.StopAsync(CancellationToken.None);
            await callbackEntered.Task.WaitAsync(Backstop);
            await callbackGate.BarrierEntered.Task.WaitAsync(Backstop);
            reply.TrySetCanceled(nativeToken); // SDK reply settles; its actual cancellation callback still does not.
            var disposing = fixture.Sut.DisposeAsync().AsTask();
            fixture.Clock.Advance(TimeSpan.FromSeconds(10));
            await TerminalAsync(stopping); await Should.ThrowAsync<TimeoutException>(() => stopping);
            callbackExited.Task.IsCompleted.ShouldBeFalse(); disposing.IsCompleted.ShouldBeFalse();
            callbackGate.ReleaseBarrier();
            var failure = await Should.ThrowAsync<Exception>(() => disposing.WaitAsync(Backstop));
            ContainsExpected(failure).ShouldBeTrue(); OnlyExpected(failure).ShouldBeTrue();
            fixture.Sut.DisposeAsync().AsTask().ShouldBeSameAs(disposing);
            var repeated = await Should.ThrowAsync<Exception>(() => fixture.Sut.DisposeAsync().AsTask());
            ContainsExpected(repeated).ShouldBeTrue(); OnlyExpected(repeated).ShouldBeTrue();
        }
        finally
        {
            callbackGate.ReleaseBarrier(); reply.TrySetCanceled(nativeToken); fixture.Read = _ => Task.FromResult(fixture.Snapshot());
            await registration.DisposeAsync();
            try { await fixture.DisposeAsync(); }
            catch (Exception error) when (OnlyExpected(error)) { }
        }
    }

    private static async Task TerminalAsync(Task actualCaller)
    {
        var terminal = actualCaller.ContinueWith(static _ => { }, CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
        await terminal.WaitAsync(Backstop);
        actualCaller.IsCompleted.ShouldBeTrue();
    }
}
