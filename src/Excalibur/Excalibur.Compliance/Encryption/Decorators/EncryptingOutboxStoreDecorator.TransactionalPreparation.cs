// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0
using System.Diagnostics.CodeAnalysis;
using Excalibur.Dispatch;

namespace Excalibur.Compliance.Encryption.Decorators;

internal sealed partial class EncryptingOutboxStoreDecorator
{
    private sealed class EncryptedTransactionalPreparation(ITransactionalOutboxPreparer inner,
        ITransactionalOutboxPayloadTransformPreparer transform, EncryptingOutboxStoreDecorator owner) : ITransactionalOutboxPreparer
    {
        public ValueTask<IPreparedTransactionalOutboxWrite> PrepareMessageAsync(OutboundMessage message, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(message);
            cancellationToken.ThrowIfCancellationRequested();
            var mode = owner.GetWriteMode();
            return mode is EncryptionMode.Disabled or EncryptionMode.DecryptOnlyWritePlaintext
                ? inner.PrepareMessageAsync(message, cancellationToken)
                : transform.PrepareMessageWithPayloadTransformAsync(message, owner.EncryptEnqueuePayloadAsync, cancellationToken);
        }

        [RequiresUnreferencedCode("The owning runtime-message codec may discover arbitrary consumer payload metadata. Use explicit encoded envelopes for the metadata-safe route.")]
        [RequiresDynamicCode("The owning runtime-message codec may require runtime code generation. Use explicit encoded envelopes for the metadata-safe route.")]
        public ValueTask<IPreparedTransactionalOutboxWrite> PrepareEnqueueAsync(IDispatchMessage message, IMessageContext context, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(message);
            ArgumentNullException.ThrowIfNull(context);
            cancellationToken.ThrowIfCancellationRequested();
            var mode = owner.GetWriteMode();
            return mode is EncryptionMode.Disabled or EncryptionMode.DecryptOnlyWritePlaintext
                ? inner.PrepareEnqueueAsync(message, context, cancellationToken)
                : transform.PrepareEnqueueWithPayloadTransformAsync(message, context, owner.EncryptEnqueuePayloadAsync, cancellationToken);
        }
    }
}
