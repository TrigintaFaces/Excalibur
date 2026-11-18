// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Runtime.CompilerServices;
using Excalibur.LeaderElection.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

namespace Excalibur.LeaderElection.Tests.Diagnostics;

[Trait("Category", "Unit")]
[Trait("Component", "LeaderElection")]
[Trait("Pattern", "Regression")]
public sealed class TelemetryInstrumentationLifetimeShould
{
    [Theory]
    [InlineData("leader_election.start", "sampling")]
    [InlineData("leader_election.start", "started")]
    [InlineData("leader_election.start", "stopped")]
    [InlineData("leader_election.stop", "sampling")]
    [InlineData("leader_election.stop", "started")]
    [InlineData("leader_election.stop", "stopped")]
    public async Task AttemptTheCoreOperationDespiteAThrowingActivityListener(string operation, string phase)
    {
        using var meter = new Meter("Excalibur.LeaderElection.Listener." + Guid.NewGuid().ToString("N"));
        using var source = new ActivitySource("Excalibur.LeaderElection.Listener." + Guid.NewGuid().ToString("N"));
        var failure = new InvalidOperationException("foreign listener failure");
        await using var fixture = new Harness(meter, source) { ExpectedNotificationFailure = failure };
        using var listener = ThrowingListener(source, operation, phase, failure);
        ActivitySource.AddActivityListener(listener);
        var previous = Activity.Current;
        await fixture.Sut.StartAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(5));
        fixture.Inner.IsLeader.ShouldBeTrue("a sampling/started/stopped listener cannot suppress actual acquisition");
        await fixture.Sut.StopAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(5));
        fixture.Inner.IsLeader.ShouldBeFalse("optional tracing must never skip the core stop operation");
        Activity.Current.ShouldBeSameAs(previous);
        await fixture.Sut.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5));
        var terminal = await Record.ExceptionAsync(() => fixture.Sut.NotificationsCompleted.WaitAsync(TimeSpan.FromSeconds(5)));
        terminal.ShouldBeSameAs(failure);
    }

    [Fact]
    public async Task PreserveASameSourceAmbientParentWhenChildSamplingThrows()
    {
        using var meter = new Meter("Excalibur.LeaderElection.Ambient." + Guid.NewGuid().ToString("N"));
        using var source = new ActivitySource("Excalibur.LeaderElection.Ambient." + Guid.NewGuid().ToString("N"));
        var failure = new InvalidOperationException("child sampling failed");
        await using var fixture = new Harness(meter, source) { ExpectedNotificationFailure = failure };
        using var listener = ThrowingListener(source, "leader_election.start", "sampling", failure);
        ActivitySource.AddActivityListener(listener);
        var parentStops = 0;
        listener.ActivityStopped = activity =>
        {
            if (activity.OperationName == "caller.parent") { Interlocked.Increment(ref parentStops); }
        };
        using var parent = source.StartActivity("caller.parent");
        parent.ShouldNotBeNull();
        await fixture.Sut.StartAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(5));
        fixture.Inner.IsLeader.ShouldBeTrue("child sampling failure cannot suppress core acquisition");
        Volatile.Read(ref parentStops).ShouldBe(0, "the caller's ambient parent is never our recovery Activity");
        parent.IsStopped.ShouldBeFalse();
        Activity.Current.ShouldBeSameAs(parent);
        await fixture.Sut.StopAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(5));
        Activity.Current.ShouldBeSameAs(parent);
        parent.IsStopped.ShouldBeFalse();
        await fixture.Sut.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5));
        var terminal = await Record.ExceptionAsync(() => fixture.Sut.NotificationsCompleted.WaitAsync(TimeSpan.FromSeconds(5)));
        terminal.ShouldBeSameAs(failure);
        Volatile.Read(ref parentStops).ShouldBe(0);
    }

    [Fact]
    public async Task ForwardActualAcquisitionWhenTheTelemetryClockThrows()
    {
        using var meter = new Meter("Excalibur.LeaderElection.Clock." + Guid.NewGuid().ToString("N"));
        using var source = new ActivitySource("Excalibur.LeaderElection.Clock." + Guid.NewGuid().ToString("N"));
        var failure = new InvalidOperationException("telemetry timestamp failed");
        await using var fixture = new Harness(meter, source, telemetryClock: new ThrowingTimestampClock(failure))
        {
            ExpectedNotificationFailure = failure,
        };
        var acquired = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.Sut.BecameLeader += (_, _) => acquired.TrySetResult();
        await fixture.Sut.StartAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(5));
        await acquired.Task.WaitAsync(TimeSpan.FromSeconds(5));
        fixture.Inner.IsLeader.ShouldBeTrue();
        await fixture.Sut.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5));
        var terminal = await Record.ExceptionAsync(() => fixture.Sut.NotificationsCompleted.WaitAsync(TimeSpan.FromSeconds(5)));
        terminal.ShouldBeSameAs(failure);
    }

    [Fact]
    public async Task PreserveCoreFailureWhenAmbientRestorationThrows()
    {
        using var meter = new Meter("Excalibur.LeaderElection.Current." + Guid.NewGuid().ToString("N"));
        using var source = new ActivitySource("Excalibur.LeaderElection.Current." + Guid.NewGuid().ToString("N"));
        var coreFailure = new IOException("release failed");
        var listenerFailure = new InvalidOperationException("CurrentChanged failed");
        await using var fixture = new Harness(meter, source, coreFailure)
        {
            ExpectedResourceFailure = coreFailure,
            ExpectedNotificationFailure = listenerFailure,
        };
        using var listener = ThrowingListener(source, "never", "sampling", listenerFailure);
        ActivitySource.AddActivityListener(listener);
        using var parent = source.StartActivity("caller.parent");
        parent.ShouldNotBeNull();
        await fixture.Sut.StartAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(5));
        var restorationCalls = 0;
        void OnChanged(object? sender, ActivityChangedEventArgs args)
        {
            if (args.Previous?.OperationName == "leader_election.stop" && ReferenceEquals(args.Current, parent))
            {
                Interlocked.Increment(ref restorationCalls);
                throw listenerFailure;
            }
        }
        Activity.CurrentChanged += OnChanged;
        try
        {
            var observed = await Record.ExceptionAsync(() => fixture.Sut.StopAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(5)));
            observed.ShouldBeSameAs(coreFailure);
            Volatile.Read(ref restorationCalls).ShouldBeGreaterThan(0);
            fixture.Inner.IsLeader.ShouldBeFalse();
            parent.IsStopped.ShouldBeFalse();
            Activity.Current.ShouldBeSameAs(parent);
        }
        finally { Activity.CurrentChanged -= OnChanged; }
        var resource = await Record.ExceptionAsync(() => fixture.Sut.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5)));
        resource.ShouldBeSameAs(coreFailure);
        var terminal = await Record.ExceptionAsync(() => fixture.Sut.NotificationsCompleted.WaitAsync(TimeSpan.FromSeconds(5)));
        terminal.ShouldBeSameAs(listenerFailure);
    }

    private sealed class ThrowingTimestampClock(Exception failure) : TimeProvider
    {
        public override long GetTimestamp() => throw failure;
    }

    [Fact]
    public async Task PreserveTheCoreStopFailureWhenActivityStoppedAlsoThrows()
    {
        using var meter = new Meter("Excalibur.LeaderElection.CoreFault." + Guid.NewGuid().ToString("N"));
        using var source = new ActivitySource("Excalibur.LeaderElection.CoreFault." + Guid.NewGuid().ToString("N"));
        var coreFailure = new IOException("conditional release failed");
        var listenerFailure = new InvalidOperationException("ActivityStopped failed");
        await using var fixture = new Harness(meter, source, coreFailure)
        {
            ExpectedResourceFailure = coreFailure,
            ExpectedNotificationFailure = listenerFailure,
        };
        using var listener = ThrowingListener(source, "leader_election.stop", "stopped", listenerFailure);
        ActivitySource.AddActivityListener(listener);
        await fixture.Sut.StartAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(5));
        var observed = await Record.ExceptionAsync(() => fixture.Sut.StopAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(5)));
        observed.ShouldBeSameAs(coreFailure, "an Activity disposal failure must not replace the core failure");
        fixture.Inner.IsLeader.ShouldBeFalse();
        var resource = await Record.ExceptionAsync(() => fixture.Sut.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5)));
        resource.ShouldBeSameAs(coreFailure);
        var terminal = await Record.ExceptionAsync(() => fixture.Sut.NotificationsCompleted.WaitAsync(TimeSpan.FromSeconds(5)));
        terminal.ShouldBeSameAs(listenerFailure, "instrumentation failure has its own truthful completion outcome");
    }

    [Fact]
    public async Task RemoveBorrowedMeterRegistrationsForEveryTerminalWrapperWithoutLosingPendingOnes()
    {
        using var meter = new Meter("Excalibur.LeaderElection.Gauge." + Guid.NewGuid().ToString("N"));
        using var source = new ActivitySource("Excalibur.LeaderElection.Gauge." + Guid.NewGuid().ToString("N"));
        var gauges = 0;
        using var listener = new MeterListener
        {
            InstrumentPublished = (instrument, _) =>
            {
                if (ReferenceEquals(instrument.Meter, meter) && instrument is ObservableGauge<int>) { Interlocked.Increment(ref gauges); }
            },
        };
        listener.Start();
        var weak = new List<WeakReference>();
        for (var index = 0; index < 20; index++)
        {
            weak.Add(CreateAndDisposeChild(meter, source));
            LeaderElectionGaugeRegistry.RegistrationCount(meter).ShouldBe(0);
        }
        Volatile.Read(ref gauges).ShouldBe(1, "one meter owns one gauge, with removable live-child registrations");
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        weak.ShouldAllBe(reference => !reference.IsAlive,
            "a surviving borrowed meter must not root terminal wrappers and their inner SDK clients");

        await using var held = new Harness(meter, source);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        held.Sut.BecameLeader += (_, _) =>
        {
            entered.TrySetResult();
#pragma warning disable RS0030 // Hold synchronous delivery while resource disposal finishes; finally releases it.
            release.Task.GetAwaiter().GetResult();
#pragma warning restore RS0030
        };
        try
        {
            await held.Sut.StartAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(5));
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            await held.Sut.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5));
            held.Sut.NotificationsCompleted.IsCompleted.ShouldBeFalse();
            LeaderElectionGaugeRegistry.RegistrationCount(meter).ShouldBe(1,
                "pending notifications retain the strong registration until their real terminal boundary");
        }
        finally
        {
            release.TrySetResult();
            await held.Sut.NotificationsCompleted.WaitAsync(TimeSpan.FromSeconds(5));
        }
        LeaderElectionGaugeRegistry.RegistrationCount(meter).ShouldBe(0);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference CreateAndDisposeChild(Meter meter, ActivitySource source)
    {
        var fixture = new Harness(meter, source);
#pragma warning disable RS0030 // SOS showed the async helper's completion frame retaining Harness during the caller's GC. This bounded, non-inlined scope must return first.
        try
        {
            fixture.Sut.StartAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(5)).GetAwaiter().GetResult();
            LeaderElectionGaugeRegistry.RegistrationCount(meter).ShouldBe(1);
            fixture.Sut.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5)).GetAwaiter().GetResult();
            fixture.Sut.NotificationsCompleted.WaitAsync(TimeSpan.FromSeconds(5)).GetAwaiter().GetResult();
            return new WeakReference(fixture.Sut);
        }
        finally
        {
            fixture.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5)).GetAwaiter().GetResult();
        }
#pragma warning restore RS0030
    }

    [Fact]
    public async Task RetainOwnedInstrumentsUntilTheLastAdmittedProxyActivityReturns()
    {
        using var meter = new Meter("Excalibur.LeaderElection.ProxyLease." + Guid.NewGuid().ToString("N"));
        using var source = new ActivitySource("Excalibur.LeaderElection.ProxyLease." + Guid.NewGuid().ToString("N"));
        await using var inner = Harness.CreateInner();
        var nativeFactory = A.Fake<ILeaderElectionFactory>();
        A.CallTo(() => nativeFactory.CreateElection(A<string>._, A<string?>._)).Returns(inner);
        using var factory = new TelemetryLeaderElectionFactory(nativeFactory, meter, source, "Redis");
        var child = factory.CreateElection("proxy-lease", "candidate");
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var listener = new ActivityListener
        {
            ShouldListenTo = candidate => ReferenceEquals(candidate, source),
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
            SampleUsingParentId = (ref ActivityCreationOptions<string> _) => ActivitySamplingResult.AllData,
            ActivityStopped = activity =>
            {
                if (activity.OperationName != "leader_election.stop") { return; }
                entered.TrySetResult();
#pragma warning disable RS0030 // Hold the real ActivityStopped callback after core Stop, before the proxy releases its operation lease.
                release.Task.GetAwaiter().GetResult();
#pragma warning restore RS0030
            },
        };
        ActivitySource.AddActivityListener(listener);
        Task stop = Task.CompletedTask;
        try
        {
            await child.StartAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(5));
            stop = Task.Run(() => child.StopAsync(CancellationToken.None));
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            inner.IsLeader.ShouldBeFalse();
            await child.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5));
            await inner.NotificationsCompleted.WaitAsync(TimeSpan.FromSeconds(5));
            factory.Dispose();
            child.NotificationsCompleted.IsCompleted.ShouldBeFalse();
            factory.InstrumentationCompleted.IsCompleted.ShouldBeFalse();
            source.HasListeners().ShouldBeTrue("the admitted proxy ActivityStopped scope still owns an instrument lease");
        }
        finally
        {
            release.TrySetResult();
            await stop.WaitAsync(TimeSpan.FromSeconds(5));
            await child.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5));
            await child.NotificationsCompleted.WaitAsync(TimeSpan.FromSeconds(5));
            factory.Dispose();
            await factory.InstrumentationCompleted.WaitAsync(TimeSpan.FromSeconds(5));
        }
        source.HasListeners().ShouldBeFalse();
    }

    private static ActivityListener ThrowingListener(ActivitySource source, string operation, string phase, Exception failure) => new()
    {
        ShouldListenTo = candidate => ReferenceEquals(candidate, source),
        Sample = (ref ActivityCreationOptions<ActivityContext> options) =>
        {
            if (options.Name == operation && phase == "sampling") { throw failure; }
            return ActivitySamplingResult.AllData;
        },
        SampleUsingParentId = (ref ActivityCreationOptions<string> options) =>
        {
            if (options.Name == operation && phase == "sampling") { throw failure; }
            return ActivitySamplingResult.AllData;
        },
        ActivityStarted = activity => { if (activity.OperationName == operation && phase == "started") { throw failure; } },
        ActivityStopped = activity => { if (activity.OperationName == operation && phase == "stopped") { throw failure; } },
    };

    private sealed class Harness : IAsyncDisposable
    {
        internal RedisLeaderElection Inner { get; }
        internal TelemetryLeaderElection Sut { get; }
        internal Exception? ExpectedResourceFailure { get; init; }
        internal Exception? ExpectedNotificationFailure { get; init; }

        internal Harness(Meter meter, ActivitySource source, Exception? releaseFailure = null, TimeProvider? telemetryClock = null)
        {
            Inner = CreateInner(releaseFailure);
            Sut = new TelemetryLeaderElection(Inner, meter, source, "Redis", telemetryClock);
        }

        internal static RedisLeaderElection CreateInner(Exception? releaseFailure = null)
        {
            var redis = A.Fake<IConnectionMultiplexer>();
            var database = A.Fake<IDatabase>();
            A.CallTo(() => redis.GetDatabase(A<int>._, A<object>._)).Returns(database);
            A.CallTo(database).Where(call => call.Method.Name == nameof(IDatabase.StringSetAsync))
                .WithReturnType<Task<bool>>().Returns(Task.FromResult(true));
            A.CallTo(database).Where(call => call.Method.Name == nameof(IDatabase.ScriptEvaluateAsync))
                .WithReturnType<Task<RedisResult>>().ReturnsLazily(call =>
                {
                    if (releaseFailure is not null && call.Arguments[0] is string script && script.Contains("'del'", StringComparison.Ordinal))
                    {
                        return Task.FromException<RedisResult>(releaseFailure);
                    }
                    return Task.FromResult(RedisResult.Create((RedisValue)1));
                });
            var clock = new FakeTimeProvider();
            return new RedisLeaderElection(redis, "instrumentation-control",
                Options.Create(new LeaderElectionOptions { InstanceId = "candidate" }),
                NullLogger<RedisLeaderElection>.Instance, timeProvider: clock);
        }

        public async ValueTask DisposeAsync()
        {
            var resource = await Record.ExceptionAsync(() => Sut.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(10)));
            resource.ShouldBeSameAs(ExpectedResourceFailure);
            var terminal = await Record.ExceptionAsync(() => Sut.NotificationsCompleted.WaitAsync(TimeSpan.FromSeconds(10)));
            terminal.ShouldBeSameAs(ExpectedNotificationFailure);
        }
    }
}
