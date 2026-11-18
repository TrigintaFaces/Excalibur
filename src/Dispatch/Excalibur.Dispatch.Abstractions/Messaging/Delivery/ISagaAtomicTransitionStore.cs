// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

namespace Excalibur.Dispatch.Messaging;

/// <summary>Optionally commits immutable state and effects in one provider-owned durable transaction.</summary>
/// <remarks>
/// Existing ISagaStore state-only semantics are unchanged. Durable orchestration must select this capability
/// explicitly and fail closed when it is absent. Implementations commit state, timers, command/event rows,
/// effect membership and an exact receipt together. They do not dispatch while holding the transaction.
/// Effects retain stable identities and ordinal/sequence metadata for the existing outbox drain.
/// This capability does not promise global FIFO, ordered completion across producers, exactly-once
/// delivery or a new shared head/fencing protocol. Configured timers require the selected participant;
/// absent timeout configuration requires no fabricated participant. Unsupported composition refuses
/// before state persistence; state-only fallback is never an atomic transition.
/// </remarks>
public interface ISagaAtomicTransitionStore
{
    /// <summary>Refuses an incompatible selected timer backend/table before the handler executes.</summary>
    /// <param name="identity">The current outer-authoritative tenant and saga scope.</param>
    /// <param name="participant">The actual selected timeout-store participant.</param>
    /// <param name="cancellationToken">The operation cancellation token.</param>
    /// <returns>A completed operation only when the known logical writer binding agrees.</returns>
    /// <remarks>
    /// This pure admission check uses the kernel's own frozen unopened target and exact writer table;
    /// it must not open connections, persist, commit, dispatch or infer physical alias equivalence.
    /// It does not prevent subsequent configuration changes. Commit must still compare the same
    /// digested participant binding against its ACTUAL open transaction/writer before any writes.
    /// Outer wrappers must revalidate the supplied authority and selected inner capability per call.
    /// </remarks>
    ValueTask ValidateTimerParticipantAsync(SagaScopeIdentity identity,
        ISagaAtomicTimeoutParticipant participant, CancellationToken cancellationToken);

    /// <summary>Synchronizes with cancellation/completion and validates an original owned timeout delivery.</summary>
    /// <param name="delivery">The original claimed timeout authority, preserved through delivery metadata.</param>
    /// <param name="cancellationToken">The operation cancellation token.</param>
    /// <returns>True for a currently eligible owned timeout, false only for positively known obsolete authority.</returns>
    /// <remarks>
    /// Missing/corrupt/mismatched authority or lookup failure must throw, never become a false no-op.
    /// A missing timer payload row alone is not obsolete: worker retirement can follow durable queue
    /// acceptance before the handler executes. The retained membership, scope incarnation/generation,
    /// and cancellation tombstone establish authority. Outer wrappers enforce the exact current tenant.
    /// A concurrent cancellation after this check must still defeat the eventual transition CAS.
    /// </remarks>
    ValueTask<bool> IsTimeoutDeliveryCurrentAsync(SagaTimeoutDeliveryIdentity delivery, CancellationToken cancellationToken);

    /// <summary>Establishes whether an identity is a retained owned timeout in the authoritative scope.</summary>
    /// <param name="identity">The exact current outer-authoritative saga scope.</param>
    /// <param name="deliveryId">The original nonempty opaque identity being tested.</param>
    /// <param name="cancellationToken">The operation cancellation token.</param>
    /// <returns>True for any retained owned timeout identity, including cancelled or previously delivered ones.</returns>
    /// <remarks>
    /// False means positively established synchronized absence in this scope, never a lookup error,
    /// missing schema or unsupported capability. This is not a probe of other tenants. Manual-timeout
    /// admission must refuse a known owned identity even when origin metadata was stripped. Ordinary
    /// queue acceptance retirement must retain this authority. Exact ordinal ID matching is required.
    /// </remarks>
    ValueTask<bool> HasOwnedTimeoutIdentityAsync(SagaScopeIdentity identity, string deliveryId, CancellationToken cancellationToken);

    /// <summary>Loads a lock-synchronized scope/state snapshot without allocating a new speculative transition.</summary>
    /// <param name="identity">The exact tenant and saga storage scope.</param>
    /// <param name="cancellationToken">The operation cancellation token.</param>
    /// <returns>The authoritative snapshot, or null only after synchronized absence is established.</returns>
    /// <remarks>A mismatched type, hash collision or unsupported legacy row must refuse, never masquerade as an absent scope.</remarks>
    ValueTask<SagaDurableSnapshot?> LoadSnapshotAsync(SagaScopeIdentity identity,CancellationToken cancellationToken);

    /// <summary>Synchronizes with outstanding commits and looks up the original delivery before handler execution or payload preparation.</summary>
    /// <param name="identity">The original delivery authority, independent of a new speculative transition identifier.</param>
    /// <param name="cancellationToken">The operation cancellation token.</param>
    /// <returns>DeliveryAlreadyCommitted and its durable version, or NotFound only after synchronized absence is established.</returns>
    /// <remarks>A lookup error or unresolved outstanding transaction must never be converted to NotFound.</remarks>
    ValueTask<SagaTransitionCommitResult> FindCommittedDeliveryAsync(SagaDeliveryIdentity identity, CancellationToken cancellationToken);

    /// <summary>Commits the same prepared batch once or resolves an existing exact receipt.</summary>
    /// <param name="transition">The immutable prepared attempt, retained across uncertain commit outcomes.</param>
    /// <param name="cancellationToken">The operation cancellation token.</param>
    /// <returns>The positively known outcome, or Unknown when commit cannot be resolved.</returns>
    ValueTask<SagaTransitionCommitResult> CommitAsync(SagaPreparedTransition transition, CancellationToken cancellationToken);
}
