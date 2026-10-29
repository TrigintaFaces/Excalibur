// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

namespace Excalibur.EventSourcing.Subscriptions;

/// <summary>
/// Persists subscription checkpoint positions for durable subscriptions.
/// </summary>
/// <remarks>
/// <para>
/// Checkpoint stores enable event subscriptions to resume from their last known
/// position after a restart. Each subscription is identified by a unique name.
/// </para>
/// <para>
/// Follows the pattern from <c>Azure.Messaging.ServiceBus.ServiceBusProcessor</c>
/// which uses checkpoint-based position tracking for message processing.
/// </para>
/// </remarks>
public interface ISubscriptionCheckpointStore
{
	/// <summary>
	/// Atomically binds each supplied projection to the subscription, and returns the subscription's
	/// COMPLETE roster as the durable store holds it.
	/// </summary>
	/// <param name="subscriptionName">The nonblank stable subscription name, at most 255 characters.</param>
	/// <param name="members">
	/// The projections this caller hosts, each with its own fingerprint and destination. Distinct by
	/// <see cref="SubscriptionMember.ProjectionKey"/> under <see cref="StringComparer.Ordinal"/>.
	/// </param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>
	/// Every member bound to this subscription, INCLUDING any the caller did not supply.
	/// </returns>
	/// <remarks>
	/// <para>
	/// ONE ROW PER PROJECTION, NOT ONE FINGERPRINT PER SET, and the difference is the whole point. A
	/// fingerprint covering the registration set refuses every cooperating host whenever a projection is
	/// added, removed or renamed, or any destination moves — and the only remedy re-folds every projection
	/// from zero, including the ones that did not change. Binding per member makes an additive change
	/// additive: a new projection binds alone and folds alone, and a changed destination refuses only the
	/// projection whose destination moved.
	/// </para>
	/// <para>
	/// THE RETURNED ROSTER IS LOAD-BEARING AND MUST NOT BE DISCARDED. A member present in the store and
	/// absent from the calling process is work this process cannot perform, and therefore cannot attest.
	/// A caller that computes its required set from its own registration list instead will fold what it
	/// can see, report freshness, and say nothing about a sibling bound by another deployment that has not
	/// folded the same events — an attestation that is false and that nothing downstream can detect,
	/// because the caller was told it need not re-check. The roster is a property of the SUBSCRIPTION, not
	/// of the process.
	/// </para>
	/// <para>
	/// Per member, the first writer establishes the binding atomically: an insert-if-absent keyed on
	/// (subscription, projection key), performed as ONE action the engine enforces. It is a CLAIM and
	/// never an upsert — the loser of a race must not overwrite the winner, and no operation on this
	/// contract may update a bound member's fingerprint. Concurrent writers supplying an identical
	/// fingerprint join; a different one is refused for that member, naming it, while members that match
	/// stay bound. Binding never creates a checkpoint: an absent checkpoint must remain distinguishable
	/// from zero. Call this before reading or applying subscription work. A binding is not a lease, an
	/// ownership claim, or a fence on external handler effects.
	/// </para>
	/// <para>
	/// The store compares fingerprints only; it does not discover or verify their contents.
	/// <see cref="SubscriptionMember.Destination"/> is persisted so a refusal can name what moved, and is
	/// never compared. The built-in processor incorporates a configured destination identity when the
	/// resolved store advertises the optional IProjectionDestinationIdentity capability; where that
	/// capability is absent the consumer must identify the destination in the subscription scope instead.
	/// Configured names are not physical resource identities or lifecycle fences: alias repointing, or
	/// resource replacement under the same name, is not detected. External edits, deletion or restoration
	/// of definition or checkpoint storage are outside this contract's lifetime guarantee.
	/// </para>
	/// </remarks>
	/// <exception cref="ArgumentException">
	/// The name, a projection key or a fingerprint does not satisfy the stated format, or two members
	/// share a projection key.
	/// </exception>
	/// <exception cref="InvalidOperationException">
	/// A supplied projection is already bound to a different processing definition. The message names
	/// that projection and both destinations. Restore that definition, or release the member and rebuild
	/// it deliberately — releasing one member re-folds that projection alone.
	/// </exception>
	Task<SubscriptionRoster> BindSubscriptionAsync(
		string subscriptionName,
		IReadOnlyList<SubscriptionMember> members,
		CancellationToken cancellationToken);

	/// <summary>Reads one consistent snapshot of a subscription, including member identities and cursors.</summary>
	/// <param name="subscriptionName">The subscription name.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>The current roster, empty when nothing is bound.</returns>
	Task<SubscriptionRoster> ReadSubscriptionAsync(string subscriptionName, CancellationToken cancellationToken);

	/// <summary>
	/// Removes one projection from a subscription's roster together with its checkpoint, so that the
	/// projection re-binds and re-folds from the beginning on its next start.
	/// </summary>
	/// <param name="subscriptionName">The nonblank stable subscription name.</param>
	/// <param name="projectionKey">The member's projection key.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <param name="bindingId">The lifetime being retired. A replacement binding is never removed.</param>
	/// <returns>
	/// <see langword="true"/> when a member row was removed; <see langword="false"/> when the
	/// subscription had no such lifetime, a reader still referenced it, or a database concurrency conflict
	/// rolled back the mutation. A false result confirms that this operation wrote nothing.
	/// </returns>
	/// <remarks>
	/// <para>
	/// THE MEMBER ROW AND ITS CHECKPOINT MUST BE REMOVED IN ONE DURABLE TRANSACTION, and the atomicity is
	/// load-bearing rather than tidy. Removing the member alone leaves a cursor that nothing validates and
	/// that no operation on this contract can clear, so lag reporting shows it forever. Removing the
	/// checkpoint alone leaves a bound member that re-folds from zero on a destination still holding the
	/// previous generation's rows. The checkpoint is addressed by the SAME pair as the member row, so the
	/// two deletes share one key and neither can be aimed at a row the other did not write.
	/// </para>
	/// <para>
	/// This is what makes a deliberate per-projection rebuild expressible, and what makes a semantic
	/// revision bump remediable: without it a changed fingerprint can only refuse startup forever, because
	/// no operation can unbind. A re-fold from zero into a live destination is safe because an
	/// asynchronous projection is required to perform positioned writes, so the destination refuses a
	/// position it has already folded.
	/// </para>
	/// </remarks>
	/// <exception cref="ArgumentException">The name or projection key is null, empty or whitespace.</exception>
	Task<bool> ReleaseSubscriptionMemberAsync(
		string subscriptionName,
		string projectionKey,
		Guid bindingId,
		CancellationToken cancellationToken);

	/// <summary>
	/// Gets the last checkpointed position for one projection within a subscription.
	/// </summary>
	/// <param name="subscriptionName">The nonblank stable subscription name, at most 255 characters.</param>
	/// <param name="projectionKey">
	/// The nonblank projection key, at most 512 characters, as carried by
	/// <see cref="SubscriptionMember.ProjectionKey"/>.
	/// </param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>
	/// The last stored position, or <see langword="null"/> if no checkpoint exists
	/// (indicating the projection should start from the beginning).
	/// </returns>
	/// <remarks>
	/// <para>
	/// A CURSOR IS ADDRESSED BY THE PAIR, NOT BY ONE COMPOSED STRING, and the difference is a storable
	/// row rather than a style preference. The two parts are bounded independently -- 255 and 512 -- so
	/// any single column wide enough for one is too narrow for both concatenated, and a deeply namespaced
	/// projection type reaches that. A pair that binds successfully as a member row has to be addressable
	/// as a cursor too: otherwise the member is bound and its cursor can never be written, which is a
	/// permanent stall rather than a refusal, and under a provider that truncates instead of refusing it
	/// is two projections silently sharing one cursor and each skipping what the other consumed.
	/// </para>
	/// <para>
	/// Both parts are compared ORDINALLY, so two keys differing only in case are two cursors.
	/// </para>
	/// </remarks>
	/// <exception cref="ArgumentException">Either part is null, empty or whitespace.</exception>
	Task<long?> GetCheckpointAsync(
		string subscriptionName,
		string projectionKey,
		CancellationToken cancellationToken);

	/// <summary>
	/// Advances the checkpoint for one projection within a subscription, but only while it still holds the
	/// position the caller last observed.
	/// </summary>
	/// <param name="subscriptionName">The nonblank stable subscription name, at most 255 characters.</param>
	/// <param name="projectionKey">
	/// The nonblank projection key, at most 512 characters, as carried by
	/// <see cref="SubscriptionMember.ProjectionKey"/>. Addressed as a pair with the subscription name for
	/// the reason stated on <see cref="GetCheckpointAsync(string, string, CancellationToken)"/>.
	/// </param>
	/// <param name="bindingId">The binding returned by the roster read before processing. Stale lifetimes cannot advance.</param>
	/// <param name="expectedPosition">
	/// The position the caller believes is stored now, or <see langword="null"/> when the caller believes
	/// no checkpoint exists yet. "No checkpoint" and "a checkpoint of 0" are different states and must
	/// stay distinguishable, which is why this is nullable rather than a sentinel value.
	/// </param>
	/// <param name="newPosition">
	/// A nonnegative position at least as large as <paramref name="expectedPosition"/> when supplied.
	/// Equal positions are accepted only if the atomic comparison still matches.
	/// </param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>
	/// <see cref="CheckpointAdvanceOutcome.Advanced"/> when the comparison matched and the position was accepted (including an equal position), or
	/// <see cref="CheckpointAdvanceOutcome.Superseded"/> when the expected checkpoint state did not match, in
	/// which case NOTHING was written.
	/// </returns>
	/// <remarks>
	/// <para>
	/// This is a conditional update of an existing member, never an insert. The binding identifier
	/// must match as well as the nullable position. Release and rebind invalidate every previous writer.
	/// This is a compare-and-set rather than a blind write, and the difference is not defensive tidiness.
	/// A blind write lets a reader that paused and resumed -- or a second reader of the same subscription
	/// -- move the checkpoint BACKWARDS to a value it read minutes ago, after which every event between
	/// the two positions is delivered a second time.
	/// </para>
	/// <para>
	/// Implementations MUST perform the comparison and the write as ONE atomic operation against the
	/// durable store: a conditional UPDATE, never a read followed by a write. A read-then-write
	/// implementation reintroduces precisely the interleaving this signature exists to exclude, and it
	/// does so invisibly -- the signature would still look correct.
	/// </para>
	/// <para>
	/// A comparison mismatch is reported as Superseded. Invalid negative or descending arguments throw
	/// before storage access, even when the expectation would not match. These checks and the atomic
	/// comparison together prevent rewind within the same checkpoint lifetime. Deletion, restoration,
	/// external writes, and legacy writers are outside that guarantee.
	/// </para>
	/// <para>
	/// Acceptance is not exclusive ownership or fencing of projection effects. Multiple equal-position
	/// calls may succeed. A caller receiving Superseded must reconcile its processing state before
	/// continuing; the result alone does not prove that another reader remains active.
	/// </para>
	/// </remarks>
	/// <exception cref="ArgumentException">Either key part is null, empty or whitespace.</exception>
	/// <exception cref="ArgumentOutOfRangeException">
	/// Either position is negative, or the new position is smaller than the supplied expected position.
	/// </exception>
	Task<CheckpointAdvanceOutcome> AdvanceCheckpointAsync(
		string subscriptionName,
		string projectionKey,
		Guid bindingId,
		long? expectedPosition,
		long newPosition,
		CancellationToken cancellationToken);

	/// <summary>
	/// Enumerates every stored subscription checkpoint.
	/// </summary>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>
	/// All persisted checkpoints, one per (subscription, projection key) pair. Returns an empty list when
	/// no checkpoints have been stored.
	/// </returns>
	/// <remarks>
	/// Enables projection-lag reporting: pairing each subscription's checkpoint with the global
	/// stream head position yields the per-stream lag.
	/// </remarks>
	Task<IReadOnlyList<SubscriptionCheckpoint>> EnumerateCheckpointsAsync(CancellationToken cancellationToken);
}

/// <summary>
/// A single stored subscription checkpoint: the projection it belongs to and its last checkpointed
/// position.
/// </summary>
/// <param name="SubscriptionName">The stable subscription name.</param>
/// <param name="ProjectionKey">The projection's key within that subscription.</param>
/// <param name="Position">The last checkpointed global-stream position.</param>
/// <remarks>
/// The pair is carried apart rather than as one composed string so a reader can group by subscription
/// without re-parsing a delimiter that either part may itself contain.
/// </remarks>
public readonly record struct SubscriptionCheckpoint(
	string SubscriptionName,
	string ProjectionKey,
	long Position);

/// <summary>
/// The result of attempting to advance a subscription checkpoint.
/// </summary>
public enum CheckpointAdvanceOutcome
{
	/// <summary>
	/// The expected state matched and the requested position was accepted, including an equal position.
	/// </summary>
	Advanced,

	/// <summary>
	/// The expected checkpoint state did not match, so this advance was refused and nothing was written.
	/// </summary>
	/// <remarks>
	/// The caller must reconcile its processing state before continuing. This outcome does not establish
	/// ownership, fence effects, or prove that another reader is still making progress.
	/// </remarks>
	Superseded,
}
