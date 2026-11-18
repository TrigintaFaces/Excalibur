// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Reflection;

using Excalibur.Dispatch.Benchmarks.Comparative;

using MassTransit;

namespace Excalibur.Dispatch.Tests.Benchmarks;

[Trait("Category", "Unit")]
[Trait("Component", "Dispatch.Core")]
[Trait("Pattern", "Behavior")]
public sealed class MassTransitBenchmarkCleanupShould
{
    [Fact]
    public async Task AttemptEveryCleanupAndPreserveEveryOriginalFailure()
    {
        var fixture = new MassTransitComparisonBenchmarks();
        var standard = A.Fake<IServiceProvider>(options => options.Implements<IDisposable>());
        var direct = A.Fake<IServiceProvider>(options => options.Implements<IDisposable>());
        var provider = A.Fake<IServiceProvider>(options => options.Implements<IDisposable>().Implements<IAsyncDisposable>());
        var bus = A.Fake<IBusControl>();
        var failures = Enumerable.Range(0, 4).Select(index => new InvalidOperationException($"cleanup-{index}")).ToArray();
        var order = new List<int>();
        A.CallTo(() => ((IDisposable)standard).Dispose()).Invokes(() => order.Add(0)).Throws(failures[0]);
        A.CallTo(() => ((IDisposable)direct).Dispose()).Invokes(() => order.Add(1)).Throws(failures[1]);
        A.CallTo(() => bus.StopAsync(CancellationToken.None)).Invokes(() => order.Add(2)).Returns(Task.FromException(failures[2]));
        A.CallTo(() => ((IAsyncDisposable)provider).DisposeAsync()).Invokes(() => order.Add(3)).Returns(new ValueTask(Task.FromException(failures[3])));
        Set(fixture, "_dispatchServiceProvider", standard);
        Set(fixture, "_dispatchDirectServiceProvider", direct);
        Set(fixture, "_busControl", bus);
        Set(fixture, "_massTransitServiceProvider", provider);
        var id = Guid.NewGuid();
        _ = MassTransitBenchmarkCompletionTracker.Register(id, expectedSignals: 1);
        try
        {
            var error = await Should.ThrowAsync<AggregateException>(() => fixture.GlobalCleanup());
            order.ShouldBe(Enumerable.Range(0, 4));
            error.InnerExceptions.Count.ShouldBe(4);
            for (var index = 0; index < failures.Length; index++)
            {
                error.InnerExceptions[index].ShouldBeSameAs(failures[index]);
            }

            A.CallTo(() => ((IAsyncDisposable)provider).DisposeAsync()).MustHaveHappenedOnceExactly();
            A.CallTo(() => ((IDisposable)provider).Dispose()).MustNotHaveHappened();
            _ = MassTransitBenchmarkCompletionTracker.Register(id, expectedSignals: 1);
        }
        finally
        {
            MassTransitBenchmarkCompletionTracker.Reset();
        }
    }

    [Fact]
    public async Task AwaitBusStopAndAsyncDisposalInOrder()
    {
        var fixture = new MassTransitComparisonBenchmarks();
        var stopped = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var disposed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var enteredDisposal = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var bus = A.Fake<IBusControl>();
        var provider = A.Fake<IServiceProvider>(options => options.Implements<IDisposable>().Implements<IAsyncDisposable>());
        A.CallTo(() => bus.StopAsync(CancellationToken.None)).Returns(stopped.Task);
        A.CallTo(() => ((IAsyncDisposable)provider).DisposeAsync())
            .Invokes(() => enteredDisposal.TrySetResult()).Returns(new ValueTask(disposed.Task));
        Set(fixture, "_busControl", bus);
        Set(fixture, "_massTransitServiceProvider", provider);
        var cleanup = fixture.GlobalCleanup();
        try
        {
            cleanup.IsCompleted.ShouldBeFalse();
            A.CallTo(() => ((IAsyncDisposable)provider).DisposeAsync()).MustNotHaveHappened();
            stopped.SetResult();
            await enteredDisposal.Task.WaitAsync(TimeSpan.FromSeconds(5));
            cleanup.IsCompleted.ShouldBeFalse();
            disposed.SetResult();
            await cleanup.WaitAsync(TimeSpan.FromSeconds(5));
            A.CallTo(() => ((IAsyncDisposable)provider).DisposeAsync()).MustHaveHappenedOnceExactly();
            A.CallTo(() => ((IDisposable)provider).Dispose()).MustNotHaveHappened();
        }
        finally
        {
            stopped.TrySetResult();
            disposed.TrySetResult();
            await cleanup.WaitAsync(TimeSpan.FromSeconds(5));
        }
    }

    [Fact]
    public async Task DisposeSyncOnlyProviderAfterPartialSetup()
    {
        var fixture = new MassTransitComparisonBenchmarks();
        var provider = A.Fake<IServiceProvider>(options => options.Implements<IDisposable>());
        Set(fixture, "_massTransitServiceProvider", provider);
        await fixture.GlobalCleanup();
        A.CallTo(() => ((IDisposable)provider).Dispose()).MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task ResetTrackerWhenNoResourcesWereCreated()
    {
        var id = Guid.NewGuid();
        _ = MassTransitBenchmarkCompletionTracker.Register(id, expectedSignals: 1);
        try
        {
            await new MassTransitComparisonBenchmarks().GlobalCleanup();
            _ = MassTransitBenchmarkCompletionTracker.Register(id, expectedSignals: 1);
        }
        finally
        {
            MassTransitBenchmarkCompletionTracker.Reset();
        }
    }

    private static void Set(object fixture, string name, object value) =>
        fixture.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(fixture, value);
}
