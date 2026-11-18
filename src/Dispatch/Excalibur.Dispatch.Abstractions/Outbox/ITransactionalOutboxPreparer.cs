// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Diagnostics.CodeAnalysis;

namespace Excalibur.Dispatch;

/// <summary>Prepares immutable transaction-participating outbox writes through the selected owning provider.</summary>
/// <remarks>
/// This optional capability is discovered on the selected outer outbox store. Payload-bearing decorators must
/// mediate it or withhold it; callers must never bypass selected encryption by resolving a raw concrete store.
/// Preparation performs no database persistence. Provider codec, metadata framing and transforms complete
/// before the coordinating transaction begins. Unsupported composition fails instead of falling back to Enqueue.
/// </remarks>
public interface ITransactionalOutboxPreparer
{
    /// <summary>Prepares an explicit already-encoded outbound envelope using the provider's selected metadata configuration.</summary>
    /// <param name="message">The caller-owned envelope, copied before asynchronous processing.</param>
    /// <param name="cancellationToken">The operation cancellation token.</param>
    /// <returns>A fully encoded immutable write that accepts no message until its owner commits.</returns>
    ValueTask<IPreparedTransactionalOutboxWrite> PrepareMessageAsync(OutboundMessage message, CancellationToken cancellationToken);

    /// <summary>Prepares the legacy runtime-message workload using the owning provider's actual configured payload codec.</summary>
    /// <param name="message">The runtime message.</param>
    /// <param name="context">The caller context whose supported envelope fields are frozen during preparation.</param>
    /// <param name="cancellationToken">The operation cancellation token.</param>
    /// <returns>A fully encoded immutable write without persisting it.</returns>
    [RequiresUnreferencedCode("The owning runtime-message codec may discover arbitrary consumer payload metadata. Use explicit encoded envelopes for the metadata-safe route.")]
    [RequiresDynamicCode("The owning runtime-message codec may require runtime code generation. Use explicit encoded envelopes for the metadata-safe route.")]
    ValueTask<IPreparedTransactionalOutboxWrite> PrepareEnqueueAsync(IDispatchMessage message, IMessageContext context, CancellationToken cancellationToken);
}
