// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch.Benchmarks.Infrastructure;
using Excalibur.Dispatch.Delivery;
using Excalibur.Dispatch.Messaging;

namespace Excalibur.Dispatch.Tests.Benchmarks;

[Trait("Category", "Unit")]
[Trait("Component", "Dispatch.Core")]
[Trait("Pattern", "Concurrency")]
public sealed class FreshContextBenchmarkDispatchShould
{
    private static readonly TimeSpan CompletionTimeout = TimeSpan.FromSeconds(10);

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public async Task PreserveTheCompletedTaskShapeAndExactResult(int mode)
    {
        var harness = new Harness(mode);
        var original = harness.DispatchTask;

        var task = harness.Invoke();
        await task.WaitAsync(CompletionTimeout);

        if (!harness.Awaited)
        {
            task.ShouldBeSameAs(original);
        }

        var result = await harness.ReadResultAsync(task);
        result.ShouldBeSameAs(harness.ExpectedResult);
        if (harness.Typed)
        {
            ((IMessageResult<object>)result).ReturnValue.ShouldBeSameAs(harness.Response);
        }

        harness.AssertReturnedOnce();
        A.CallTo(() => harness.Factory.CreateContext()).MustHaveHappenedOnceExactly();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public async Task NotReturnAContextWhenRentalFails(int mode)
    {
        var harness = new Harness(mode);
        var failure = new InvalidOperationException("rental failed");
        A.CallTo(() => harness.Factory.CreateContext()).Throws(failure);

        (await CaptureAcquisitionFailureAsync(harness)).ShouldBeSameAs(failure);

        harness.AssertNotReturned();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public async Task ReturnOnceWhenDispatchThrowsBeforeReturningATask(int mode)
    {
        var harness = new Harness(mode);
        var failure = new InvalidOperationException("synchronous dispatch failure");
        harness.DuringDispatch = () => throw failure;

        (await CaptureAcquisitionFailureAsync(harness)).ShouldBeSameAs(failure);

        harness.AssertReturnedOnce();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public async Task AttemptReturnOnlyOnceWhenBothDispatchAndReturnThrow(int mode)
    {
        var harness = new Harness(mode);
        var cleanupFailure = new InvalidOperationException("return failure");
        harness.DuringDispatch = () => throw new InvalidOperationException("dispatch failure");
        A.CallTo(() => harness.Factory.Return(harness.Context)).Throws(cleanupFailure);

        (await CaptureAcquisitionFailureAsync(harness)).ShouldBeSameAs(cleanupFailure);

        harness.AssertReturnedOnce();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public async Task NotCatchAndRepeatACompletedSuccessReturnFailure(int mode)
    {
        var harness = new Harness(mode);
        var failure = new InvalidOperationException("return failure");
        A.CallTo(() => harness.Factory.Return(harness.Context)).Throws(failure);

        (await CaptureAcquisitionFailureAsync(harness)).ShouldBeSameAs(failure);

        harness.AssertReturnedOnce();
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(0, 1)]
    [InlineData(0, 2)]
    [InlineData(1, 0)]
    [InlineData(1, 1)]
    [InlineData(1, 2)]
    [InlineData(2, 0)]
    [InlineData(2, 1)]
    [InlineData(2, 2)]
    [InlineData(3, 0)]
    [InlineData(3, 1)]
    [InlineData(3, 2)]
    public async Task RetainTheRentalUntilPendingDispatchCompletes(int mode, int outcome)
    {
        var harness = new Harness(mode);
        var untyped = new TaskCompletionSource<IMessageResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        var typed = new TaskCompletionSource<IMessageResult<object>>(TaskCreationOptions.RunContinuationsAsynchronously);
        harness.UntypedTask = untyped.Task;
        harness.TypedTask = typed.Task;
        var failure = new InvalidOperationException("pending dispatch failed");
        var token = new CancellationToken(canceled: true);

        var task = harness.Invoke();

        task.IsCompleted.ShouldBeFalse();
        harness.AssertNotReturned();
        if (harness.Typed)
        {
            if (outcome == 0) typed.SetResult(harness.TypedResult);
            else if (outcome == 1) typed.SetException(failure);
            else typed.SetCanceled(token);
        }
        else
        {
            if (outcome == 0) untyped.SetResult(harness.UntypedResult);
            else if (outcome == 1) untyped.SetException(failure);
            else untyped.SetCanceled(token);
        }

        var observed = await Record.ExceptionAsync(() => task.WaitAsync(CompletionTimeout));
        if (outcome == 0)
        {
            observed.ShouldBeNull();
            (await harness.ReadResultAsync(task)).ShouldBeSameAs(harness.ExpectedResult);
        }
        else if (outcome == 1)
        {
            observed.ShouldBeSameAs(failure);
        }
        else
        {
            observed.ShouldBeAssignableTo<OperationCanceledException>().CancellationToken.ShouldBe(token);
        }

        harness.AssertReturnedOnce();
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(0, true)]
    [InlineData(1, false)]
    [InlineData(1, true)]
    [InlineData(2, false)]
    [InlineData(2, true)]
    [InlineData(3, false)]
    [InlineData(3, true)]
    public async Task AwaitAlreadyFaultedOrCanceledTasksBeforeReturning(int mode, bool canceled)
    {
        var harness = new Harness(mode);
        var failure = new InvalidOperationException("already faulted");
        var token = new CancellationToken(canceled: true);
        harness.UntypedTask = canceled ? Task.FromCanceled<IMessageResult>(token) : Task.FromException<IMessageResult>(failure);
        harness.TypedTask = canceled
            ? Task.FromCanceled<IMessageResult<object>>(token)
            : Task.FromException<IMessageResult<object>>(failure);

        var task = harness.Invoke();
        var observed = await Record.ExceptionAsync(() => task.WaitAsync(CompletionTimeout));

        if (canceled)
        {
            observed.ShouldBeAssignableTo<OperationCanceledException>().CancellationToken.ShouldBe(token);
        }
        else
        {
            observed.ShouldBeSameAs(failure);
        }

        // Only the selected task is consumed; observe the unused faulted sibling as well.
        _ = harness.Typed ? harness.UntypedTask.Exception : harness.TypedTask.Exception;
        harness.AssertReturnedOnce();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public async Task AttemptReturnOnlyOnceWhenPendingSuccessCleanupThrows(int mode)
    {
        var harness = new Harness(mode);
        var untyped = new TaskCompletionSource<IMessageResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        var typed = new TaskCompletionSource<IMessageResult<object>>(TaskCreationOptions.RunContinuationsAsynchronously);
        harness.UntypedTask = untyped.Task;
        harness.TypedTask = typed.Task;
        var failure = new InvalidOperationException("pending cleanup failed");
        A.CallTo(() => harness.Factory.Return(harness.Context)).Throws(failure);

        var task = harness.Invoke();
        task.IsCompleted.ShouldBeFalse();
        harness.AssertNotReturned();
        if (harness.Typed) typed.SetResult(harness.TypedResult);
        else untyped.SetResult(harness.UntypedResult);

        (await Record.ExceptionAsync(() => task.WaitAsync(CompletionTimeout))).ShouldBeSameAs(failure);
        harness.AssertReturnedOnce();
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(0, true)]
    [InlineData(1, false)]
    [InlineData(1, true)]
    [InlineData(2, false)]
    [InlineData(2, true)]
    [InlineData(3, false)]
    [InlineData(3, true)]
    public async Task ReturnToTheIssuingFactoryWhenTheCallerReplacesItsReference(int mode, bool pending)
    {
        var harness = new Harness(mode);
        var issuingFactory = harness.Factory;
        var replacement = A.Fake<IMessageContextFactory>();
        var untyped = new TaskCompletionSource<IMessageResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        var typed = new TaskCompletionSource<IMessageResult<object>>(TaskCreationOptions.RunContinuationsAsynchronously);
        if (pending)
        {
            harness.UntypedTask = untyped.Task;
            harness.TypedTask = typed.Task;
        }

        harness.DuringDispatch = () => harness.Factory = replacement;

        var task = harness.Invoke();
        if (pending)
        {
            task.IsCompleted.ShouldBeFalse();
            A.CallTo(() => issuingFactory.Return(A<IMessageContext>._)).MustNotHaveHappened();
            if (harness.Typed) typed.SetResult(harness.TypedResult);
            else untyped.SetResult(harness.UntypedResult);
        }

        await task.WaitAsync(CompletionTimeout);

        A.CallTo(() => issuingFactory.Return(harness.Context)).MustHaveHappenedOnceExactly();
        A.CallTo(() => issuingFactory.Return(A<IMessageContext>._)).MustHaveHappenedOnceExactly();
        A.CallTo(() => replacement.Return(A<IMessageContext>._)).MustNotHaveHappened();
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(0, true)]
    [InlineData(1, false)]
    [InlineData(1, true)]
    [InlineData(2, false)]
    [InlineData(2, true)]
    [InlineData(3, false)]
    [InlineData(3, true)]
    public async Task ValidateDependenciesWithTheOriginalSynchronousOrAsyncShape(int mode, bool missingFactory)
    {
        var harness = new Harness(mode);
        var factory = harness.Factory;
        if (missingFactory) harness.Factory = null!;
        else harness.Dispatcher = null!;

        var observed = await CaptureAcquisitionFailureAsync(harness);

        observed.ShouldBeOfType<ArgumentNullException>().ParamName.ShouldBe(missingFactory ? "contextFactory" : "dispatcher");
        A.CallTo(() => factory.CreateContext()).MustNotHaveHappened();
        A.CallTo(() => factory.Return(A<IMessageContext>._)).MustNotHaveHappened();
    }

    private static async Task<Exception> CaptureAcquisitionFailureAsync(Harness harness)
    {
        Task? returnedTask = null;
        var synchronous = Record.Exception(() => { returnedTask = harness.Invoke(); });
        if (!harness.Awaited)
        {
            returnedTask.ShouldBeNull();
            return synchronous.ShouldNotBeNull();
        }

        synchronous.ShouldBeNull();
        _ = returnedTask.ShouldNotBeNull();
        return (await Record.ExceptionAsync(() => returnedTask!.WaitAsync(CompletionTimeout))).ShouldNotBeNull();
    }

    private sealed class Command : IDispatchMessage;
    private sealed class Query : IDispatchAction<object>;

    private sealed class Harness
    {
        public Harness(int mode)
        {
            Typed = (mode & 1) != 0;
            Awaited = (mode & 2) != 0;
            TypedResult = MessageResult.Success(Response);
            UntypedTask = Task.FromResult(UntypedResult);
            TypedTask = Task.FromResult(TypedResult);
            A.CallTo(() => Factory.CreateContext()).Returns(Context);
            A.CallTo(() => Dispatcher.DispatchAsync(A<Command>._, Context, CancellationToken.None))
                .ReturnsLazily(() => { DuringDispatch?.Invoke(); return UntypedTask; });
            A.CallTo(() => Dispatcher.DispatchAsync<Query, object>(A<Query>._, Context, CancellationToken.None))
                .ReturnsLazily(() => { DuringDispatch?.Invoke(); return TypedTask; });
        }

        public bool Typed { get; }
        public bool Awaited { get; }
        public IMessageContext Context { get; } = new MessageContext();
        public IMessageContextFactory Factory { get; set; } = A.Fake<IMessageContextFactory>();
        public IDispatcher Dispatcher { get; set; } = A.Fake<IDispatcher>();
        public object Response { get; } = new();
        public IMessageResult UntypedResult { get; } = MessageResult.Success();
        public IMessageResult<object> TypedResult { get; }
        public Task<IMessageResult> UntypedTask { get; set; }
        public Task<IMessageResult<object>> TypedTask { get; set; }
        public Task DispatchTask => Typed ? TypedTask : UntypedTask;
        public IMessageResult ExpectedResult => Typed ? TypedResult : UntypedResult;
        public Action? DuringDispatch { get; set; }

        public Task Invoke() => Typed
            ? Awaited
                ? FreshContextBenchmarkDispatch.DispatchAwaitedAsync<Query, object>(Dispatcher, Factory, new Query())
                : FreshContextBenchmarkDispatch.DispatchAsync<Query, object>(Dispatcher, Factory, new Query())
            : Awaited
                ? FreshContextBenchmarkDispatch.DispatchAwaitedAsync(Dispatcher, Factory, new Command())
                : FreshContextBenchmarkDispatch.DispatchAsync(Dispatcher, Factory, new Command());

        public async Task<IMessageResult> ReadResultAsync(Task task) => Typed
            ? await (Task<IMessageResult<object>>)task
            : await (Task<IMessageResult>)task;

        public void AssertReturnedOnce()
        {
            A.CallTo(() => Factory.Return(Context)).MustHaveHappenedOnceExactly();
            A.CallTo(() => Factory.Return(A<IMessageContext>._)).MustHaveHappenedOnceExactly();
        }

        public void AssertNotReturned() => A.CallTo(() => Factory.Return(A<IMessageContext>._)).MustNotHaveHappened();
    }
}
