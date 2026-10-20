// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Collections.Concurrent;
using System.Text.Json;
using Excalibur.Dispatch;
using Excalibur.Dispatch.Delivery;
using Excalibur.Dispatch.Delivery.Registry;
using Excalibur.Dispatch.ErrorHandling;
using Excalibur.Outbox.InMemory;
using DeliveryOptions = Excalibur.Dispatch.Options.Delivery.OutboxDeliveryOptions;

namespace Excalibur.Outbox.Tests;

[Trait("Category", "Unit")]
[Trait("Component", "Outbox")]
[Trait("Pattern", "Regression")]
public sealed class OutboxRetryBudgetDuringDeadLetterOutageShould
{
    [Theory]
    [InlineData(1, false)]
    [InlineData(2, false)]
    [InlineData(1, true)]
    [InlineData(2, true)]
    public async Task ResumeKnownTerminalDispositionWithoutAnotherDecodeOrDispatch(int degree, bool poison)
    {
        var state = new Control { Permanent = !poison };
        await using var provider = Build(state);
        var row = await StageAsync(provider.GetRequiredService<IOutboxStore>(), "failed");
        if (poison)
        {
            row.Payload = "not-json"u8.ToArray();
        }
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        await using (var first = Create(provider, state, degree, true))
        {
            first.Init("terminal-first");
            await first.DispatchPendingMessagesAsync(deadline.Token);
        }
        row.RetryCount.ShouldBe(poison ? 0 : 1);
        row.LastError.ShouldNotBeNull();
        row.Status.ShouldBe(OutboxStatus.Failed);
        // A repaired body makes a second decode/dispatch observable; disposition evidence must take precedence.
        if (poison)
        {
            row.Payload = JsonSerializer.SerializeToUtf8Bytes(new BudgetEvent("healthy"));
        }
        state.QueueAvailable = true;
        await Task.Delay(TimeSpan.FromMilliseconds(1100), deadline.Token); // delay-ok: the retry floor must actually elapse before the successor's attempt is due
        await using (var successor = Create(provider, state, degree, true))
        {
            successor.Init("terminal-successor");
            await successor.DispatchPendingMessagesAsync(deadline.Token);
        }
        state.FailedCalls.ShouldBe(poison ? 0 : 1);
        state.HealthyCalls.ShouldBe(0);
        row.Status.ShouldBe(OutboxStatus.DeadLettered);
        var retained = state.Retained.Single();
        retained.Attempts.ShouldBe(poison ? "0" : "1");
        retained.Reason.ShouldBe(poison ? DeadLetterReason.DeserializationFailed : DeadLetterReason.UnhandledException);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public async Task ReportObservedAttemptOnAnImmediatelySuccessfulTransfer(int degree)
    {
        var state = new Control { Permanent = true, QueueAvailable = true };
        await using var provider = Build(state);
        var row = await StageAsync(provider.GetRequiredService<IOutboxStore>(), "failed");
        await using var processor = Create(provider, state, degree, true);
        processor.Init("immediate-transfer");
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await processor.DispatchPendingMessagesAsync(deadline.Token);
        state.Retained.Single().Attempts.ShouldBe("1");
        row.Status.ShouldBe(OutboxStatus.DeadLettered);
        row.RetryCount.ShouldBe(1);
        state.FailedCalls.ShouldBe(1);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public async Task NeverResurrectATerminalRowWhenTransferCommitsThenThrows(int degree)
    {
        var state = new Control { Permanent = true, CommitBeforeThrow = true };
        await using var provider = Build(state);
        var row = await StageAsync(provider.GetRequiredService<IOutboxStore>(), "failed");
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        for (var drain = 0; drain < 2; drain++)
        {
            await using var processor = Create(provider, state, degree, true);
            processor.Init("ambiguous-terminal-" + drain);
            await processor.DispatchPendingMessagesAsync(deadline.Token);
            row.Status.ShouldBe(OutboxStatus.DeadLettered);
        }
        state.FailedCalls.ShouldBe(1);
        state.QueueCalls.ShouldBe(1);
    }
    [Theory]
    [InlineData(1, false)]
    [InlineData(1, true)]
    [InlineData(2, false)]
    [InlineData(2, true)]
    public async Task PreserveTheExhaustedBudgetAcrossQueueOutageAndProcessorRecreation(int degree, bool batch)
    {
        var state = new Control();
        await using var provider = Build(state);
        var store = provider.GetRequiredService<IOutboxStore>();
        var failed = await StageAsync(store, "failed");
        var healthy = await StageAsync(store, "healthy");
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(20));

        // Every drain uses a new processor over the SAME actual store: a local field cannot enforce this budget.
        for (var drain = 0; drain < 4; drain++)
        {
            await using var processor = Create(provider, state, degree, batch);
            processor.Init("retry-budget-" + drain);
            await processor.DispatchPendingMessagesAsync(deadline.Token);
            if (drain < 3)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(1100), deadline.Token); // delay-ok: the retry floor must actually elapse before the successor's attempt is due
            }
        }

        state.FailedCalls.ShouldBe(2, "queue recovery attempts must not resend an exhausted delivery");
        failed.RetryCount.ShouldBe(2, "the actual final failure must be recorded before queue transfer can fail");
        failed.Status.ShouldBe(OutboxStatus.Failed);
        failed.Payload.ShouldBe(JsonSerializer.SerializeToUtf8Bytes(new BudgetEvent("failed")));
        failed.TenantId.ShouldBe("budget-tenant");
        healthy.Status.ShouldBe(OutboxStatus.Sent, "one unavailable dead-letter transfer must not strand an independent healthy row");
        state.HealthyCalls.ShouldBe(1);
        state.QueueCalls.ShouldBeGreaterThanOrEqualTo(2, "successor drains must still attempt recovery transfer");
        state.Retained.ShouldBeEmpty();

        state.QueueAvailable = true;
        await Task.Delay(TimeSpan.FromMilliseconds(1100), deadline.Token); // delay-ok: the retry floor must actually elapse before the successor's attempt is due
        await using (var recovered = Create(provider, state, degree, batch))
        {
            recovered.Init("recovered-budget");
            await recovered.DispatchPendingMessagesAsync(deadline.Token);
        }
        state.FailedCalls.ShouldBe(2);
        failed.Status.ShouldBe(OutboxStatus.DeadLettered);
        var retained = state.Retained.Single();
        retained.Payload.ShouldBe(failed.Payload);
        retained.Tenant.ShouldBe("budget-tenant");
        retained.Attempts.ShouldBe("2");
        retained.Reason.ShouldBe(DeadLetterReason.MaxRetriesExceeded);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public async Task LeaveCallerCancellationOutsideThePersistedFailureBudget(int degree)
    {
        using var caller = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var state = new Control { Caller = caller };
        await using var provider = Build(state);
        var row = await StageAsync(provider.GetRequiredService<IOutboxStore>(), "failed");
        await using var processor = Create(provider, state, degree, true);
        processor.Init("cancelled-budget");
        await Should.ThrowAsync<OperationCanceledException>(() => processor.DispatchPendingMessagesAsync(caller.Token));
        state.FailedCalls.ShouldBe(1);
        row.RetryCount.ShouldBe(0);
        state.QueueCalls.ShouldBe(0);
        row.Status.ShouldNotBe(OutboxStatus.DeadLettered);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public async Task LeaveOpenCircuitOutsideThePersistedFailureBudget(int degree)
    {
        var state = new Control();
        await using var provider = Build(state);
        var row = await StageAsync(provider.GetRequiredService<IOutboxStore>(), "failed");
        var circuit = A.Fake<ICircuitBreakerPolicy>();
        A.CallTo(() => circuit.State).Returns(CircuitState.Open);
        var registry = A.Fake<ITransportCircuitBreakerRegistry>();
        A.CallTo(() => registry.GetOrCreate(A<string>._)).Returns(circuit);
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        for (var drain = 0; drain < 2; drain++)
        {
            await using var processor = Create(provider, state, degree, true, registry);
            processor.Init("open-budget-" + drain);
            await processor.DispatchPendingMessagesAsync(deadline.Token);
            if (drain == 0)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(1100), deadline.Token); // delay-ok: the retry floor must actually elapse before the successor's attempt is due
            }
        }
        state.FailedCalls.ShouldBe(0);
        row.RetryCount.ShouldBe(0);
        state.QueueCalls.ShouldBe(0);
        row.Status.ShouldBe(OutboxStatus.Failed);
    }

    private static ServiceProvider Build(Control state)
    {
        MessageTypeRegistry.RegisterType<BudgetEvent>();
        var dispatcher = A.Fake<IDispatcher>();
        A.CallTo(() => dispatcher.DispatchAsync(A<IDispatchMessage>._, A<IMessageContext>._, A<CancellationToken>._))
            .ReturnsLazily((IDispatchMessage message, IMessageContext _, CancellationToken token) =>
            {
                if (((BudgetEvent)message).Value == "healthy")
                {
                    Interlocked.Increment(ref state.HealthyCalls);
                    return Task.FromResult<IMessageResult>(Excalibur.Dispatch.MessageResult.Success());
                }
                Interlocked.Increment(ref state.FailedCalls);
                if (state.Caller is { } caller)
                {
                    caller.Cancel();
                    return Task.FromException<IMessageResult>(new OperationCanceledException(token));
                }
                return Task.FromException<IMessageResult>(state.Permanent
                    ? new Excalibur.Dispatch.Exceptions.ConfigurationException("CONTROLLED-PERMANENT-FAILURE")
                    : new IOException("CONTROLLED-DELIVERY-FAILURE"));
            });
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(dispatcher);
        services.Configure<Excalibur.Outbox.Outbox.OutboxProcessingOptions>(options => options.PollingInterval = TimeSpan.FromMilliseconds(100));
        services.AddInMemoryOutboxStore(options =>
        {
            options.LeaseTimeoutSeconds = 1;
            options.FailureBackoffFloorSeconds = 1;
        });
        return services.BuildServiceProvider();
    }

    private static async Task<OutboundMessage> StageAsync(IOutboxStore store, string value)
    {
        var row = new OutboundMessage(typeof(BudgetEvent).AssemblyQualifiedName!,
            JsonSerializer.SerializeToUtf8Bytes(new BudgetEvent(value)), "local") { TenantId = "budget-tenant" };
        await store.StageMessageAsync(row, CancellationToken.None);
        return row;
    }

    private static OutboxProcessor Create(ServiceProvider provider, Control state, int degree, bool batch,
        ITransportCircuitBreakerRegistry? registry = null)
    {
        var queue = A.Fake<IDeadLetterQueue>();
        A.CallTo(() => queue.EnqueueAsync(A<IOutboxMessage>._, A<DeadLetterReason>._, A<CancellationToken>._,
                A<Exception?>._, A<IDictionary<string, string>?>._))
            .ReturnsLazily(async (IOutboxMessage message, DeadLetterReason reason, CancellationToken token,
                Exception? _, IDictionary<string, string>? metadata) =>
            {
                token.ThrowIfCancellationRequested();
                Interlocked.Increment(ref state.QueueCalls);
                if (state.CommitBeforeThrow)
                {
                    var terminal = (IDeadLetterableOutboxStore)provider.GetRequiredService<IOutboxStore>();
                    await terminal.MarkDeadLetteredAsync(message.MessageId, "CONTROLLED-COMMITTED-THEN-THREW", message.Attempts, token);
                    throw new IOException("CONTROLLED-AMBIGUOUS-TRANSFER");
                }
                if (!state.QueueAvailable)
                {
                    throw new IOException("CONTROLLED-DLQ-OUTAGE");
                }
                state.Retained.Enqueue(new Retained(message.MessageBody.ToArray(), message.TenantId, metadata!["Attempts"], reason));
                return Guid.NewGuid();
            });
        var backoff = A.Fake<IBackoffCalculator>();
        A.CallTo(() => backoff.CalculateDelay(A<int>._)).Returns(TimeSpan.Zero);
        return new OutboxProcessor(Microsoft.Extensions.Options.Options.Create(new DeliveryOptions
        {
            QueueCapacity = 10,
            ProducerBatchSize = 10,
            ConsumerBatchSize = 10,
            PerRunTotal = 10,
            MaxAttempts = 2,
            EnableBatchDatabaseOperations = batch,
            BatchProcessing = { ParallelProcessingDegree = degree },
        }), provider.GetRequiredService<IOutboxStore>(), new DispatchJsonSerializer(), provider,
            NullLogger<OutboxProcessor>.Instance, registry ?? PassThroughCircuitBreakerRegistry.Instance,
            deadLetterQueue: queue, backoffCalculator: backoff);
    }

    private sealed record BudgetEvent(string Value) : IIntegrationEvent;
    private sealed record Retained(byte[] Payload, string? Tenant, string Attempts, DeadLetterReason Reason);
    private sealed class Control
    {
        internal int FailedCalls;
        internal int HealthyCalls;
        internal int QueueCalls;
        internal bool QueueAvailable;
        internal bool Permanent;
        internal bool CommitBeforeThrow;
        internal CancellationTokenSource? Caller;
        internal ConcurrentQueue<Retained> Retained { get; } = new();
    }
}
