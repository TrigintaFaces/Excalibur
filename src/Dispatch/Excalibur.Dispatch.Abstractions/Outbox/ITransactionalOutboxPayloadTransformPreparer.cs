// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Diagnostics.CodeAnalysis;

namespace Excalibur.Dispatch;

/// <summary>Lets a selected decorator transform the owning provider's serialized bytes before immutable transactional preparation.</summary>
/// <remarks>
/// The owning provider freezes its actual runtime-message envelope and stable borrowed payload before calling
/// the transform exactly once. It copies the returned bytes before completing final wire encoding. Callback
/// faults, cancellation or a null result accept no write. The callback completes before any transaction begins.
/// Payload-bearing outer decorators must not expose this raw facet to bypass their own mediation.
/// </remarks>
public interface ITransactionalOutboxPayloadTransformPreparer
{
    /// <summary>Prepares an encoded envelope after transforming its payload, freezing provider metadata before the first callback await.</summary>
    /// <param name="message">The caller-owned encoded envelope.</param>
    /// <param name="transformPayload">The transform over stable borrowed payload bytes and frozen tenant provenance.</param>
    /// <param name="cancellationToken">The operation cancellation token.</param>
    /// <returns>An immutable write whose metadata and routing were encoded before the transform could suspend.</returns>
    /// <remarks>
    /// The owning provider encodes headers and transport metadata before invoking this callback. A decorator
    /// must not await encryption and then ask a later preparation call to inspect mutable header objects.
    /// </remarks>
    ValueTask<IPreparedTransactionalOutboxWrite> PrepareMessageWithPayloadTransformAsync(
        OutboundMessage message,
        Func<ReadOnlyMemory<byte>, string?, CancellationToken, ValueTask<byte[]>> transformPayload,
        CancellationToken cancellationToken);

    /// <summary>Prepares a runtime message after applying one transform to its owning codec's serialized payload.</summary>
    /// <param name="message">The runtime message.</param>
    /// <param name="context">The context frozen by the owning provider.</param>
    /// <param name="transformPayload">The transform over stable borrowed payload bytes and frozen tenant provenance.</param>
    /// <param name="cancellationToken">The operation cancellation token.</param>
    /// <returns>A fully encoded immutable write, without persistence.</returns>
    [RequiresUnreferencedCode("The owning runtime-message codec may discover arbitrary consumer payload metadata. Use explicit encoded envelopes for the metadata-safe route.")]
    [RequiresDynamicCode("The owning runtime-message codec may require runtime code generation. Use explicit encoded envelopes for the metadata-safe route.")]
    ValueTask<IPreparedTransactionalOutboxWrite> PrepareEnqueueWithPayloadTransformAsync(
        IDispatchMessage message,
        IMessageContext context,
        Func<ReadOnlyMemory<byte>, string?, CancellationToken, ValueTask<byte[]>> transformPayload,
        CancellationToken cancellationToken);
}
