// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch.Benchmarks.Infrastructure;
using Excalibur.Dispatch.Delivery;
using Excalibur.Dispatch.Features;
using Excalibur.Dispatch.Messaging;
using Excalibur.Dispatch.Routing;

namespace Excalibur.Dispatch.Tests.Benchmarks;

[Trait("Category", "Unit")]
[Trait("Component", "Dispatch.Core")]
[Trait("Pattern", "Concurrency")]
public sealed class RoutingBenchmarkDispatchShould
{
    private static readonly RoutingDecision Decision = RoutingDecision.Success("local", ["local"]);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ReturnTheExactRentalAndResultOnCompletedSuccess(bool typed)
    {
        var harness = new Harness(typed);

        var result = await harness.InvokeAsync();

        result.ShouldBeSameAs(typed ? harness.TypedResult : harness.UntypedResult);
        harness.Context.GetRoutingFeature()!.RoutingDecision.ShouldBeSameAs(Decision);
        harness.AssertReturnedOnce();
        A.CallTo(() => harness.Factory.CreateContext()).MustHaveHappenedOnceExactly();
        if (typed)
        {
            ((IMessageResult<object>)result).ReturnValue.ShouldBeSameAs(harness.Response);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AvoidReturningWhenRentThrows(bool typed)
    {
        var harness = new Harness(typed);
        var failure = new InvalidOperationException("rent failed");
        A.CallTo(() => harness.Factory.CreateContext()).Throws(failure);

        (await Should.ThrowAsync<InvalidOperationException>(() => harness.InvokeAsync())).ShouldBeSameAs(failure);

        A.CallTo(() => harness.Factory.CreateContext()).MustHaveHappenedOnceExactly();
        harness.AssertNotReturned();
        Fake.GetCalls(harness.Dispatcher).ShouldBeEmpty();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ReturnWhenRoutingFeatureAccessThrows(bool typed)
    {
        var context = A.Fake<IMessageContext>();
        var failure = new InvalidOperationException("feature access failed");
        A.CallTo(() => context.Features).Throws(failure);
        var harness = new Harness(typed, context);

        (await Should.ThrowAsync<InvalidOperationException>(() => harness.InvokeAsync())).ShouldBeSameAs(failure);

        A.CallTo(() => context.Features).MustHaveHappenedOnceExactly();
        harness.AssertReturnedOnce();
        Fake.GetCalls(harness.Dispatcher).ShouldBeEmpty();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ReturnWhenRoutingDecisionAssignmentThrows(bool typed)
    {
        var context = A.Fake<IMessageContext>();
        var feature = A.Fake<IMessageRoutingFeature>();
        var failure = new InvalidOperationException("routing assignment failed");
        A.CallTo(() => context.Features).Returns(new Dictionary<Type, object>
        {
            [typeof(IMessageRoutingFeature)] = feature,
        });
        A.CallToSet(() => feature.RoutingDecision).To(Decision).Throws(failure);
        var harness = new Harness(typed, context);

        (await Should.ThrowAsync<InvalidOperationException>(() => harness.InvokeAsync())).ShouldBeSameAs(failure);

        A.CallToSet(() => feature.RoutingDecision).To(Decision).MustHaveHappenedOnceExactly();
        harness.AssertReturnedOnce();
        Fake.GetCalls(harness.Dispatcher).ShouldBeEmpty();
    }

    [Fact]
    public async Task InstallRoutingBeforeConfigurationAndReturnWhenConfigurationThrows()
    {
        var harness = new Harness(typed: false);
        var failure = new InvalidOperationException("configuration failed");
        var configured = false;

        var observed = await Should.ThrowAsync<InvalidOperationException>(() => harness.InvokeAsync(context =>
        {
            configured = true;
            context.ShouldBeSameAs(harness.Context);
            context.GetRoutingFeature()!.RoutingDecision.ShouldBeSameAs(Decision);
            throw failure;
        }));

        observed.ShouldBeSameAs(failure);
        configured.ShouldBeTrue();
        harness.AssertReturnedOnce();
        Fake.GetCalls(harness.Dispatcher).ShouldBeEmpty();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ReturnWhenDispatchThrowsSynchronously(bool typed)
    {
        var harness = new Harness(typed);
        var failure = new InvalidOperationException("synchronous dispatch failed");
        A.CallTo(() => harness.Dispatcher.DispatchAsync(A<Command>._, harness.Context, CancellationToken.None)).Throws(failure);
        A.CallTo(() => harness.Dispatcher.DispatchAsync<Query, object>(A<Query>._, harness.Context, CancellationToken.None)).Throws(failure);

        (await Should.ThrowAsync<InvalidOperationException>(() => harness.InvokeAsync())).ShouldBeSameAs(failure);

        Fake.GetCalls(harness.Dispatcher).ShouldHaveSingleItem();
        harness.AssertReturnedOnce();
    }

    [Theory]
    [InlineData(false, "success")]
    [InlineData(true, "success")]
    [InlineData(false, "fault")]
    [InlineData(true, "fault")]
    [InlineData(false, "cancel")]
    [InlineData(true, "cancel")]
    public async Task KeepTheRentalUntilPendingDispatchCompletes(bool typed, string outcome)
    {
        var harness = new Harness(typed);
        var untypedSource = new TaskCompletionSource<IMessageResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        var typedSource = new TaskCompletionSource<IMessageResult<object>>(TaskCreationOptions.RunContinuationsAsynchronously);
        A.CallTo(() => harness.Dispatcher.DispatchAsync(A<Command>._, harness.Context, CancellationToken.None)).Returns(untypedSource.Task);
        A.CallTo(() => harness.Dispatcher.DispatchAsync<Query, object>(A<Query>._, harness.Context, CancellationToken.None)).Returns(typedSource.Task);
        var task = harness.InvokeAsync();

        task.IsCompleted.ShouldBeFalse();
        harness.AssertNotReturned();
        var failure = new InvalidOperationException("asynchronous dispatch failed");
        var token = new CancellationToken(canceled: true);
        switch (outcome)
        {
            case "success":
                if (typed)
                {
                    typedSource.SetResult(harness.TypedResult);
                }
                else
                {
                    untypedSource.SetResult(harness.UntypedResult);
                }

                (await task).ShouldBeSameAs(typed ? harness.TypedResult : harness.UntypedResult);
                break;
            case "fault":
                if (typed)
                {
                    typedSource.SetException(failure);
                }
                else
                {
                    untypedSource.SetException(failure);
                }

                (await Should.ThrowAsync<InvalidOperationException>(() => task)).ShouldBeSameAs(failure);
                break;
            case "cancel":
                if (typed)
                {
                    typedSource.SetCanceled(token);
                }
                else
                {
                    untypedSource.SetCanceled(token);
                }

                (await Should.ThrowAsync<TaskCanceledException>(() => task)).CancellationToken.ShouldBe(token);
                break;
            default:
                throw new InvalidOperationException("Unknown test outcome.");
        }

        harness.AssertReturnedOnce();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ReturnWhenDispatchIsAlreadyFaulted(bool typed)
    {
        var harness = new Harness(typed);
        var failure = new InvalidOperationException("completed dispatch failed");
        A.CallTo(() => harness.Dispatcher.DispatchAsync(A<Command>._, harness.Context, CancellationToken.None))
            .Returns(Task.FromException<IMessageResult>(failure));
        A.CallTo(() => harness.Dispatcher.DispatchAsync<Query, object>(A<Query>._, harness.Context, CancellationToken.None))
            .Returns(Task.FromException<IMessageResult<object>>(failure));

        (await Should.ThrowAsync<InvalidOperationException>(() => harness.InvokeAsync())).ShouldBeSameAs(failure);

        harness.AssertReturnedOnce();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ReturnWhenDispatchIsAlreadyCanceled(bool typed)
    {
        var harness = new Harness(typed);
        var token = new CancellationToken(canceled: true);
        A.CallTo(() => harness.Dispatcher.DispatchAsync(A<Command>._, harness.Context, CancellationToken.None))
            .Returns(Task.FromCanceled<IMessageResult>(token));
        A.CallTo(() => harness.Dispatcher.DispatchAsync<Query, object>(A<Query>._, harness.Context, CancellationToken.None))
            .Returns(Task.FromCanceled<IMessageResult<object>>(token));

        (await Should.ThrowAsync<TaskCanceledException>(() => harness.InvokeAsync())).CancellationToken.ShouldBe(token);

        harness.AssertReturnedOnce();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AttemptReturnOnlyOnceWhenReturnThrows(bool typed)
    {
        var harness = new Harness(typed);
        var failure = new InvalidOperationException("return failed");
        A.CallTo(() => harness.Factory.Return(harness.Context)).Throws(failure);

        (await Should.ThrowAsync<InvalidOperationException>(() => harness.InvokeAsync())).ShouldBeSameAs(failure);

        harness.AssertReturnedOnce();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PreserveFinallyPrecedenceWhenDispatchAndReturnBothThrow(bool typed)
    {
        var harness = new Harness(typed);
        var dispatchFailure = new InvalidOperationException("dispatch failed first");
        var returnFailure = new InvalidOperationException("return failed second");
        A.CallTo(() => harness.Dispatcher.DispatchAsync(A<Command>._, harness.Context, CancellationToken.None)).Throws(dispatchFailure);
        A.CallTo(() => harness.Dispatcher.DispatchAsync<Query, object>(A<Query>._, harness.Context, CancellationToken.None)).Throws(dispatchFailure);
        A.CallTo(() => harness.Factory.Return(harness.Context)).Throws(returnFailure);

        (await Should.ThrowAsync<InvalidOperationException>(() => harness.InvokeAsync())).ShouldBeSameAs(returnFailure);

        Fake.GetCalls(harness.Dispatcher).ShouldHaveSingleItem();
        harness.AssertReturnedOnce();
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task KeepArgumentFailuresOnTheReturnedTask(bool typed, bool missingDispatcher)
    {
        var dispatcher = missingDispatcher ? null : A.Fake<IDispatcher>();
        Task? task = null;
        var synchronous = Record.Exception(() =>
        {
            task = typed
                ? RoutingBenchmarkDispatch.DispatchAsync<Query, object>(dispatcher, null, new Query(), Decision)
                : RoutingBenchmarkDispatch.DispatchAsync(dispatcher, null, new Command(), Decision);
        });

        synchronous.ShouldBeNull();
        _ = task.ShouldNotBeNull();
        (await Should.ThrowAsync<ArgumentNullException>(() => task)).ParamName.ShouldBe(missingDispatcher ? "dispatcher" : "contextFactory");
    }

    [Fact]
    public async Task ConfigureTheRoutedContextBeforeDispatch()
    {
        var harness = new Harness(typed: false);
        var configured = false;
        A.CallTo(() => harness.Dispatcher.DispatchAsync(A<Command>._, harness.Context, CancellationToken.None))
            .ReturnsLazily(() =>
            {
                configured.ShouldBeTrue();
                harness.Context.GetRoutingFeature()!.RoutingDecision.ShouldBeSameAs(Decision);
                return Task.FromResult(harness.UntypedResult);
            });

        (await harness.InvokeAsync(context =>
        {
            context.ShouldBeSameAs(harness.Context);
            context.GetRoutingFeature()!.RoutingDecision.ShouldBeSameAs(Decision);
            configured = true;
        })).ShouldBeSameAs(harness.UntypedResult);

        harness.AssertReturnedOnce();
    }

    [Fact]
    public async Task PreserveQueuedInterfaceDispatchAndReturnTheResult()
    {
        var harness = new Harness(typed: false, queued: true);

        var result = await harness.InvokeAsync().WaitAsync(TimeSpan.FromSeconds(10));

        result.ShouldBeSameAs(harness.UntypedResult);
        harness.Context.GetRoutingFeature()!.RoutingDecision.ShouldBeSameAs(Decision);
        A.CallTo(() => harness.Dispatcher.DispatchAsync(A<IDispatchMessage>._, harness.Context, CancellationToken.None))
            .MustHaveHappenedOnceExactly();
        harness.AssertReturnedOnce();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ReturnQueuedRentalWhenRoutingSetupThrows(bool assignmentFailure)
    {
        var context = A.Fake<IMessageContext>();
        var failure = new InvalidOperationException("queued routing setup failed");
        if (assignmentFailure)
        {
            var feature = A.Fake<IMessageRoutingFeature>();
            A.CallTo(() => context.Features).Returns(new Dictionary<Type, object>
            {
                [typeof(IMessageRoutingFeature)] = feature,
            });
            A.CallToSet(() => feature.RoutingDecision).To(Decision).Throws(failure);
        }
        else
        {
            A.CallTo(() => context.Features).Throws(failure);
        }

        var harness = new Harness(typed: false, context, queued: true);

        (await Should.ThrowAsync<InvalidOperationException>(() => harness.InvokeAsync())).ShouldBeSameAs(failure);

        harness.AssertReturnedOnce();
        Fake.GetCalls(harness.Dispatcher).ShouldBeEmpty();
    }

    [Fact]
    public async Task NotReturnAQueuedRentalThatWasNeverCreated()
    {
        var harness = new Harness(typed: false, queued: true);
        var failure = new InvalidOperationException("queued rental failed");
        A.CallTo(() => harness.Factory.CreateContext()).Throws(failure);

        (await Should.ThrowAsync<InvalidOperationException>(() => harness.InvokeAsync())).ShouldBeSameAs(failure);

        harness.AssertNotReturned();
        Fake.GetCalls(harness.Dispatcher).ShouldBeEmpty();
    }

    [Fact]
    public async Task ReturnQueuedRentalWhenDispatchThrowsSynchronously()
    {
        var harness = new Harness(typed: false, queued: true);
        var failure = new InvalidOperationException("queued dispatch failed");
        A.CallTo(() => harness.Dispatcher.DispatchAsync(A<IDispatchMessage>._, harness.Context, CancellationToken.None))
            .Throws(failure);

        (await Should.ThrowAsync<InvalidOperationException>(() => harness.InvokeAsync())).ShouldBeSameAs(failure);

        harness.AssertReturnedOnce();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public async Task RetainQueuedRentalThroughPendingSuccessFaultOrCancellation(int outcome)
    {
        var harness = new Harness(typed: false, queued: true);
        var pending = new TaskCompletionSource<IMessageResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        A.CallTo(() => harness.Dispatcher.DispatchAsync(A<IDispatchMessage>._, harness.Context, CancellationToken.None))
            .Returns(pending.Task);
        var failure = new InvalidOperationException("queued pending failure");
        var token = new CancellationToken(canceled: true);

        var task = harness.InvokeAsync();
        task.IsCompleted.ShouldBeFalse();
        harness.AssertNotReturned();
        if (outcome == 0) pending.SetResult(harness.UntypedResult);
        else if (outcome == 1) pending.SetException(failure);
        else pending.SetCanceled(token);

        var observed = await Record.ExceptionAsync(() => task.WaitAsync(TimeSpan.FromSeconds(10)));
        if (outcome == 0)
        {
            observed.ShouldBeNull();
            (await task).ShouldBeSameAs(harness.UntypedResult);
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
    [InlineData(false)]
    [InlineData(true)]
    public async Task AttemptQueuedReturnOnlyOnceWhenCleanupThrows(bool dispatchAlsoFails)
    {
        var harness = new Harness(typed: false, queued: true);
        var failure = new InvalidOperationException("queued cleanup failure");
        A.CallTo(() => harness.Factory.Return(harness.Context)).Throws(failure);
        if (dispatchAlsoFails)
        {
            A.CallTo(() => harness.Dispatcher.DispatchAsync(A<IDispatchMessage>._, harness.Context, CancellationToken.None))
                .Throws(new InvalidOperationException("queued dispatch failure"));
        }

        (await Should.ThrowAsync<InvalidOperationException>(() => harness.InvokeAsync())).ShouldBeSameAs(failure);

        harness.AssertReturnedOnce();
    }

    [Fact]
    public async Task AttemptQueuedReturnOnlyOnceWhenPendingCleanupThrows()
    {
        var harness = new Harness(typed: false, queued: true);
        var pending = new TaskCompletionSource<IMessageResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        A.CallTo(() => harness.Dispatcher.DispatchAsync(A<IDispatchMessage>._, harness.Context, CancellationToken.None))
            .Returns(pending.Task);
        var failure = new InvalidOperationException("queued pending cleanup failure");
        A.CallTo(() => harness.Factory.Return(harness.Context)).Throws(failure);

        var task = harness.InvokeAsync();
        task.IsCompleted.ShouldBeFalse();
        harness.AssertNotReturned();
        pending.SetResult(harness.UntypedResult);

        (await Should.ThrowAsync<InvalidOperationException>(() => task.WaitAsync(TimeSpan.FromSeconds(10))))
            .ShouldBeSameAs(failure);

        harness.AssertReturnedOnce();
    }

    private sealed class Command : IDispatchMessage
    {
    }

    private sealed class Query : IDispatchAction<object>
    {
    }

    private sealed class Harness
    {
        private readonly bool _typed;
        private readonly bool _queued;

        public Harness(bool typed, IMessageContext? context = null, bool queued = false)
        {
            _typed = typed;
            _queued = queued;
            Context = context ?? new MessageContext();
            TypedResult = MessageResult.Success(Response);
            A.CallTo(() => Factory.CreateContext()).Returns(Context);
            A.CallTo(() => Dispatcher.DispatchAsync(A<Command>._, Context, CancellationToken.None)).Returns(Task.FromResult(UntypedResult));
            A.CallTo(() => Dispatcher.DispatchAsync(A<IDispatchMessage>._, Context, CancellationToken.None)).Returns(Task.FromResult(UntypedResult));
            A.CallTo(() => Dispatcher.DispatchAsync<Query, object>(A<Query>._, Context, CancellationToken.None)).Returns(Task.FromResult(TypedResult));
        }

        public IMessageContext Context { get; }
        public IMessageContextFactory Factory { get; } = A.Fake<IMessageContextFactory>();
        public IDispatcher Dispatcher { get; } = A.Fake<IDispatcher>();
        public object Response { get; } = new();
        public IMessageResult UntypedResult { get; } = MessageResult.Success();
        public IMessageResult<object> TypedResult { get; }

        public async Task<IMessageResult> InvokeAsync(Action<IMessageContext>? configure = null)
        {
            if (_queued)
            {
                return await RoutingBenchmarkDispatch.DispatchQueuedAsync(Dispatcher, Factory, new Command(), Decision);
            }

            return _typed
                ? await RoutingBenchmarkDispatch.DispatchAsync<Query, object>(Dispatcher, Factory, new Query(), Decision)
                : await RoutingBenchmarkDispatch.DispatchAsync(Dispatcher, Factory, new Command(), Decision, configure);
        }

        public void AssertReturnedOnce()
        {
            A.CallTo(() => Factory.Return(Context)).MustHaveHappenedOnceExactly();
            A.CallTo(() => Factory.Return(A<IMessageContext>._)).MustHaveHappenedOnceExactly();
        }

        public void AssertNotReturned() => A.CallTo(() => Factory.Return(A<IMessageContext>._)).MustNotHaveHappened();
    }
}
