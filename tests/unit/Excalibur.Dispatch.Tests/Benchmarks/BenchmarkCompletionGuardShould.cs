// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch.Benchmarks.Infrastructure;

namespace Excalibur.Dispatch.Tests.Benchmarks;

[Trait("Category", "Unit")]
[Trait("Component", "Dispatch.Core")]
[Trait("Pattern", "Behavior")]
public sealed class BenchmarkCompletionGuardShould
{
    [Fact]
    public async Task RejectUnfinishedHandlersWithoutWaitingForTheirLaterCompletion()
    {
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var verification = BenchmarkCompletionGuard.VerifyCompletedAsync(Task.CompletedTask, completion.Task);
        try
        {
            verification.IsCompleted.ShouldBeTrue();
            await Should.ThrowAsync<InvalidOperationException>(() => verification);
        }
        finally
        {
            completion.TrySetResult();
        }
    }

    [Fact]
    public async Task AcceptCompletedHandlersAfterAnAsynchronousInvocation()
    {
        var invocation = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var verification = BenchmarkCompletionGuard.VerifyCompletedAsync(invocation.Task, completion.Task);
        verification.IsCompleted.ShouldBeFalse();
        completion.SetResult();
        invocation.SetResult();
        await verification;
    }

    [Fact]
    public async Task RejectIncompleteHandlersAfterPendingInvocationFinishes()
    {
        var invocation = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var verification = BenchmarkCompletionGuard.VerifyCompletedAsync(invocation.Task, completion.Task);
        invocation.SetResult();
        try
        {
            await Should.ThrowAsync<InvalidOperationException>(() => verification.WaitAsync(TimeSpan.FromSeconds(2)));
        }
        finally
        {
            completion.TrySetResult();
        }
    }

    [Fact]
    public async Task PreserveInvocationFailure()
    {
        var failure = new InvalidOperationException("Invocation failed");
        var actual = await Should.ThrowAsync<InvalidOperationException>(() =>
            BenchmarkCompletionGuard.VerifyCompletedAsync(Task.FromException(failure), Task.CompletedTask));
        actual.ShouldBeSameAs(failure);
    }

    [Fact]
    public async Task PreserveInvocationCancellation()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Should.ThrowAsync<OperationCanceledException>(() =>
            BenchmarkCompletionGuard.VerifyCompletedAsync(Task.FromCanceled(cancellation.Token), Task.CompletedTask));
    }

    [Fact]
    public async Task RejectFaultedCompletion()
    {
        await Should.ThrowAsync<InvalidOperationException>(() =>
            BenchmarkCompletionGuard.VerifyCompletedAsync(Task.CompletedTask, Task.FromException(new InvalidOperationException("Handler failed"))));
    }

    [Fact]
    public async Task RejectCanceledCompletion()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Should.ThrowAsync<InvalidOperationException>(() =>
            BenchmarkCompletionGuard.VerifyCompletedAsync(Task.CompletedTask, Task.FromCanceled(cancellation.Token)));
    }
}
