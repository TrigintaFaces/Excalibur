// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch;

/// <summary>
/// Reports the <em>effective</em> durable capabilities of an <see cref="IInboxStore"/>, composing through
/// decorator chains so a startup capability guard can probe the true innermost behavior rather than a
/// statically-declared interface that may throw at runtime.
/// </summary>
/// <remarks>
/// <para>
/// A decorating inbox store (for example a transparent-encryption decorator) typically declares the
/// segregated capability interfaces (<see cref="IClaimableInboxStore"/>,
/// <see cref="IProcessingTrackingInboxStore"/>) so it can forward them to its inner store. A simple
/// <c>is IClaimableInboxStore</c> check therefore <em>passes</em> for the decorator even when the wrapped
/// inner store lacks the capability — the decorator then throws <see cref="NotSupportedException"/> at
/// first call (pass-then-throw-at-runtime). A decorator that implements this interface instead reports the
/// capability it can actually forward, letting the <c>ValidateOnStart</c> presence-guards fail fast at
/// startup and making the runtime <see cref="NotSupportedException"/> structurally unreachable.
/// </para>
/// <para>
/// Implementations MUST report the <b>effective</b> capability and compose through chains: a decorator
/// reports <see langword="true"/> when its inner store either directly implements the matching capability
/// interface or itself reports <see langword="true"/> via this interface. Plain (non-decorating) stores do
/// not need to implement this interface; the guards fall back to the direct interface check for them.
/// </para>
/// </remarks>
public interface IInboxStoreCapabilities
{
    /// <summary>
    /// Gets a value indicating whether the store can atomically claim a message for idempotent processing
    /// (the effective <see cref="IClaimableInboxStore"/> capability, forwarded through any decoration).
    /// </summary>
    /// <value>
    /// <see langword="true"/> if an atomic claim/release can be forwarded to a capable store; otherwise
    /// <see langword="false"/>.
    /// </value>
    bool SupportsClaim { get; }

    /// <summary>
    /// Gets a value indicating whether the store can acquire a self-expiring lease on a message (the
    /// effective <see cref="ILeasedInboxStore"/> capability, forwarded through any decoration).
    /// </summary>
    /// <remarks>
    /// Distinct from <see cref="SupportsClaim"/>: the two are separate protocols that disagree about the
    /// state space they admit, about who ends a claim, and about whether a stuck claim recovers on its
    /// own. One boolean cannot answer for both, and a store may support either without the other.
    /// </remarks>
    /// <value>
    /// <see langword="true"/> if a lease acquisition can be forwarded to a capable store; otherwise
    /// <see langword="false"/>.
    /// </value>
    bool SupportsLeasedClaim { get; }

    /// <summary>
    /// Gets a value indicating whether the store can durably persist the in-flight
    /// <see cref="InboxStatus.Processing"/> status (the effective
    /// <see cref="IProcessingTrackingInboxStore"/> capability, forwarded through any decoration).
    /// </summary>
    /// <value>
    /// <see langword="true"/> if durable Processing tracking can be forwarded to a capable store; otherwise
    /// <see langword="false"/>.
    /// </value>
    bool SupportsProcessingTracking { get; }

    /// <summary>
    /// Gets a value indicating whether the store can process a message and mark it processed inside a single
    /// enlisting transaction, enabling exactly-once processing.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This answers for EITHER transactional seam: the relational <see cref="ITransactionalInboxStore"/> or
    /// the document-store <see cref="IScopedTransactionalInboxStore"/>. It is a guarantee flag, not a type
    /// test, and <see langword="true"/> is therefore NOT a licence to cast: a document store answers
    /// <see langword="true"/> here while implementing only the scoped seam, and a cast to
    /// <see cref="ITransactionalInboxStore"/> against it fails. Use
    /// <see cref="SupportsScopedTransactional"/> to discover which seam is available before casting.
    /// </para>
    /// </remarks>
    /// <value>
    /// <see langword="true"/> if transactional handler+mark can be forwarded to a capable store over either
    /// seam; otherwise <see langword="false"/> (the store falls back to the documented at-least-once claim
    /// protocol).
    /// </value>
    bool SupportsTransactional { get; }

    /// <summary>
    /// Gets a value indicating whether the store offers the document-store transactional seam specifically
    /// (the effective <see cref="IScopedTransactionalInboxStore"/> capability, forwarded through any
    /// decoration), which hands the handler a scope to enlist its own writes into.
    /// </summary>
    /// <remarks>
    /// Distinct from <see cref="SupportsTransactional"/>, which is satisfied by either seam. The two seams
    /// differ in what the handler receives — the scoped seam yields an
    /// <see cref="IInboxTransactionScope"/> the handler enlists into, the relational seam does not — so a
    /// caller that needs to write alongside the mark cannot act on the union flag. Reporting the seam
    /// separately is what lets that caller discover the boundary instead of discovering it as a failed
    /// cast at first use.
    /// </remarks>
    /// <value>
    /// <see langword="true"/> if a scoped transactional handler+mark can be forwarded to a capable store;
    /// otherwise <see langword="false"/>.
    /// </value>
    bool SupportsScopedTransactional { get; }

    /// <summary>
    /// Gets a value indicating whether the store can durably record a per-entry next-attempt time (the
    /// effective <see cref="IBackoffSchedulableInboxStore"/> capability, forwarded through any decoration).
    /// </summary>
    /// <remarks>
    /// <para>
    /// This capability is the one whose absence is INVISIBLE at the call site, which is why it belongs on
    /// this panel rather than being left to a type check. Every other capability here fails loudly when it
    /// is missing: the call throws, or a startup guard refuses the configuration. A missing backoff
    /// schedule does neither -- the entry is still marked failed, the call still returns successfully, and
    /// the only symptom is that the computed delay never throttles re-delivery, so a poison message is
    /// retried on the fixed re-admission window instead of an exponential one.
    /// </para>
    /// <para>
    /// <see cref="IBackoffSchedulableInboxStore"/> documents that fallback, and locates it in the CALLER:
    /// the processor falls back, having first observed that the store is not schedulable. A decorator that
    /// declares the interface in order to forward it destroys exactly that observability -- the caller sees
    /// a schedulable store, and the decorator silently performs the fallback on its behalf. Reporting the
    /// effective capability here restores the caller's ability to see what it is actually getting.
    /// </para>
    /// </remarks>
    /// <value>
    /// <see langword="true"/> if a backoff schedule can be forwarded to a capable store; otherwise
    /// <see langword="false"/> (the caller falls back to the plain failed status and its fixed
    /// re-admission window).
    /// </value>
    bool SupportsBackoffScheduling { get; }
}
