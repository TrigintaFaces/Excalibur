// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Text.Json;
using Excalibur.Dispatch;
using Excalibur.Dispatch.Delivery;
using Excalibur.Dispatch.Delivery.Handlers;
using Excalibur.Dispatch.Delivery.Registry;
using Excalibur.Dispatch.Middleware.Resilience;
using Excalibur.Dispatch.Options.Resilience;
using Excalibur.Dispatch.Exceptions;
using Excalibur.Dispatch.Features;
using Excalibur.Dispatch.Routing;
using Excalibur.Dispatch.Transport;
using Excalibur.Outbox.InMemory;
using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.Outbox.Tests;

[Trait("Category", "Unit")]
[Trait("Component", "Outbox")]
[Trait("Pattern", "Regression")]
public sealed class OutboxPersistedRetryOwnershipShould
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task Retire_Explicitly_Classified_Permanent_Or_Poison_Failure_After_One_Persisted_Attempt(bool batch, bool poison)
    {
        var state = new FaultState { TypedFault = poison ? new JsonException("CONTROLLED-TYPED-POISON")
            : new ConfigurationException("CONTROLLED-TYPED-PERMANENT") };
        await using var provider = Build(state, batchProcessing: batch);
        var message = await StageClassifiedAsync(provider, "typed-single");
        var processor = provider.GetRequiredService<IOutboxProcessor>();
        processor.Init("typed-permanent-owner-test");
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        (await processor.DispatchPendingMessagesAsync(deadline.Token)).ShouldBe(0);
        state.Calls.ShouldBe(1);
        message.Status.ShouldBe(OutboxStatus.DeadLettered, $"the original typed failure must retire immediately: {message.LastError}");
        await Task.Delay(TimeSpan.FromMilliseconds(1100), deadline.Token);
        (await processor.DispatchPendingMessagesAsync(deadline.Token)).ShouldBe(0);
        state.Calls.ShouldBe(1, "a terminal permanent or poison failure must not invoke its handler again");
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task Retire_Only_All_Permanent_Fanout_And_Keep_Mixed_Transient_Fanout_Retryable(bool batch, bool mixed)
    {
        var state = new FaultState { Fanout = true, MixedFanout = mixed };
        await using var provider = Build(state, batchProcessing: batch);
        var message = await StageClassifiedAsync(provider, "typed-fanout");
        var processor = provider.GetRequiredService<IOutboxProcessor>();
        processor.Init("typed-fanout-owner-test");
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        (await processor.DispatchPendingMessagesAsync(deadline.Token)).ShouldBe(0);
        state.RouteCalls.ShouldBe([1, 1], customMessage: $"both actual bus routes must execute: {message.LastError}");
        message.Status.ShouldBe(mixed ? OutboxStatus.Failed : OutboxStatus.DeadLettered,
            $"only all known Permanent/Poison route failures justify immediate retirement: {message.LastError}");
        await Task.Delay(TimeSpan.FromMilliseconds(1100), deadline.Token);
        (await processor.DispatchPendingMessagesAsync(deadline.Token)).ShouldBe(0);
        if (mixed)
        {
            state.RouteCalls[1].ShouldBe(2, "a permanent sibling must not discard the retryable destination's next durable attempt");
            message.Status.ShouldBe(OutboxStatus.Failed);
            message.RetryCount.ShouldBe(2);
        }
        else { state.RouteCalls.ShouldBe([1, 1], customMessage: "terminal fanout must not republish any destination"); }
    }

    private static async Task<OutboundMessage> StageClassifiedAsync(ServiceProvider provider, string value)
    {
        MessageTypeRegistry.RegisterType<OwnedRetryEvent>();
        var message = new OutboundMessage(nameof(OwnedRetryEvent), JsonSerializer.SerializeToUtf8Bytes(new OwnedRetryEvent(value)), "local");
        await provider.GetRequiredService<IOutboxStore>().StageMessageAsync(message, CancellationToken.None);
        return message;
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Treat_A_Wrapped_Caller_Cancellation_As_No_Persisted_Or_Breaker_Failure(bool batch)
    {
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var state = new FaultState { WrappedCallerCancellation = cancellation };
        await using var provider = Build(state, batchProcessing: batch);
        var message = await StageClassifiedAsync(provider, "wrapped-caller-cancellation");
        var processor = provider.GetRequiredService<IOutboxProcessor>();
        processor.Init("wrapped-cancellation-owner-test");
        _ = await Should.ThrowAsync<OperationCanceledException>(() => processor.DispatchPendingMessagesAsync(cancellation.Token));
        state.Calls.ShouldBe(1);
        message.RetryCount.ShouldBe(0, "caller cancellation must not consume the durable failure budget");
        message.Status.ShouldNotBe(OutboxStatus.Sent);
        message.Status.ShouldNotBe(OutboxStatus.DeadLettered);
        var breaker = provider.GetRequiredService<ITransportCircuitBreakerRegistry>().GetOrCreate(nameof(OwnedRetryEvent));
        breaker.ShouldBeAssignableTo<ICircuitBreakerDiagnostics>().ConsecutiveFailures.ShouldBe(0,
            "the IOException wrapper must be normalized before the circuit breaker classifies its outcome");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Keep_An_Independent_Transport_Timeout_As_A_Real_Retryable_Failure(bool batch)
    {
        var state = new FaultState { TypedFault = new IOException("independent timeout", new TimeoutException("own transport deadline")) };
        await using var provider = Build(state, batchProcessing: batch);
        var message = await StageClassifiedAsync(provider, "independent-timeout");
        var processor = provider.GetRequiredService<IOutboxProcessor>();
        processor.Init("independent-timeout-owner-test");
        using var caller = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        (await processor.DispatchPendingMessagesAsync(caller.Token)).ShouldBe(0);
        caller.IsCancellationRequested.ShouldBeFalse();
        state.Calls.ShouldBe(1);
        message.Status.ShouldBe(OutboxStatus.Failed);
        message.RetryCount.ShouldBe(1);
        var breaker = provider.GetRequiredService<ITransportCircuitBreakerRegistry>().GetOrCreate(nameof(OwnedRetryEvent));
        breaker.ShouldBeAssignableTo<ICircuitBreakerDiagnostics>().ConsecutiveFailures.ShouldBe(1);
    }

    [Fact]
    public async Task Keep_An_Unknown_Failed_Result_Retryable_Without_Classifying_A_Fabricated_Exception()
    {
        var classifier = A.Fake<IMessageFailureClassifier>();
        A.CallTo(() => classifier.Classify(A<Exception>._)).Returns(MessageFailureKind.Permanent);
        var state = new FaultState { FailedResult = true, Classifier = classifier };
        await using var provider = Build(state);
        var message = await StageClassifiedAsync(provider, "unknown-result-custom-classifier");
        var processor = provider.GetRequiredService<IOutboxProcessor>();
        processor.Init("unknown-result-owner-test");
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        (await processor.DispatchPendingMessagesAsync(deadline.Token)).ShouldBe(0);
        state.Calls.ShouldBe(1);
        message.Status.ShouldBe(OutboxStatus.Failed);
        message.RetryCount.ShouldBe(1);
        await Task.Delay(TimeSpan.FromMilliseconds(1100), deadline.Token);
        (await processor.DispatchPendingMessagesAsync(deadline.Token)).ShouldBe(0);
        state.Calls.ShouldBe(2);
        message.Status.ShouldBe(OutboxStatus.Failed);
        message.RetryCount.ShouldBe(2);
        A.CallTo(() => classifier.Classify(A<Exception>._)).MustNotHaveHappened();
        var breaker = provider.GetRequiredService<ITransportCircuitBreakerRegistry>().GetOrCreate(nameof(OwnedRetryEvent));
        breaker.ShouldBeAssignableTo<ICircuitBreakerDiagnostics>().ConsecutiveFailures.ShouldBe(2,
            "two returned failed results must count once each, never as breaker successes or twice-counted failures");
    }
    [Fact]
    public async Task Keep_Failed_Results_From_Multiplying_Persisted_Attempts()
    {
        var state = new FaultState { FailedResult = true };
        await using var provider = Build(state);
        var store = provider.GetRequiredService<IOutboxStore>();
        MessageTypeRegistry.RegisterType<OwnedRetryEvent>();
        var message = new OutboundMessage(nameof(OwnedRetryEvent), JsonSerializer.SerializeToUtf8Bytes(new OwnedRetryEvent("failed-result")), "local");
        await store.StageMessageAsync(message, CancellationToken.None);
        var processor = provider.GetRequiredService<IOutboxProcessor>();
        processor.Init("failed-result-owner-test");
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        for (var attempt = 1; attempt <= 4; attempt++)
        {
            (await processor.DispatchPendingMessagesAsync(deadline.Token)).ShouldBe(0);
            if (attempt < 4) await Task.Delay(TimeSpan.FromMilliseconds(1100), deadline.Token);
        }
        state.Calls.ShouldBe(4);
        message.Status.ShouldBe(OutboxStatus.DeadLettered);
        await using var scope = provider.CreateAsyncScope();
        var result = await scope.ServiceProvider.GetRequiredService<IDispatcher>().DispatchAsync(new OwnedRetryEvent("foreground-result"), deadline.Token);
        result.Succeeded.ShouldBeFalse();
        state.Calls.ShouldBe(7, "foreground failed results retain their own three-attempt middleware budget");
        result.ProblemDetails!.Detail.ShouldBe("CONTROLLED-FAILED-RESULT");
    }

    [Fact]
    public async Task Retain_A_Missing_Handler_Failure_Without_Claiming_Delivery()
    {
        var state = new FaultState();
        await using var provider = Build(state);
        var store = provider.GetRequiredService<IOutboxStore>();
        MessageTypeRegistry.RegisterType<MissingHandlerAction>();
        var message = new OutboundMessage(nameof(MissingHandlerAction), JsonSerializer.SerializeToUtf8Bytes(new MissingHandlerAction("missing")), "local");
        await store.StageMessageAsync(message, CancellationToken.None);
        var processor = provider.GetRequiredService<IOutboxProcessor>();
        processor.Init("missing-handler-owner-test");
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        (await processor.DispatchPendingMessagesAsync(deadline.Token)).ShouldBe(0);
        message.Status.ShouldBe(OutboxStatus.Failed);
        message.RetryCount.ShouldBe(1);
        message.LastError.ShouldContain(nameof(MissingHandlerAction));
        state.Calls.ShouldBe(0);
    }

    [Fact]
    public async Task Retain_Independent_Retry_Budget_For_A_Nested_Different_Message()
    {
        var state = new FaultState { Nest = true };
        await using var provider = Build(state);
        var store = provider.GetRequiredService<IOutboxStore>();
        MessageTypeRegistry.RegisterType<OwnedRetryEvent>();
        var message = new OutboundMessage(nameof(OwnedRetryEvent), JsonSerializer.SerializeToUtf8Bytes(new OwnedRetryEvent("outer")), "local");
        await store.StageMessageAsync(message, CancellationToken.None);
        var processor = provider.GetRequiredService<IOutboxProcessor>();
        processor.Init("nested-owner-test");
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        (await processor.DispatchPendingMessagesAsync(deadline.Token)).ShouldBe(1);
        state.Calls.ShouldBe(1);
        state.NestedCalls.ShouldBe(3);
        state.NestedExhausted.ShouldBeTrue();
        message.Status.ShouldBe(OutboxStatus.Sent);
    }

    [Fact]
    public async Task Keep_Parallel_Rows_Isolated_From_Each_Others_Ownership()
    {
        var state = new FaultState();
        await using var provider = Build(state, parallel: true);
        var store = provider.GetRequiredService<IOutboxStore>();
        MessageTypeRegistry.RegisterType<OwnedRetryEvent>();
        var messages = Enumerable.Range(0, 2).Select(index => new OutboundMessage(
            nameof(OwnedRetryEvent), JsonSerializer.SerializeToUtf8Bytes(new OwnedRetryEvent($"row-{index}")), "local")).ToArray();
        foreach (var message in messages) await store.StageMessageAsync(message, CancellationToken.None);
        var processor = provider.GetRequiredService<IOutboxProcessor>();
        processor.Init("parallel-owner-test");
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        (await processor.DispatchPendingMessagesAsync(deadline.Token)).ShouldBe(0);
        state.Calls.ShouldBe(2);
        foreach (var message in messages)
        {
            message.Status.ShouldBe(OutboxStatus.Failed);
            message.RetryCount.ShouldBe(1);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Propagate_Cancellation_Without_Another_Handler_Attempt(bool parallel)
    {
        var state = new FaultState { WaitForCancellation = true };
        await using var provider = Build(state, parallel);
        var store = provider.GetRequiredService<IOutboxStore>();
        MessageTypeRegistry.RegisterType<OwnedRetryEvent>();
        var message = new OutboundMessage(nameof(OwnedRetryEvent), JsonSerializer.SerializeToUtf8Bytes(new OwnedRetryEvent("cancel")), "local");
        await store.StageMessageAsync(message, CancellationToken.None);
        var processor = provider.GetRequiredService<IOutboxProcessor>();
        processor.Init("cancel-owner-test");
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var running = processor.DispatchPendingMessagesAsync(cancellation.Token);
        await state.Entered.Task.WaitAsync(TimeSpan.FromSeconds(3));
        await cancellation.CancelAsync();
        await Should.ThrowAsync<OperationCanceledException>(() => running);
        state.Calls.ShouldBe(1);
        message.Status.ShouldNotBe(OutboxStatus.Sent);
        message.Status.ShouldNotBe(OutboxStatus.DeadLettered);
        message.RetryCount.ShouldBe(0, "caller cancellation is not a retry-consuming delivery failure");
    }

    [Fact]
    public async Task Keep_One_Handler_Attempt_Per_Persisted_Outbox_Attempt()
    {
        var state = new FaultState();
        await using var provider = Build(state);
        var store = provider.GetRequiredService<IOutboxStore>();
        MessageTypeRegistry.RegisterType<OwnedRetryEvent>();
        var message = new OutboundMessage(nameof(OwnedRetryEvent), JsonSerializer.SerializeToUtf8Bytes(new OwnedRetryEvent("outbox")), "local");
        await store.StageMessageAsync(message, CancellationToken.None);
        var processor = provider.GetRequiredService<IOutboxProcessor>();
        processor.Init("persisted-owner-test");
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        for (var attempt = 1; attempt <= 4; attempt++)
        {
            (await processor.DispatchPendingMessagesAsync(deadline.Token)).ShouldBe(0);
            message.Status.ShouldBe(attempt < 4 ? OutboxStatus.Failed : OutboxStatus.DeadLettered);
            if (attempt < 4) await Task.Delay(TimeSpan.FromMilliseconds(1100), deadline.Token);
        }
        state.Calls.ShouldBe(4, "the persisted four-attempt budget must not multiply by three pipeline attempts");
        (await processor.DispatchPendingMessagesAsync(deadline.Token)).ShouldBe(0);
        state.Calls.ShouldBe(4);
    }

    [Fact]
    public async Task Retain_Foreground_Middleware_Retries()
    {
        var state = new FaultState();
        await using var provider = Build(state);
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await using var scope = provider.CreateAsyncScope();
        await Should.ThrowAsync<IOException>(() => scope.ServiceProvider.GetRequiredService<IDispatcher>()
            .DispatchAsync(new OwnedRetryEvent("foreground"), deadline.Token));
        state.Calls.ShouldBe(3);
    }

    private static ServiceProvider Build(FaultState state, bool parallel = false, bool? batchProcessing = null)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<Excalibur.Dispatch.ErrorHandling.IDeadLetterQueue>(Excalibur.Dispatch.ErrorHandling.NullDeadLetterQueue.Instance);
        if (state.Classifier is { } classifier) services.AddSingleton(classifier);
        services.AddSingleton(new FailedResultMiddleware(state));
        if (state.Fanout)
        {
            services.AddSingleton(new FanoutRoutesMiddleware());
            var buses = new Dictionary<string, IMessageBus>(StringComparer.OrdinalIgnoreCase);
            for (var index = 0; index < 2; index++)
            {
                var route = index;
                var bus = A.Fake<IMessageBus>();
                A.CallTo(() => bus.PublishAsync(A<IDispatchEvent>._, A<IMessageContext>._, A<CancellationToken>._))
                    .ReturnsLazily(() =>
                    {
                        Interlocked.Increment(ref state.RouteCalls[route]);
                        Exception fault = state.MixedFanout && route == 1 ? new IOException("CONTROLLED-TYPED-TRANSIENT")
                            : new ConfigurationException("CONTROLLED-TYPED-PERMANENT-ROUTE");
                        return Task.FromException(fault);
                    });
                buses["classified-route-" + index] = bus;
            }
            services.AddSingleton<IMessageBusProvider>(new ControlledBusProvider(buses));
        }
        services.AddDispatch(dispatch =>
        {
            dispatch.UseMiddleware<RetryMiddleware>();
            if (state.Fanout) dispatch.UseMiddleware<FanoutRoutesMiddleware>();
            if (state.FailedResult) dispatch.UseMiddleware<FailedResultMiddleware>();
        });
        services.Configure<RetryOptions>(options =>
        {
            options.MaxRetryAttempts = 2;
            options.BaseDelay = TimeSpan.Zero;
            options.MaxDelay = TimeSpan.FromMilliseconds(1);
            options.UseJitter = false;
            options.RetryableExceptions.Add(typeof(IOException));
        });
        services.AddSingleton(provider => new FaultHandler(state, provider.GetRequiredService<IServiceScopeFactory>()));
        services.AddExcalibur(builder => builder.AddOutbox(outbox => outbox.UseInMemory().WithProcessing(processing =>
            processing.MaxRetryCount(3).PollingInterval(TimeSpan.FromMilliseconds(20)).RetryDelay(TimeSpan.FromMilliseconds(10)))));
        services.Configure<InMemoryOutboxOptions>(options => options.FailureBackoffFloorSeconds = 1);
        if (parallel) services.Configure<Excalibur.Dispatch.Options.Delivery.OutboxDeliveryOptions>(options => options.BatchProcessing.ParallelProcessingDegree = 2);
        if (batchProcessing is { } batch) services.Configure<Excalibur.Dispatch.Options.Delivery.OutboxDeliveryOptions>(options => options.EnableBatchDatabaseOperations = batch);
        var provider = services.BuildServiceProvider();
        provider.GetRequiredService<IHandlerRegistry>().Register(typeof(OwnedRetryEvent), typeof(FaultHandler), expectsResponse: false);
        provider.GetRequiredService<IHandlerRegistry>().Register(typeof(NestedRetryEvent), typeof(FaultHandler), expectsResponse: false);
        return provider;
    }

    private sealed record OwnedRetryEvent(string Value) : IIntegrationEvent;
    private sealed record NestedRetryEvent : IIntegrationEvent;
    private sealed record MissingHandlerAction(string Value) : IDispatchAction;
    private sealed class FaultState
    {
        internal int Calls;
        internal int NestedCalls;
        internal bool Nest;
        internal bool NestedExhausted;
        internal bool WaitForCancellation;
        internal bool FailedResult;
        internal Exception? TypedFault;
        internal IMessageFailureClassifier? Classifier;
        internal CancellationTokenSource? WrappedCallerCancellation;
        internal bool Fanout;
        internal bool MixedFanout;
        internal int[] RouteCalls { get; } = [0, 0];
        internal TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    }
    private sealed class FailedResultMiddleware(FaultState state) : IDispatchMiddleware
    {
        public DispatchMiddlewareStage? Stage => DispatchMiddlewareStage.End;
        public ValueTask<IMessageResult> InvokeAsync(IDispatchMessage message, IMessageContext context,
            DispatchRequestDelegate nextDelegate, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Interlocked.Increment(ref state.Calls);
            return ValueTask.FromResult<IMessageResult>(Excalibur.Dispatch.MessageResult.Failed(new MessageProblemDetails
            {
                Status = 503,
                Title = "Controlled failure",
                Detail = "CONTROLLED-FAILED-RESULT",
            }));
        }
    }
    private sealed class FaultHandler(FaultState state, IServiceScopeFactory scopes) : IEventHandler<OwnedRetryEvent>, IEventHandler<NestedRetryEvent>
    {
        public async Task HandleAsync(OwnedRetryEvent message, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Interlocked.Increment(ref state.Calls);
            if (state.WrappedCallerCancellation is { } caller)
            {
                await caller.CancelAsync();
                throw new IOException("transport wrapped caller cancellation", new OperationCanceledException(cancellationToken));
            }
            if (state.TypedFault is { } typed) { throw typed; }
            if (state.WaitForCancellation)
            {
                state.Entered.TrySetResult();
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            }
            if (state.Nest)
            {
                await using var scope = scopes.CreateAsyncScope();
                try
                {
                    await scope.ServiceProvider.GetRequiredService<IDispatcher>().DispatchAsync(new NestedRetryEvent(), cancellationToken);
                }
                catch (IOException) { state.NestedExhausted = true; }
                return;
            }
            throw new IOException("CONTROLLED-BEFORE-EFFECT");
        }

        public Task HandleAsync(NestedRetryEvent message, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref state.NestedCalls);
            return Task.FromException(new IOException("CONTROLLED-NESTED-BEFORE-EFFECT"));
        }
    }

    private sealed class FanoutRoutesMiddleware : IDispatchMiddleware
    {
        public DispatchMiddlewareStage? Stage => DispatchMiddlewareStage.Routing;
        public ValueTask<IMessageResult> InvokeAsync(IDispatchMessage message, IMessageContext context,
            DispatchRequestDelegate nextDelegate, CancellationToken cancellationToken)
        {
            context.GetOrCreateRoutingFeature().RoutingDecision =
                RoutingDecision.Success("classified-route-0", ["classified-route-0", "classified-route-1"]);
            return nextDelegate(message, context, cancellationToken);
        }
    }

    private sealed class ControlledBusProvider(Dictionary<string, IMessageBus> buses) : IMessageBusProvider
    {
        public IMessageBus? GetMessageBus(string name) => buses.GetValueOrDefault(name);
        public IEnumerable<IMessageBus> GetAllMessageBuses() => buses.Values;
        public IEnumerable<string> GetAllMessageBusNames() => buses.Keys;
        public IMessageBus? GetRemoteMessageBus(string name) => GetMessageBus(name);
        public bool TryGet(string name, out IMessageBus? bus) => buses.TryGetValue(name, out bus);
    }
}
