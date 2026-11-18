// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.LeaderElection.Tests.Redis;

/// <summary>
/// Consumer loss callbacks probe the real CurrentLeaderId getter from a dedicated other thread.
/// Resource Stop/Dispose and queued callback delivery are separate lifetimes; both must be joined.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
[Trait("Pattern", "Regression")]
public sealed class RedisLeaderElectionReentrancyShould : UnitTestBase
{
    private static (RedisLeaderElection Sut, Func<int> ReleaseCount) CreateAcquiringElection()
    {
        var multiplexer = A.Fake<IConnectionMultiplexer>();
        var database = A.Fake<IDatabase>();
        A.CallTo(() => multiplexer.GetDatabase(A<int>._, A<object>._)).Returns(database);
        A.CallTo(database).Where(call => call.Method.Name == nameof(IDatabase.StringSetAsync))
            .WithReturnType<Task<bool>>().Returns(true);
        var releases = 0;
        // The typed SDK returns a non-null INTEGER RedisResult for successful conditional release.
        // This fixture is a success-path callback test, not independent proof of Redis CAS behavior.
        A.CallTo(() => database.ScriptEvaluateAsync(A<string>._, A<RedisKey[]>._, A<RedisValue[]>._, A<CommandFlags>._))
            .ReturnsLazily(call =>
            {
                if (call.GetArgument<string>(0) is { } script && script.Contains("redis.call('del'", StringComparison.Ordinal)) { Interlocked.Increment(ref releases); }
                return Task.FromResult(RedisResult.Create((RedisValue)1));
            });
        var sut = new RedisLeaderElection(multiplexer, "reentrancy-test-key",
            Options.Create(new LeaderElectionOptions()), NullLogger<RedisLeaderElection>.Instance);
        return (sut, () => Volatile.Read(ref releases));
    }

    [Fact]
    public Task RaiseLostLeadershipOutsideLock_NoReentrantDeadlock_OnStop() => VerifyAsync(dispose: false);

    [Fact]
    public Task RaiseLostLeadershipOutsideLock_NoReentrantDeadlock_OnDispose() => VerifyAsync(dispose: true);

    private static async Task VerifyAsync(bool dispose)
    {
        var (sut, releaseCount) = CreateAcquiringElection();
        var probe = new CrossThreadGetterProbe(sut);
        try
        {
            await sut.StartAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(5));
            sut.IsLeader.ShouldBeTrue("positive precondition: fake Redis SET actually acquired");
            sut.CurrentLeadership.ShouldNotBeNull();
            sut.LostLeadership += probe.OnLoss;
            if (dispose) { await sut.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5)); }
            else { await sut.StopAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(5)); }
            // Neither resource operation promises the queued callback already ran.
            await probe.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            await probe.Completed.Task.WaitAsync(TimeSpan.FromSeconds(5));
        }
        finally
        {
            Exception? shutdownFailure = null;
            var resources = DisposeResourcesAsync(sut);
            var terminal = sut.NotificationsCompleted;
            try { await Task.WhenAll(resources, terminal); }
            catch (Exception error) { shutdownFailure = error; }
            try { probe.Dispose(); }
            catch (Exception error)
            { shutdownFailure = shutdownFailure is null ? error : new AggregateException(shutdownFailure, error); }
            if (shutdownFailure is not null) { System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(shutdownFailure).Throw(); }
        }
        probe.Count.ShouldBe(1, "a default false flag without an actual loss callback cannot prove reentrancy");
        probe.GetterFailure.ShouldBeNull();
        probe.LockHeldDuringHandler.ShouldBeFalse("the dedicated getter probe must run before the loss handler returns");
        releaseCount().ShouldBe(1);
        sut.IsLeader.ShouldBeFalse(); sut.CurrentLeadership.ShouldBeNull();
    }

    private static async Task DisposeResourcesAsync(RedisLeaderElection sut) => await sut.DisposeAsync();

    private sealed class CrossThreadGetterProbe(RedisLeaderElection sut) : IDisposable
    {
        private readonly ManualResetEventSlim _probed = new(false);
        private Thread? _thread;
        private int _count;
        internal int Count => Volatile.Read(ref _count);
        internal TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource Completed { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal bool LockHeldDuringHandler { get; private set; }
        internal Exception? GetterFailure { get; private set; }
        internal void OnLoss(object? sender, LeaderElectionEventArgs args)
        {
            if (Interlocked.Increment(ref _count) != 1) { throw new InvalidOperationException("Duplicate loss callback."); }
            Entered.TrySetResult();
            _thread = new Thread(() =>
            {
                try { _ = sut.CurrentLeaderId; }
                catch (Exception error) { GetterFailure = error; }
                finally { _probed.Set(); }
            }) { IsBackground = true };
            try
            {
                // A dedicated thread avoids interpreting thread-pool starvation as a state-lock defect.
                _thread.Start();
#pragma warning disable RS0030 // The synchronous consumer deliberately probes a different thread's real state getter.
                LockHeldDuringHandler = !_probed.Wait(TimeSpan.FromSeconds(2));
#pragma warning restore RS0030
            }
            finally { Completed.TrySetResult(); }
        }
        public void Dispose()
        {
            // Do not dispose the wait handle while a getter thread could still call Set.
#pragma warning disable RS0030 // Join the exact explicitly owned dedicated probe thread before disposing its signal.
            if (_thread is not null && !_thread.Join(TimeSpan.FromSeconds(5))) { throw new TimeoutException("Owned getter probe thread did not finish."); }
#pragma warning restore RS0030
            _probed.Dispose();
        }
    }
}
