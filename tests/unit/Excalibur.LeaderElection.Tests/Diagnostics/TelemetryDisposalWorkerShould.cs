// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Transactions;

using Excalibur.LeaderElection.Diagnostics;

namespace Excalibur.LeaderElection.Tests.Diagnostics;

[Trait("Category", "Unit")]
[Trait("Component", "LeaderElection")]
[Trait("Pattern", "Regression")]
public sealed class TelemetryDisposalWorkerShould
{
    [Fact]
    public async Task ShareTheActualResourceJoinWithoutJoiningTheNotificationTail()
    {
        var entered = NewSignal();
        var resources = NewSignal();
        var notifications = NewSignal();
        var inner = NewInner(notifications.Task);
        A.CallTo(() => inner.DisposeAsync()).ReturnsLazily(() =>
        {
            entered.TrySetResult();
            return new ValueTask(resources.Task);
        });
        using var meter = new Meter("TelemetryDisposalWorker." + Guid.NewGuid().ToString("N"));
        using var activity = new ActivitySource("TelemetryDisposalWorker");
        var sut = new TelemetryLeaderElection(inner, meter, activity, "test");
        var first = sut.DisposeAsync().AsTask();
        var repeated = sut.DisposeAsync().AsTask();
        try
        {
            repeated.ShouldBeSameAs(first);
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
            first.IsCompleted.ShouldBeFalse("entering inner disposal is not resource completion");
            sut.NotificationsCompleted.IsCompleted.ShouldBeFalse();
            resources.TrySetResult();
            await first.WaitAsync(TimeSpan.FromSeconds(10));
            sut.DisposeAsync().AsTask().ShouldBeSameAs(first);
            sut.NotificationsCompleted.IsCompleted.ShouldBeFalse("resource cleanup must not self-join subscriber delivery");
            A.CallTo(() => inner.DisposeAsync()).MustHaveHappenedOnceExactly();
        }
        finally
        {
            resources.TrySetResult();
            notifications.TrySetResult();
            await first.WaitAsync(TimeSpan.FromSeconds(10));
            await sut.NotificationsCompleted.WaitAsync(TimeSpan.FromSeconds(10));
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RetainTheExactDelayedResourceFailureAfterNotificationsComplete(bool canceled)
    {
        var entered = NewSignal();
        var resources = NewSignal();
        var notifications = NewSignal();
        var inner = NewInner(notifications.Task);
        A.CallTo(() => inner.DisposeAsync()).ReturnsLazily(() =>
        {
            entered.TrySetResult();
            return new ValueTask(resources.Task);
        });
        using var meter = new Meter("TelemetryDisposalFailure." + Guid.NewGuid().ToString("N"));
        using var activity = new ActivitySource("TelemetryDisposalFailure");
        var sut = new TelemetryLeaderElection(inner, meter, activity, "test");
        Exception failure = canceled ? new OperationCanceledException("delayed disposal canceled") : new IOException("delayed disposal failed");
        var resource = sut.DisposeAsync().AsTask();
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
            notifications.TrySetResult();
            await sut.NotificationsCompleted.WaitAsync(TimeSpan.FromSeconds(10));
            resource.IsCompleted.ShouldBeFalse("notification completion cannot publish an unfinished resource result");
            resources.TrySetException(failure);
            (await Record.ExceptionAsync(() => resource.WaitAsync(TimeSpan.FromSeconds(10)))).ShouldBeSameAs(failure);
            sut.DisposeAsync().AsTask().ShouldBeSameAs(resource);
            (await Record.ExceptionAsync(() => sut.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(10)))).ShouldBeSameAs(failure);
            A.CallTo(() => inner.DisposeAsync()).MustHaveHappenedOnceExactly();
        }
        finally
        {
            resources.TrySetException(failure);
            notifications.TrySetResult();
            _ = await Record.ExceptionAsync(() => resource.WaitAsync(TimeSpan.FromSeconds(10)));
            await sut.NotificationsCompleted.WaitAsync(TimeSpan.FromSeconds(10));
        }
    }

    [Fact]
    public async Task SuppressTheBusinessTransactionWhilePreservingDiagnosticContext()
    {
        var notifications = NewSignal();
        var inner = NewInner(notifications.Task);
        var context = new AsyncLocal<string?> { Value = "caller-diagnostic-context" };
        Transaction? observedTransaction = null;
        string? observedContext = null;
        var entered = false;
        A.CallTo(() => inner.DisposeAsync()).ReturnsLazily(() =>
        {
            observedTransaction = Transaction.Current;
            observedContext = context.Value;
            entered = true;
            notifications.TrySetResult();
            return ValueTask.CompletedTask;
        });
        using var meter = new Meter("TelemetryDisposalTransaction." + Guid.NewGuid().ToString("N"));
        using var activity = new ActivitySource("TelemetryDisposalTransaction");
        var sut = new TelemetryLeaderElection(inner, meter, activity, "test");
        using var business = new TransactionScope(TransactionScopeOption.Required, TransactionScopeAsyncFlowOption.Enabled);
        var callerTransaction = Transaction.Current;
        callerTransaction.ShouldNotBeNull();
        try
        {
            await sut.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(10));
            entered.ShouldBeTrue();
            observedTransaction.ShouldBeNull("election resource cleanup must not enlist in a caller's business transaction");
            observedContext.ShouldBe("caller-diagnostic-context");
            Transaction.Current.ShouldBeSameAs(callerTransaction);
            context.Value.ShouldBe("caller-diagnostic-context");
            await sut.NotificationsCompleted.WaitAsync(TimeSpan.FromSeconds(10));
        }
        finally
        {
            notifications.TrySetResult();
            await sut.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(10));
            await sut.NotificationsCompleted.WaitAsync(TimeSpan.FromSeconds(10));
            context.Value = null;
        }
    }

    private static TaskCompletionSource NewSignal() => new(TaskCreationOptions.RunContinuationsAsynchronously);

    private static ILeaderElection NewInner(Task notifications)
    {
        var inner = A.Fake<ILeaderElection>();
        A.CallTo(() => inner.NotificationsCompleted).Returns(notifications);
        A.CallTo(() => inner.CandidateId).Returns("worker-candidate");
        return inner;
    }
}
