// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0
using k8s.Models;
namespace Excalibur.LeaderElection.Tests.Kubernetes;

[Trait("Category", "Unit")]
[Trait("Component", "LeaderElection")]
[Trait("Pattern", "Regression")]
public sealed class KubernetesStoppedLeaderRelinquishesShould
{
    [Fact]
    public async Task AcquireDespiteAHeldStartupDiagnosticButJoinOwnershipHistoryBeforeRestart()
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var armed = 0;
        var held = 0;
        var logger = A.Fake<Microsoft.Extensions.Logging.ILogger<KubernetesLeaderElection>>();
        A.CallTo(() => logger.IsEnabled(A<Microsoft.Extensions.Logging.LogLevel>._)).Returns(true);
        A.CallTo(logger).Where(call => call.Method.Name == "Log").Invokes(call =>
        {
            // Diagnostic coalescing may replace the startup text before the worker reads it.
            // Hold the first runtime diagnostic, not a message whose preservation is not promised.
            if (Volatile.Read(ref armed) == 0 || Interlocked.CompareExchange(ref held, 1, 0) != 0) { return; }
            entered.TrySetResult();
#pragma warning disable RS0030 // Deliberately hold foreign synchronous logging; finally releases it.
            release.Task.GetAwaiter().GetResult();
#pragma warning restore RS0030
        });
        await using var fixture = new KubeTenureFixture();
        var sut = new KubernetesLeaderElection(fixture.Client, "orders", Options.Create(new KubernetesLeaderElectionOptions
        {
            Namespace = "test", LeaseName = "orders", InstanceId = "candidate", LeaseDuration = TimeSpan.FromSeconds(15),
            RenewInterval = TimeSpan.FromSeconds(5), GracePeriod = TimeSpan.FromSeconds(5),
            RetryInterval = TimeSpan.FromHours(1), MaxRetries = 0, MaxRetryDelay = TimeSpan.Zero,
        }), logger, fixture.Clock);
        Task? restart = null;
        var acquisitions = 0;
        sut.BecameLeader += (_, _) => Interlocked.Increment(ref acquisitions);
        Volatile.Write(ref armed, 1);
        try
        {
            var start = Task.Run(() => sut.StartAsync(CancellationToken.None));
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            await start.WaitAsync(TimeSpan.FromSeconds(5));
            var deadline = System.Diagnostics.Stopwatch.StartNew();
            while (!sut.IsLeader && deadline.Elapsed < TimeSpan.FromSeconds(5))
            {
                await Task.Delay(10, CancellationToken.None); // delay-ok: Poll actual leadership/publication/retirement state under the existing stopwatch; elapsed cadence is not ownership evidence.
            }
            sut.IsLeader.ShouldBeTrue("diagnostic delivery is not an ownership-history prerequisite for the first native acquisition");
            Volatile.Read(ref acquisitions).ShouldBe(0, "the actual acquired notification is still queued behind the held logger");
            await sut.StopAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(5));
            restart = Task.Run(() => sut.StartAsync(CancellationToken.None));
            // The helper's deterministic queued/executing ownership barriers prove restart gating;
            // this provider control proves the exact public history after that gate opens.
            sut.NotificationsCompleted.IsCompleted.ShouldBeFalse();
            release.TrySetResult();
            await restart.WaitAsync(TimeSpan.FromSeconds(5));
            deadline.Restart();
            while ((!sut.IsLeader || Volatile.Read(ref acquisitions) != 2) && deadline.Elapsed < TimeSpan.FromSeconds(5))
            {
                await Task.Delay(10, CancellationToken.None); // delay-ok: Poll actual leadership/publication/retirement state under the existing stopwatch; elapsed cadence is not ownership evidence.
            }
            sut.IsLeader.ShouldBeTrue();
            Volatile.Read(ref acquisitions).ShouldBe(2, "exactly the original tenure and one restarted tenure publish acquisition");
            A.CallTo(() => fixture.Api.ReplaceNamespacedLeaseWithHttpMessagesAsync(
                A<V1Lease>._, A<string>._, A<string>._, A<string>._, A<string>._, A<string>._, A<bool?>._,
                A<IReadOnlyDictionary<string, IReadOnlyList<string>>>._, A<CancellationToken>._))
                .MustHaveHappened(3, Times.Exactly);
        }
        finally
        {
            release.TrySetResult();
            if (restart is not null) { await restart.WaitAsync(TimeSpan.FromSeconds(5)); }
            await sut.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5));
            await sut.NotificationsCompleted.WaitAsync(TimeSpan.FromSeconds(5));
        }
    }


    [Fact]
    public async Task DisposeAfterStopWithoutReusingARetiredRunCancellationSource()
    {
        await using var f = new KubeTenureFixture();
        await f.StartAsync();
        await f.Sut.StopAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(5));
        f.Sut.CurrentLeadership.ShouldBeNull();
        await f.Sut.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5));
        await f.Sut.NotificationsCompleted.WaitAsync(TimeSpan.FromSeconds(5));
        f.Acquired.ShouldBe(1); f.Lost.ShouldBe(1);
    }

    [Fact]
    public async Task JoinAnAdmittedHeldStopBeforeDisposingItsRunCancellationSource()
    {
        await using var f = new KubeTenureFixture();
        await f.StartAsync();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var reply = new TaskCompletionSource<V1Lease>(TaskCreationOptions.RunContinuationsAsynchronously);
        V1Lease? pending = null;
        f.Replace = (lease, _) => lease.Spec.HolderIdentity is null ? Hold(lease) : Task.FromResult(f.Persist(lease));
        Task<V1Lease> Hold(V1Lease lease) { pending = lease; entered.TrySetResult(); return reply.Task; }
        try
        {
            var stopping = f.Sut.StopAsync(CancellationToken.None);
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            var disposing = f.Sut.DisposeAsync().AsTask();
            disposing.IsCompleted.ShouldBeFalse();
            reply.TrySetResult(f.Persist(pending!));
            await stopping.WaitAsync(TimeSpan.FromSeconds(5));
            await disposing.WaitAsync(TimeSpan.FromSeconds(5));
            await f.Sut.NotificationsCompleted.WaitAsync(TimeSpan.FromSeconds(5));
            f.Sut.CurrentLeadership.ShouldBeNull();
            f.Lost.ShouldBe(1);
        }
        finally { reply.TrySetResult(f.Snapshot()); }
    }

    [Fact]
    public async Task SynchronousDisposeJoinsTheActualPendingSdkCleanup()
    {
        await using var f = new KubeTenureFixture(); await f.StartAsync();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var reply = new TaskCompletionSource<V1Lease>(TaskCreationOptions.RunContinuationsAsynchronously);
        try
        {
            f.Read = _ => { entered.TrySetResult(); return reply.Task; };
            await f.Clock.WaitForDueAsync(TimeSpan.FromSeconds(5), 1);
            f.Clock.Advance(TimeSpan.FromSeconds(5)); await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            var synchronousCleanup = Task.Run(f.Sut.Dispose);
            var deadline = System.Diagnostics.Stopwatch.StartNew();
            while (f.Sut.IsLeader && deadline.Elapsed < TimeSpan.FromSeconds(5))
            {
                await Task.Delay(10, CancellationToken.None); // delay-ok: Poll actual leadership/publication/retirement state under the existing stopwatch; elapsed cadence is not ownership evidence.
            }
            f.Sut.IsLeader.ShouldBeFalse("synchronous Dispose has admitted permanent retirement");
            synchronousCleanup.IsCompleted.ShouldBeFalse("synchronous Dispose joins the actual held native operation");
            await Should.ThrowAsync<ObjectDisposedException>(() => f.Sut.StartAsync(CancellationToken.None));
            var actualCleanup = f.Sut.DisposeAsync().AsTask();
            actualCleanup.IsCompleted.ShouldBeFalse();
            f.Read = _ => Task.FromResult(f.Snapshot()); reply.TrySetResult(f.Snapshot());
            await synchronousCleanup.WaitAsync(TimeSpan.FromSeconds(5));
            await actualCleanup.WaitAsync(TimeSpan.FromSeconds(5));
            await f.Sut.NotificationsCompleted.WaitAsync(TimeSpan.FromSeconds(5));
            f.Lost.ShouldBe(1);
        }
        finally { f.Read = _ => Task.FromResult(f.Snapshot()); reply.TrySetResult(f.Snapshot()); }
    }

    [Fact]
    public async Task SynchronousDisposePreservesTheOriginalNativeCleanupFailure()
    {
        var f = new KubeTenureFixture(); await f.StartAsync();
        var failure = new IOException("synchronous release failed");
        f.Replace = (lease, _) => lease.Spec.HolderIdentity is null
            ? Task.FromException<V1Lease>(failure) : Task.FromResult(f.Persist(lease));
        var observed = Record.Exception(f.Sut.Dispose);
        observed.ShouldBeSameAs(failure);
        f.Sut.CurrentLeadership.ShouldBeNull();
        var repeated = await Record.ExceptionAsync(() => f.Sut.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5)));
        repeated.ShouldBeSameAs(failure);
        await f.Sut.NotificationsCompleted.WaitAsync(TimeSpan.FromSeconds(5));
        f.Lost.ShouldBe(1);
        f.Clock.Dispose();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PublishExactlyOneLossAcrossStopAndResourceDisposal(bool stopFirst)
    {
        await using var f = new KubeTenureFixture();
        await f.StartAsync();
        if (stopFirst) { await f.Sut.StopAsync(CancellationToken.None); }
        await f.Sut.DisposeAsync();
        await f.Sut.NotificationsCompleted.WaitAsync(TimeSpan.FromSeconds(5));
        f.Acquired.ShouldBe(1); f.Lost.ShouldBe(1);
        f.Sut.IsLeader.ShouldBeFalse(); f.Sut.CurrentLeaderId.ShouldBeNull();
        f.Snapshot().Spec.HolderIdentity.ShouldBeNull();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RetireLocallyAndPreserveTheActualNativeCleanupFailure(bool stopFirst)
    {
        var f = new KubeTenureFixture();
        await f.StartAsync();
        var failure = new IOException("release transport failed");
        f.Replace = (lease, _) => lease.Spec.HolderIdentity is null ? Task.FromException<V1Lease>(failure) : Task.FromResult(f.Persist(lease));
        if (stopFirst)
        {
            var stopped = await Record.ExceptionAsync(() => f.Sut.StopAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(5)));
            stopped.ShouldBeSameAs(failure);
            await Should.ThrowAsync<InvalidOperationException>(() => f.Sut.StartAsync(CancellationToken.None));
        }
        var resource = await Record.ExceptionAsync(() => f.Sut.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5)));
        resource.ShouldBeSameAs(failure);
        var repeated = await Record.ExceptionAsync(() => f.Sut.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5)));
        repeated.ShouldBeSameAs(failure);
        await f.Sut.NotificationsCompleted.WaitAsync(TimeSpan.FromSeconds(5));
        f.Sut.CurrentLeadership.ShouldBeNull(); f.Lost.ShouldBe(1);
        f.Clock.Dispose();
    }
}
