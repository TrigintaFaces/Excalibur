// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch;
using Excalibur.Dispatch.Features;
using Excalibur.Outbox.Marten;
using global::Marten;
using Shouldly;
using Xunit;

namespace Excalibur.Integration.Tests.Data.Outbox;

public sealed partial class MartenOutboxEnqueueTransformShould
{
    private static readonly byte[] ExpectedTransformed = [37, 0, 255];
    private static readonly byte[] CanceledReturn = [9];
    [Fact]
    public async Task FreezeTheMartenEnvelopeBeforeCallbackAndOwnItsResultBeforeBatchSerialization()
    {
        byte[] callbackResult = [37, 0, 255];
        var listener = new TransformSaveListener(() => callbackResult[0] = 99);
        await RunAsync(null, async harness =>
        {
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(45));
            var input = new InputMessage(); var context = Context(harness, input, "ignored-context-id");
            context.SetDestination("prepared-destination"); var calls = 0;
            await RequireMartenTransform(harness.Raw).EnqueueWithPayloadTransformAsync(input, context, async (bytes, tenant, token) =>
            {
                calls++; bytes.ToArray().ShouldBe(DefaultPayload); tenant.ShouldBe("before-tenant"); token.ShouldBe(deadline.Token);
                listener.Calls.ShouldBe(0); (await harness.ReadAsync(token)).ShouldBeEmpty();
                context.MessageId = "changed-id"; context.CorrelationId = "changed-correlation"; context.CausationId = "changed-cause";
                context.SetDestination("changed-destination"); context.GetOrCreateIdentityFeature().TenantId = "changed-tenant";
                bytes.ToArray().ShouldBe(DefaultPayload, "borrowed memory is inspected only during invocation");
                return callbackResult;
            }, deadline.Token);
            calls.ShouldBe(1); listener.Calls.ShouldBe(1); callbackResult[0].ShouldBe((byte)99);
            var row = (await harness.ReadAsync(deadline.Token)).Single();
            AssertEnvelope(row, "ignored-context-id", "prepared-destination"); Payload(row).ShouldBe(ExpectedTransformed);
        }, listener);
    }
    [Fact]
    public async Task ObserveThePlainStageArrayMutationAtTheSamePreSerializationListener()
    {
        byte[] original = [37, 0, 255];
        var listener = new TransformSaveListener(() => original[0] = 99);
        await RunAsync(null, async harness =>
        {
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(45));
            await harness.Store.StageMessageAsync(new OutboundMessage("alias-proof", original, "destination")
            { Id = Guid.NewGuid().ToString(), TenantId = "before-tenant", CorrelationId = "before-correlation" }, deadline.Token);
            listener.Calls.ShouldBe(1); original[0].ShouldBe((byte)99);
            var stored = Payload((await harness.ReadAsync(deadline.Token)).Single());
            stored.Length.ShouldBe(3); stored[0].ShouldBe((byte)99); stored[1].ShouldBe((byte)0); stored[2].ShouldBe((byte)255);
            // This positive establishes the listener observes the original array before actual Marten
            // batch serialization. It is a controlled live-call alias, not a new caller ownership rule.
        }, listener);
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)] [InlineData(4)] [InlineData(5)]
    public async Task RefuseMartenTransformFaultNullAndCancellationBeforeSaving(int outcome)
    {
        var listener = new TransformSaveListener(null);
        await RunAsync(null, async harness =>
        {
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(45));
            using var operation = CancellationTokenSource.CreateLinkedTokenSource(deadline.Token);
            var input = new InputMessage(); var context = Context(harness, input, "ignored-context-id"); var calls = 0;
            var fault = new IOException("transform-fault");
            Func<ReadOnlyMemory<byte>, string?, CancellationToken, ValueTask<byte[]>>? callback = (bytes, tenant, token) =>
            {
                calls++; bytes.ToArray().ShouldBe(DefaultPayload); tenant.ShouldBe("before-tenant"); token.ShouldBe(operation.Token);
                if (outcome == 0) { return ValueTask.FromException<byte[]>(fault); }
                if (outcome == 1) { return ValueTask.FromResult<byte[]>(null!); }
                operation.Cancel();
                return outcome == 2 ? new ValueTask<byte[]>(Task.FromCanceled<byte[]>(operation.Token)) : ValueTask.FromResult(CanceledReturn);
            };
            if (outcome == 4) { callback = null; }
            if (outcome == 5) { operation.Cancel(); }
            Exception? observed = null;
            try { await RequireMartenTransform(harness.Raw).EnqueueWithPayloadTransformAsync(input, context, callback!, operation.Token); }
            catch (Exception error) { observed = error; }
            observed.ShouldNotBeNull();
            if (outcome == 0) { observed.ShouldBeSameAs(fault); }
            else if (outcome == 1) { observed.ShouldBeOfType<InvalidOperationException>(); }
            else if (outcome == 4) { observed.ShouldBeOfType<ArgumentNullException>(); }
            else { observed.ShouldBeAssignableTo<OperationCanceledException>().ShouldNotBeNull().CancellationToken.ShouldBe(operation.Token); }
            calls.ShouldBe(outcome is 4 or 5 ? 0 : 1); input.SecretReads.ShouldBe(outcome is 4 or 5 ? 0 : 1);
            listener.Calls.ShouldBe(0); (await harness.ReadAsync(deadline.Token)).ShouldBeEmpty();
        }, listener);
    }
    [Fact]
    public async Task PersistAnEmptyMartenTransformResultAsEmptyBinary()
    {
        await RunAsync(null, async harness =>
        {
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(45));
            var input = new InputMessage(); var calls = 0;
            await RequireMartenTransform(harness.Raw).EnqueueWithPayloadTransformAsync(input, Context(harness, input, null), (bytes, tenant, token) =>
            { calls++; bytes.ToArray().ShouldBe(DefaultPayload); return ValueTask.FromResult(Array.Empty<byte>()); }, deadline.Token);
            calls.ShouldBe(1); Payload((await harness.ReadAsync(deadline.Token)).Single()).ShouldBeEmpty();
        });
    }
    [Fact]
    public async Task KeepTheInjectedMartenDocumentStoreUsableAfterProviderDisposal()
    {
        await RunAsync(null, async harness =>
        {
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(45));
            var input = new InputMessage(); await harness.Store.EnqueueAsync(input, Context(harness, input, null), deadline.Token);
            var original = (await harness.ReadAsync(deadline.Token)).Single();
            await harness.Services.DisposeAsync();
            await using var session = harness.Documents.LightweightSession();
            // A real database query through the same caller-owned document store after DI disposal.
            var values = await session.QueryAsync<int>("SELECT 1", deadline.Token);
            values.Single().ShouldBe(1); Payload(original).ShouldBe(DefaultPayload);
        });
    }
    private static IOutboxPayloadTransformStore RequireMartenTransform(MartenOutboxStore raw) =>
        ((IOutboxStore)raw).GetService(typeof(IOutboxPayloadTransformStore)).ShouldBeAssignableTo<IOutboxPayloadTransformStore>().ShouldNotBeNull();
    private sealed class TransformSaveListener(Action? beforeSave) : DocumentSessionListenerBase
    {
        private int _calls;
        internal int Calls => Volatile.Read(ref _calls);
        public override Task BeforeSaveChangesAsync(IDocumentSession session, CancellationToken token)
        {
            token.ThrowIfCancellationRequested(); Interlocked.Increment(ref _calls); beforeSave?.Invoke(); return Task.CompletedTask;
        }
    }
}
