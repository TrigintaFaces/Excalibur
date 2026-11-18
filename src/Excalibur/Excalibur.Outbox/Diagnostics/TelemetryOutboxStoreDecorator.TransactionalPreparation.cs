// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0
using System.Data;
using System.Diagnostics.CodeAnalysis;
using Excalibur.Dispatch;
using Excalibur.Dispatch.Diagnostics;
namespace Excalibur.Outbox.Diagnostics;
internal sealed partial class TelemetryOutboxStoreDecorator
{
    private sealed class MeasuredTransactionalPreparation(ITransactionalOutboxPreparer inner, TelemetryOutboxStoreDecorator owner) : ITransactionalOutboxPreparer
    {
        public async ValueTask<IPreparedTransactionalOutboxWrite> PrepareMessageAsync(OutboundMessage message, CancellationToken cancellationToken)
        {
            var watch = ValueStopwatch.StartNew();
            var prepared = await inner.PrepareMessageAsync(message, cancellationToken).ConfigureAwait(false);
            owner.RecordOperation("transactional_prepare", 0, watch.Elapsed.TotalMilliseconds);
            return new MeasuredTransactionalWrite(prepared, owner);
        }
        [RequiresUnreferencedCode("The owning runtime-message codec may discover arbitrary consumer payload metadata. Use explicit encoded envelopes for the metadata-safe route.")]
        [RequiresDynamicCode("The owning runtime-message codec may require runtime code generation. Use explicit encoded envelopes for the metadata-safe route.")]
        public async ValueTask<IPreparedTransactionalOutboxWrite> PrepareEnqueueAsync(IDispatchMessage message, IMessageContext context, CancellationToken cancellationToken)
        {
            var watch = ValueStopwatch.StartNew();
            var prepared = await inner.PrepareEnqueueAsync(message, context, cancellationToken).ConfigureAwait(false);
            owner.RecordOperation("transactional_prepare", 0, watch.Elapsed.TotalMilliseconds);
            return new MeasuredTransactionalWrite(prepared, owner);
        }
    }
    private sealed class MeasuredTransactionalTransform(ITransactionalOutboxPayloadTransformPreparer inner, TelemetryOutboxStoreDecorator owner) : ITransactionalOutboxPayloadTransformPreparer
    {
        public async ValueTask<IPreparedTransactionalOutboxWrite> PrepareMessageWithPayloadTransformAsync(OutboundMessage message, Func<ReadOnlyMemory<byte>, string?, CancellationToken, ValueTask<byte[]>> transformPayload, CancellationToken cancellationToken)
        {
            var watch = ValueStopwatch.StartNew();
            var prepared = await inner.PrepareMessageWithPayloadTransformAsync(message, transformPayload, cancellationToken).ConfigureAwait(false);
            owner.RecordOperation("transactional_prepare", 0, watch.Elapsed.TotalMilliseconds);
            return new MeasuredTransactionalWrite(prepared, owner);
        }
        [RequiresUnreferencedCode("The owning runtime-message codec may discover arbitrary consumer payload metadata. Use explicit encoded envelopes for the metadata-safe route.")]
        [RequiresDynamicCode("The owning runtime-message codec may require runtime code generation. Use explicit encoded envelopes for the metadata-safe route.")]
        public async ValueTask<IPreparedTransactionalOutboxWrite> PrepareEnqueueWithPayloadTransformAsync(IDispatchMessage message, IMessageContext context, Func<ReadOnlyMemory<byte>, string?, CancellationToken, ValueTask<byte[]>> transformPayload, CancellationToken cancellationToken)
        {
            var watch = ValueStopwatch.StartNew();
            var prepared = await inner.PrepareEnqueueWithPayloadTransformAsync(message, context, transformPayload, cancellationToken).ConfigureAwait(false);
            owner.RecordOperation("transactional_prepare", 0, watch.Elapsed.TotalMilliseconds);
            return new MeasuredTransactionalWrite(prepared, owner);
        }
    }
    private sealed class MeasuredTransactionalWrite(IPreparedTransactionalOutboxWrite inner, TelemetryOutboxStoreDecorator owner) : IPreparedTransactionalOutboxWrite
    {
        public string MessageId => inner.MessageId;
        public string? TenantId => inner.TenantId;
        public byte[] GetCanonicalWireBytes() => inner.GetCanonicalWireBytes();
        public async ValueTask StageAsync(IDbTransaction transaction, CancellationToken cancellationToken)
        {
            var watch = ValueStopwatch.StartNew();
            await inner.StageAsync(transaction, cancellationToken).ConfigureAwait(false);
            // Provisional insertion, NOT a transaction-commit or message-delivered metric.
            owner.RecordOperation("transactional_stage", 1, watch.Elapsed.TotalMilliseconds);
        }
    }
}
