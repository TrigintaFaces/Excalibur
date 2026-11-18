// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Collections.Concurrent;
using System.Runtime.ExceptionServices;
using Tests.Shared.Fixtures;
using Excalibur.Dispatch.LeaderElection;
using Excalibur.LeaderElection.Redis;
using FakeItEasy;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Shouldly;
using StackExchange.Redis;
using Xunit;

namespace Excalibur.Integration.Tests.LeaderElection;

[Trait("Category", "Integration")]
[Trait("Component", "LeaderElection")]
[Trait("Pattern", "Regression")]
public sealed class RedisLateOwnerRuntimeShould(RedisContainerFixture fixture) : IClassFixture<RedisContainerFixture>
{
    [Fact]
    public async Task RejectActualOldRenewAndReleaseLuaAfterSameCandidateSuccessorAcquires()
    {
        // The shared fixture owns a disposable test container; no external Redis settings are read.
        var configuration = ConfigurationOptions.Parse(fixture.ConnectionString);
        configuration.ConnectTimeout = 5000;
        configuration.AsyncTimeout = 5000;
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(45));
        using var redis = await ConnectionMultiplexer.ConnectAsync(configuration).WaitAsync(deadline.Token);
        var database = redis.GetDatabase();
        var key = (RedisKey)("excalibur:tests:late-owner:" + Guid.NewGuid().ToString("N"));
        var display = "same-public-id";
        var options = Options.Create(new LeaderElectionOptions { InstanceId = display });
        var time = new ScheduledRuntimeClock();
        var releaseOldRenew = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var oldRenewEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var oldRenewResult = new TaskCompletionSource<long>(TaskCreationOptions.RunContinuationsAsynchronously);
        var oldReleaseResult = new TaskCompletionSource<long>(TaskCreationOptions.RunContinuationsAsynchronously);
        var oldLost = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var ownedValues = new ConcurrentQueue<RedisValue>();
        var forwardMultiplexer = A.Fake<IConnectionMultiplexer>();
        var forwardDatabase = A.Fake<IDatabase>();
        A.CallTo(() => forwardMultiplexer.GetDatabase(A<int>._, A<object>._)).Returns(forwardDatabase);
        A.CallTo(forwardDatabase).Where(call => call.Method.Name == nameof(IDatabase.StringSetAsync))
            .WithReturnType<Task<bool>>().ReturnsLazily(call =>
            {
                var value = (RedisValue)call.Arguments[1]!;
                ownedValues.Enqueue(value);
                return database.StringSetAsync((RedisKey)call.Arguments[0]!, value,
                    options.Value.LeaseDuration, When.NotExists);
            });
        A.CallTo(() => forwardDatabase.StringGetAsync(A<RedisKey>._, A<CommandFlags>._))
            .ReturnsLazily((RedisKey requested, CommandFlags flags) => database.StringGetAsync(requested, flags));
        A.CallTo(forwardDatabase).Where(call => call.Method.Name == nameof(IDatabase.ScriptEvaluateAsync))
            .WithReturnType<Task<RedisResult>>().ReturnsLazily(async call =>
            {
                // These are the ACTUAL scripts/arguments produced by the candidate, not copies
                // of intended Lua. Delaying forwarding models an in-flight old client request.
                var script = (string)call.Arguments[0]!;
                var keys = ((RedisKey[])call.Arguments[1]!).ToArray();
                var values = ((RedisValue[])call.Arguments[2]!).ToArray();
                if (script.Contains("'pexpire'", StringComparison.Ordinal))
                {
                    oldRenewEntered.TrySetResult();
                    await releaseOldRenew.Task;
                    var result = await database.ScriptEvaluateAsync(script, keys, values);
                    oldRenewResult.TrySetResult((long)result);
                    return result;
                }
                var released = await database.ScriptEvaluateAsync(script, keys, values);
                oldReleaseResult.TrySetResult((long)released);
                return released;
            });
        var old = new RedisLeaderElection(forwardMultiplexer, key.ToString(), options,
            NullLogger<RedisLeaderElection>.Instance, timeProvider: time);
        var successor = new RedisLeaderElection(redis, key.ToString(), options,
            NullLogger<RedisLeaderElection>.Instance);
        old.LostLeadership += (_, _) => oldLost.TrySetResult();
        var failures = new List<Exception>();
        try
        {
            await old.StartAsync(deadline.Token);
            old.IsLeader.ShouldBeTrue();
            await time.WaitForFirstRenewalDelayAsync(deadline.Token);
            time.Advance(options.Value.RenewInterval);
            await oldRenewEntered.Task.WaitAsync(deadline.Token);
            // Force the old server lease to expire, then acquire through the REAL second adapter.
            // This is a fault injection at the expiry boundary, not an assertion about TTL duration.
            await database.KeyExpireAsync(key, TimeSpan.FromMilliseconds(1));
            while (await database.KeyExistsAsync(key))
            {
                await Task.Delay(10, deadline.Token); // delay-ok: Poll actual Redis key disappearance after injected server expiry before the real successor acquires.
            }
            await successor.StartAsync(deadline.Token);
            successor.IsLeader.ShouldBeTrue();
            var successorValue = await database.StringGetAsync(key);
            successorValue.ShouldNotBe(ownedValues.Single());
            time.Advance(TimeSpan.FromSeconds(10));
            await oldLost.Task.WaitAsync(deadline.Token); // independent old local deadline, before its reply.
            releaseOldRenew.TrySetResult();
            (await oldRenewResult.Task.WaitAsync(deadline.Token)).ShouldBe(0);
            await old.StopAsync(deadline.Token);
            (await oldReleaseResult.Task.WaitAsync(deadline.Token)).ShouldBe(0);
            (await database.StringGetAsync(key)).ShouldBe(successorValue);
            old.IsLeader.ShouldBeFalse();
            successor.IsLeader.ShouldBeTrue();
        }
        catch (Exception error) { failures.Add(error); }
        finally
        {
            releaseOldRenew.TrySetResult();
            await CaptureCleanupAsync(() => old.DisposeAsync().AsTask(), failures);
            await CaptureCleanupAsync(() => successor.DisposeAsync().AsTask(), failures);
            await CaptureCleanupAsync(async () => { _ = await database.KeyDeleteAsync(key); }, failures);
        }
        if (failures.Count == 1) { ExceptionDispatchInfo.Capture(failures[0]).Throw(); }
        if (failures.Count > 1) { throw new AggregateException(failures); }
    }

    private static async Task CaptureCleanupAsync(Func<Task> cleanup, List<Exception> failures)
    {
        try { await cleanup(); }
        catch (Exception error) { failures.Add(error); }
    }

    private sealed class ScheduledRuntimeClock : FakeTimeProvider
    {
        private readonly TaskCompletionSource _delayReady = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal Task WaitForFirstRenewalDelayAsync(CancellationToken token) => _delayReady.Task.WaitAsync(token);
        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        {
            var timer = base.CreateTimer(callback, state, dueTime, period);
            if (dueTime == TimeSpan.FromSeconds(5)) { _delayReady.TrySetResult(); }
            return timer;
        }
    }

}
