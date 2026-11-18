// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Text.Json;
using Excalibur.Dispatch;
using Excalibur.Dispatch.Delivery;
using Excalibur.Dispatch.Delivery.Registry;
using Excalibur.Dispatch.Messaging;
using Excalibur.Dispatch.ErrorHandling;
using Excalibur.Outbox.InMemory;
using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.Outbox.Tests;

[Trait("Category", "Unit")]
[Trait("Component", "Outbox")]
[Trait("Pattern", "Regression")]
[Collection(SchedulingSensitiveCollection.Name)]
public sealed class OutboxUnknownRetainedSourceShould
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PersistUnknownThenHoldAcrossFreshProcessorsUntilExplicitClaimScopedRelease(bool parallel)
    {
        var state = new State();
        await using var provider = Build(state, parallel);
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(20)); // deadline-ok: bounds all real-store acquisitions and drains
        var token = deadline.Token;
        var held = await Stage(provider, "unknown", token);
        var bytes = (byte[])held.Payload.Clone();
        await Drain(provider, "first", token);
        state.UnknownCalls.ShouldBe(1);
        AssertHeld(provider, held.Id, bytes, 1);
        var failed = await provider.GetRequiredService<IOutboxStoreAdmin>().GetAllTenantsFailedMessagesAsync(0, null, 10, token);
        failed.Single().Id.ShouldBe(held.Id);
        failed.Single().TenantId.ShouldBe("tenant-unknown");

        await WaitForStoreEligibility(token);
        var healthy = await Stage(provider, "healthy", token);
        await Drain(provider, "fresh", token);
        state.UnknownCalls.ShouldBe(1, "a fresh processor must recognize persisted evidence before sending");
        state.HealthyCalls.ShouldBe(1);
        AssertHeld(provider, held.Id, bytes, 1);
        PersistedInMemoryOutboxState.Read(provider, healthy.Id).Status.ShouldBe(OutboxStatus.Sent);

        // Low-level operator composition: drains stopped; acquire fresh authority, review, then
        // replace only the marker. A stale acquisition cannot clear the successor's hold.
        await WaitForStoreEligibility(token);
        var store = provider.GetRequiredService<IOutboxStore>();
        var stale = (await store.GetUnsentMessagesAsync(10, token)).Single();
        await WaitForStoreEligibility(token);
        var current = (await store.GetUnsentMessagesAsync(10, token)).Single();
        current.DispatcherId.ShouldNotBe(stale.DispatcherId);
        var claims = (IClaimScopedOutboxStore)store.GetService(typeof(IClaimScopedOutboxStore))!;
        (await claims.MarkFailedAsync(held.Id, "reviewed retryable release", 1, stale.DispatcherId!, token))
            .ShouldBe(OutboxCompletionOutcome.ClaimLost);
        AssertHeld(provider, held.Id, bytes, 1);
        (await claims.MarkFailedAsync(held.Id, "reviewed retryable release", 1, current.DispatcherId!, token))
            .ShouldBe(OutboxCompletionOutcome.Applied);
        state.AllowUnknown = true;
        await WaitForStoreEligibility(token);
        await Drain(provider, "operator-released", token);
        state.UnknownCalls.ShouldBe(2);
        var sent = PersistedInMemoryOutboxState.Read(provider, held.Id);
        sent.Status.ShouldBe(OutboxStatus.Sent);
        sent.RetryCount.ShouldBe(1, "manual release must not reset the consumed logical attempt count");
        sent.Payload.ShouldBe(bytes);
    }

    [Theory]
    [InlineData(false, "Unknown", true)]
    [InlineData(true, "Unknown", true)]
    [InlineData(false, "MaxRetriesExceeded", false)]
    [InlineData(true, "MaxRetriesExceeded", false)]
    public async Task RecognizeOnlyExactHeldPairBeforeExhaustion(bool parallel, string reason, bool held)
    {
        var state = new State();
        await using var provider = Build(state, parallel);
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10)); // deadline-ok: bounds the seeded provider write and drain
        var token = deadline.Token;
        var message = await Stage(provider, "unknown", token);
        var store = provider.GetRequiredService<IOutboxStore>();
        var acquired = (await store.GetUnsentMessagesAsync(10, token)).Single();
        var evidence = new OutboxFailureEvidence { Attempts = 2, Classification = "Unknown", Reason = reason, Error = "CONTROLLED-UNKNOWN" };
        var claims = (IClaimScopedOutboxStore)store.GetService(typeof(IClaimScopedOutboxStore))!;
        (await claims.MarkFailedAsync(message.Id, evidence.Encode(), 2, acquired.DispatcherId!, token))
            .ShouldBe(OutboxCompletionOutcome.Applied);
        await WaitForStoreEligibility(token);
        await Drain(provider, "persisted-at-budget", token);
        state.UnknownCalls.ShouldBe(0, "persisted disposition is decided before decoding or dispatch");
        var persisted = PersistedInMemoryOutboxState.Read(provider, message.Id);
        persisted.Status.ShouldBe(held ? OutboxStatus.Failed : OutboxStatus.DeadLettered);
        persisted.RetryCount.ShouldBe(2);
        if (held) persisted.LastError.ShouldBe(evidence.Encode());
    }

    private static void AssertHeld(ServiceProvider provider, string id, byte[] bytes, int attempts)
    {
        var saved = PersistedInMemoryOutboxState.Read(provider, id);
        saved.Status.ShouldBe(OutboxStatus.Failed);
        saved.RetryCount.ShouldBe(attempts);
        saved.Payload.ShouldBe(bytes);
        saved.TenantId.ShouldBe("tenant-unknown");
        OutboxFailureEvidence.TryRead(saved.LastError, out var evidence).ShouldBeTrue();
        evidence.ShouldNotBeNull();
        evidence.Classification.ShouldBe("Unknown");
        evidence.Reason.ShouldBe("Unknown");
        evidence.Attempts.ShouldBe(attempts);
        evidence.Error.ShouldContain("CONTROLLED-UNKNOWN");
    }

    private static async Task<OutboundMessage> Stage(ServiceProvider provider, string value, CancellationToken token)
    {
        MessageTypeRegistry.RegisterType<ProbeEvent>();
        var message = new OutboundMessage(nameof(ProbeEvent), JsonSerializer.SerializeToUtf8Bytes(new ProbeEvent(value)), "local")
        { TenantId = "tenant-unknown" };
        await provider.GetRequiredService<IOutboxStore>().StageMessageAsync(message, token);
        return message;
    }

    private static async Task Drain(ServiceProvider provider, string owner, CancellationToken token)
    {
        var processor = provider.GetRequiredService<IOutboxProcessor>();
        processor.Init(owner);
        await processor.DispatchPendingMessagesAsync(token);
    }

    private static Task WaitForStoreEligibility(CancellationToken token)
    {
        return Task.Delay(TimeSpan.FromMilliseconds(1100), token); // delay-ok: The real InMemory retry floor and one-second lease expiry must elapse; subsequent eligibility and claim outcomes remain the witnesses.
    }

    private static ServiceProvider Build(State state, bool parallel)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IDeadLetterQueue>(NullDeadLetterQueue.Instance);
        services.AddSingleton<IMessageFailureClassifier>(new UnknownClassifier());
        services.AddSingleton(new ControlledDispatch(state));
        services.AddDispatch(dispatch => dispatch.UseMiddleware<ControlledDispatch>());
        services.AddExcalibur(builder => builder.AddOutbox(outbox => outbox.UseInMemory()
            .WithProcessing(processing => processing.PollingInterval(TimeSpan.FromMilliseconds(20)))));
        services.Configure<InMemoryOutboxOptions>(options =>
        {
            options.FailureBackoffFloorSeconds = 1;
            options.LeaseTimeoutSeconds = 1;
        });
        services.Configure<Excalibur.Dispatch.Options.Delivery.OutboxDeliveryOptions>(options =>
        {
            options.MaxAttempts = 2;
            options.EnableBatchDatabaseOperations = parallel;
            options.BatchProcessing.ParallelProcessingDegree = parallel ? 2 : 1;
        });
        return services.BuildServiceProvider();
    }

    private sealed record ProbeEvent(string Value) : IDispatchEvent;
    private sealed class State
    {
        internal int UnknownCalls;
        internal int HealthyCalls;
        internal bool AllowUnknown;
    }
    private sealed class UnknownClassifier : IMessageFailureClassifier
    {
        // Current-API RED calibration uses the future numeric slot without requiring a new enum name.
        public MessageFailureKind Classify(Exception exception) => (MessageFailureKind)4;
    }
    private sealed class ControlledDispatch(State state) : IDispatchMiddleware
    {
        public DispatchMiddlewareStage? Stage => DispatchMiddlewareStage.End;
        public ValueTask<IMessageResult> InvokeAsync(IDispatchMessage message, IMessageContext context,
            DispatchRequestDelegate nextDelegate, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (((ProbeEvent)message).Value == "unknown")
            {
                Interlocked.Increment(ref state.UnknownCalls);
                if (!state.AllowUnknown) return ValueTask.FromException<IMessageResult>(new Exception("CONTROLLED-UNKNOWN"));
            }
            else Interlocked.Increment(ref state.HealthyCalls);
            return ValueTask.FromResult<IMessageResult>(Excalibur.Dispatch.MessageResult.Success());
        }
    }
}
