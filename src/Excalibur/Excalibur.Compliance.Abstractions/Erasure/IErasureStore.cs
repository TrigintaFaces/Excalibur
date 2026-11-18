// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0


using Excalibur.Dispatch;

namespace Excalibur.Compliance;

/// <summary>
/// Storage abstraction for erasure requests and certificates.
/// </summary>
/// <remarks>
/// <para>
/// Implementations should use Dapper (NOT EntityFramework Core) per project constraints.
/// </para>
/// <para>
/// <b>Failures are part of this contract, not an implementation detail.</b> The members below state the
/// exception each one raises, and every implementation is required to raise the same exception for the
/// same condition — a caller must be able to write one <c>catch</c> that works against any store. In
/// particular, callers should not have to catch a database provider's own exception type: an
/// implementation that lets a raw provider exception escape for a condition named here is not conforming,
/// because it forces every consumer to reference that provider and to know its error codes.
/// </para>
/// <para>
/// <b>One condition, one exception type — and the type must identify the condition.</b> Where several
/// conditions would otherwise share a type, each gets its own: a duplicate request identifier raises
/// <see cref="DuplicateErasureRequestException"/>, an absent or stale backing schema raises
/// <see cref="ErasureStoreNotProvisionedException"/> (outside the <see cref="InvalidOperationException"/>
/// hierarchy entirely), and an unresolved ambient tenant raises <see cref="TenantRequiredException"/>. A
/// caller branching on the duplicate signal MUST catch that specific type. Catching
/// <see cref="InvalidOperationException"/> instead reads every one of those conditions as "the request is
/// already on file", so a request that was never stored is treated as stored and never re-filed — and an
/// erasure request dropped that way is a statutory right silently lost, with nothing anywhere reporting it.
/// </para>
/// <para>
/// <b>Expected write outcomes are explicit.</b> <see cref="UpdateStatusAsync"/> returns
/// <see cref="ErasureWriteOutcome"/> to distinguish an applied write, a lost claim, an illegal status
/// transition and a request absent from the ambient tenant. <see cref="RecordCompletionAsync"/> returns
/// <see cref="ErasureWriteOutcome.Applied"/> or <see cref="ErasureWriteOutcome.ClaimLost"/>; an absent
/// request or an ineligible completion status remains an anomaly signaled by
/// <see cref="KeyNotFoundException"/>. Test for <see cref="ErasureWriteOutcome.Applied"/> by name; a
/// refused write must never attest that erasure completed. Losing a claim requires standing down rather
/// than overwriting the new executor. <see cref="RecordCancellationAsync"/> retains its Boolean result.
/// Typed refusals do not replace the documented exceptions for invalid inputs, provisioning, tenant or
/// infrastructure failures.
/// </para>
/// <para>
/// <b>Tenant confinement.</b> Every member here is confined to the ambient tenant established for this
/// store instance. A <c>requestId</c>-addressed member (<see cref="GetStatusAsync"/>,
/// <see cref="UpdateStatusAsync"/>, <see cref="RecordCompletionAsync"/>,
/// <see cref="RecordCancellationAsync"/>) reports another tenant's request the same way it reports one
/// that never existed — not found — rather than resolving or mutating it, so a caller cannot probe for a
/// foreign request's existence by its identifier alone. Which mechanism a given provider uses to hold
/// that boundary is declared by its capability marker — <see cref="ITenantScopingCapability{TContract}"/>
/// for a store that reads an ambient tenant — and the package's own <c>ARCHITECTURE.md</c> states the
/// falsifiable guarantee and how it is verified. A store presenting no marker is not confined by the
/// framework.
/// </para>
/// </remarks>
[TenantOwned]
public interface IErasureStore
{
	/// <summary>
	/// Saves a new erasure request.
	/// </summary>
	/// <param name="request">The erasure request to save.</param>
	/// <param name="scheduledExecutionTime">When the erasure is scheduled to execute.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>A task representing the async operation.</returns>
	/// <exception cref="DuplicateErasureRequestException">
	/// A request with the same <see cref="ErasureRequest.RequestId"/> already exists — this exception is
	/// raised for that condition and for no other, so it is the signal a caller may safely read as "already
	/// on file". This operation inserts; it does not overwrite. Re-filing an existing request identifier is
	/// reported rather than silently replacing the stored request, because the original request records when
	/// erasure was asked for and under which legal basis.
	/// </exception>
	/// <exception cref="ErasureStoreNotProvisionedException">
	/// The store's backing schema is absent, or is present but missing columns its statements bind. A
	/// deployment fault rather than an outcome of this call: nothing about <paramref name="request"/> is
	/// wrong, the request was <b>not</b> stored, and re-filing it after the schema is repaired is correct.
	/// </exception>
	/// <exception cref="TenantRequiredException">
	/// Multi-tenancy is active but no ambient tenant is established, so the request cannot be confined to a
	/// tenant. The request was <b>not</b> stored.
	/// </exception>
	/// <exception cref="ObjectDisposedException">
	/// The store has been disposed. The request was <b>not</b> stored.
	/// </exception>
	Task SaveRequestAsync(
		ErasureRequest request,
		DateTimeOffset scheduledExecutionTime,
		CancellationToken cancellationToken);

	/// <summary>
	/// Gets the status of an erasure request.
	/// </summary>
	/// <param name="requestId">The request ID.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>The erasure status, or null if not found.</returns>
	/// <remarks>
	/// Confined to the ambient tenant established for this store instance: a request stored under
	/// another tenant is reported as not found, the same as a <paramref name="requestId"/> that never
	/// existed.
	/// </remarks>
	/// <exception cref="ErasureStoreNotProvisionedException">
	/// The store's backing schema is absent or stale, so the lookup cannot be answered at all. This is the
	/// only condition under which this member does not return: a request that is not there is reported as
	/// <see langword="null"/> and never as an exception, so a caller can always tell "no such request" from
	/// "this store cannot answer".
	/// </exception>
	/// <exception cref="TenantRequiredException">
	/// Multi-tenancy is active but no ambient tenant is established, so the lookup cannot be confined to a
	/// tenant. It fails closed rather than reading across tenants.
	/// </exception>
	Task<ErasureStatus?> GetStatusAsync(
		Guid requestId,
		CancellationToken cancellationToken);

	/// <summary>
	/// Updates the status of an erasure request.
	/// </summary>
	/// <param name="requestId">The request ID.</param>
	/// <param name="status">The new status.</param>
	/// <remarks>
	/// <para>
	/// <b>THIS MEMBER MAY NOT BE USED TO TAKE CUSTODY, and asking it to throws.</b> Writing
	/// <see cref="ErasureRequestStatus.InProgress"/> through it raises
	/// <see cref="System.ArgumentException"/>: that status records CUSTODY, claiming has its own door in
	/// <see cref="ClaimForExecutionAsync"/>, and that is the only member which mints the identity a
	/// fenced write presents. A second door would advance a request into the claimed status for a caller
	/// holding no identity, and every fenced write it then made would compare against nothing.
	/// </para>
	/// <para>
	/// <i>Superseded wording, quoted so an implementation written against it is recognisable: "The
	/// transition to InProgress is a CLAIM, and it is conditional. It succeeds only for a request
	/// currently in Scheduled ... exactly one receives true and the other false." That was the contract
	/// when the status column carried both OUTCOME and CUSTODY. Custody now has columns of its own and a
	/// member of its own; this one refuses that status outright. The REASON has not changed and is worth
	/// keeping: two callers both told they claimed a request each erase the same subject and each write
	/// a completion certificate, one of them attesting that keys were destroyed when its own attempt
	/// found nothing left to destroy.</i>
	/// </para>
	/// <para>
	/// <b>The transition to <see cref="ErasureRequestStatus.Scheduled"/> is ALSO conditional, and for the
	/// same reason read backwards.</b> It succeeds only for a request that is neither OWNED nor TERMINAL
	/// — that is, not <see cref="ErasureRequestStatus.InProgress"/>,
	/// <see cref="ErasureRequestStatus.AwaitingKeyDestruction"/>,
	/// <see cref="ErasureRequestStatus.Completed"/> or <see cref="ErasureRequestStatus.Cancelled"/> — and
	/// the test and the write are ONE indivisible operation. <c>Scheduled</c> is not an outcome: it is
	/// the single state the claim above succeeds from, so writing it does not record what happened, it
	/// GRANTS PERMISSION to the next caller. Writing it over a claimed request re-offers an erasure that
	/// somebody is already executing, and the second caller's claim then succeeds because it is not a
	/// second claim at all — producing exactly the two concurrent executions and two completion
	/// certificates the paragraph above exists to prevent, reached from the other side. A caller that
	/// reads the status first and then writes cannot establish this: the claim may land between its read
	/// and its write.
	/// </para>
	/// <para>
	/// <see cref="ErasureWriteOutcome.ClaimLost"/> is the store telling the caller that somebody else now
	/// owns the request. A retry path must treat it as "not mine any more" and stand down silently,
	/// never as a transient failure to write. It is distinct from
	/// <see cref="ErasureWriteOutcome.StatusRefused"/>, which while holding the claim is a caller error
	/// worth reporting.
	/// </para>
	/// <para>
	/// <b>The transition to <see cref="ErasureRequestStatus.AwaitingKeyDestruction"/> is CONDITIONAL for
	/// the same reason.</b> It succeeds only from <see cref="ErasureRequestStatus.InProgress"/> or from
	/// <see cref="ErasureRequestStatus.AwaitingKeyDestruction"/> itself. That status is the completion
	/// pass's DISCOVERY predicate and enables <c>RecordCompletionAsync</c>, so writing it enrolls the
	/// request in a pass that asks the key-management provider to confirm destruction and then completes
	/// and CERTIFIES it. Writing it over a cancelled request would feed a cancelled erasure's keys into
	/// the destruction pipeline and could certify an erasure the consumer called off — and the status's
	/// own documentation already says a request awaiting key destruction is not cancellable, while
	/// nothing enforced the converse. The source set includes the target itself because the completion
	/// pass legitimately rewrites the detail while the provider still holds the keys; a rule permitting
	/// only <see cref="ErasureRequestStatus.InProgress"/> would break that pass.
	/// </para>
	/// <para>
	/// Every remaining transition is unconditional: a request may be moved to a terminal or blocked state
	/// from whatever state it is in, because those transitions genuinely record an outcome rather than
	/// acquire ownership. <b>The partition is the rule, not the list:</b> a status is unconditionally
	/// writable only if NO operation's precondition succeeds BECAUSE the request is in it. If a status
	/// ever becomes such an enabling predicate, its write becomes conditional by that fact alone — and
	/// that reclassification can be caused by adding a QUERY, with nothing in the write path's diff to
	/// show it.
	/// </para>
	/// <para>
	/// <b>THAT GAP IS NOW CLOSED, by the expected-status parameter rather than by more special cases.</b>
	/// By the rule above <see cref="ErasureRequestStatus.Pending"/> is also an enabling status —
	/// <c>RecordCancellationAsync</c> succeeds because a request is <c>Pending</c> or <c>Scheduled</c> —
	/// and its write used to be unconditional here. A caller that cares now passes <c>Pending</c> as
	/// <paramref name="expectedStatus"/> and the store refuses anything else.
	/// </para>
	/// <para>
	/// <b><paramref name="expectedStatus"/> TIGHTENS; it never replaces.</b> <see langword="null"/> means
	/// the partition above applies as written. A value means the partition applies AND the current status
	/// must equal it. It is deliberately not a way to state the whole precondition yourself: that would
	/// move this state machine into every caller, consumer callers included, and the guarantee that a
	/// request cannot be enrolled in the destruction pipeline from a cancelled state rests on this store
	/// holding the partition centrally. Because the parameter can only ever add a conjunct, LOOSENING the
	/// precondition has no expression in this signature at all.
	/// </para>
	/// <para>
	/// <i>Superseded wording, quoted so an implementation written against it is recognisable: "Every
	/// other transition is unconditional: a request may be moved to a terminal or blocked state from
	/// whatever state it is in, because those transitions record an outcome rather than acquire
	/// ownership." The justification was sound for every status except <c>Scheduled</c>, which is not an
	/// outcome, and all three shipped stores implemented the sentence as written.</i>
	/// </para>
	/// </remarks>
	/// <param name="expectedStatus">
	/// When non-null, the status the request must currently be in for this write to apply, in ADDITION to
	/// the partition above. When null, only the partition applies.
	/// </param>
	/// <param name="errorMessage">Optional error message if failed.</param>
	/// <param name="claimIdentity">
	/// The identity of the claim this caller holds, or <see langword="null"/> to assert that the request
	/// carries NO LIVE CLAIM.
	/// <para>
	/// <b>Null is the STRICTER argument, not the lenient one</b>, which is why a nullable here is not a
	/// guard a caller can satisfy by omission. A non-null identity must equal the identity the request
	/// currently carries. Null requires that no unlapsed claim exists at all, so for every status below
	/// there is no value a caller can pass that writes through a live claim it does not hold.
	/// </para>
	/// <para>
	/// <b>EXCEPT ONE, AND IT IS REACHABLE FROM THIS SURFACE.</b> A write of
	/// <see cref="ErasureRequestStatus.BlockedByLegalHold"/> is admitted REGARDLESS of custody, and it
	/// also RELEASES custody. So a caller holding nothing can take a live executor's request away from it
	/// mid-run, and the executor learns of it only when its next attestation write is refused. That is
	/// deliberate — a hold is a superior authority, and withholding an erasure is recoverable while
	/// destroying held records is not — but it is an exception to the sentence above rather than an
	/// illustration of it. Only this framework's own hold path writes that status today, and it passes
	/// its identity; a consumer writing an admin tool or its own hold re-evaluation reaches it from this
	/// documented surface on any configuration.
	/// </para>
	/// <para>
	/// <i>Superseded wording, quoted so a reader who acted on it recognises it: "There is therefore no
	/// value any caller can pass that writes through a live claim it does not hold." There is exactly
	/// one, it is named above, and the claim was absolute where the code is not.</i>
	/// </para>
	/// <para>
	/// Null is what a SUPERVISOR passes — a scheduler re-offering an abandoned request, or marking one
	/// terminally failed after its retries are exhausted, or a hold re-evaluation resuming a deferred
	/// request. None of those holds a claim, none can obtain one, and each means exactly "do this only if
	/// nobody is working on it".
	/// </para>
	/// </param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>
	/// <see cref="ErasureWriteOutcome.Applied"/> when the row was written;
	/// <see cref="ErasureWriteOutcome.ClaimLost"/> when the claim term did not hold, which is an ordinary
	/// race the caller must stand down from silently; <see cref="ErasureWriteOutcome.StatusRefused"/> when
	/// the transition is illegal or <paramref name="expectedStatus"/> did not match, which while holding
	/// the claim is a caller error worth reporting; and <see cref="ErasureWriteOutcome.NotFound"/> when no
	/// such request exists in this tenant.
	/// </returns>
	/// <exception cref="ErasureStoreNotProvisionedException">
	/// The store's backing schema is absent or stale, so the update cannot be attempted at all. Together
	/// with the custody refusal above, these are the only conditions under which this member does not
	/// return: a request that is not there is reported as
	/// <see cref="ErasureWriteOutcome.NotFound"/> and never as an exception, so a caller can always tell
	/// "no such request" from "this store cannot answer".
	/// </exception>
	/// <exception cref="TenantRequiredException">
	/// Multi-tenancy is active but no ambient tenant is established, so the update cannot be confined to a
	/// tenant. It fails closed rather than mutating across tenants.
	/// </exception>
	Task<ErasureWriteOutcome> UpdateStatusAsync(
		Guid requestId,
		ErasureRequestStatus status,
		ErasureRequestStatus? expectedStatus,
		string? errorMessage,
		string? claimIdentity,
		CancellationToken cancellationToken);

	/// <summary>
	/// Takes custody of a request for execution, in one indivisible step, minting the identity the caller
	/// must present on every attestation write it then makes.
	/// </summary>
	/// <param name="requestId">The request to claim.</param>
	/// <param name="expectedStatus">
	/// The status the request must currently be in. Claiming from
	/// <see cref="ErasureRequestStatus.Scheduled"/> ADVANCES the status to in-progress; claiming from
	/// <see cref="ErasureRequestStatus.AwaitingKeyDestruction"/> LEAVES THE STATUS ALONE, because that
	/// status is a stage of the work rather than a statement about who is doing it.
	/// </param>
	/// <param name="claimDuration">
	/// How long this claim remains live. <see cref="TimeSpan.Zero"/> is LEGAL and yields a claim that has
	/// already lapsed — it is in this contract because it is the only lever a provider-agnostic
	/// conformance test has to construct a lapsed claim without controlling a clock.
	/// </param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>
	/// <see cref="ErasureClaimOutcome.Granted"/> carrying a fresh opaque identity;
	/// <see cref="ErasureClaimOutcome.AlreadyClaimed"/> when another claim is live, which returns of its
	/// own accord once that claim lapses; <see cref="ErasureClaimOutcome.StatusMismatch"/> when the
	/// request is in some other status, which does not; or
	/// <see cref="ErasureClaimOutcome.NotFound"/>. An absent request is a VALUE here and never an
	/// exception.
	/// </returns>
	/// <exception cref="ErasureStoreNotProvisionedException">
	/// The store's backing schema is absent or stale, so the claim cannot be attempted at all.
	/// </exception>
	/// <exception cref="TenantRequiredException">
	/// Multi-tenancy is active but no ambient tenant is established, so the claim cannot be confined to a
	/// tenant. It fails closed rather than claiming across tenants.
	/// </exception>
	/// <remarks>
	/// <para>
	/// <b>ONE indivisible step.</b> The status test, the status write, the identity and the expiry all
	/// happen together or not at all. An implementation that writes the identity in a second operation
	/// has weakened the postcondition in a way no sequential test can observe: two callers can both be
	/// granted, and the loser's writes then pass a fence that is comparing against its own identity.
	/// </para>
	/// <para>
	/// <b>The expiry is written and compared BY THE STORE, from a clock no caller can supply.</b> A
	/// caller-computed instant would make a lapse a comparison between two machines' clocks — the one
	/// comparison that may never decide anything, since a fast clock hands its holder a claim that has
	/// already expired and a slow one holds a claim nobody can take.
	/// </para>
	/// <para>
	/// <b>A LAPSE DOES NOT REVOKE; A RE-CLAIM REVOKES.</b> Nothing tells a holder its claim has lapsed,
	/// and nothing needs to: a holder whose run outlasted its own lease, but whom nobody superseded, still
	/// holds the identity the request carries and its attestation writes still apply. If a lapse revoked
	/// by itself, an executor that had destroyed a subject's keys could be refused its own completion, and
	/// then the erasure would be done with no certificate able to attest it.
	/// </para>
	/// <para>
	/// <b>What this never touches:</b> the staged intents and the destruction ledger. Those record what
	/// has already physically happened and are shared across claims on purpose — see
	/// <see cref="RecordKeyDestroyedAsync"/>.
	/// </para>
	/// </remarks>
	Task<ErasureClaimResult> ClaimForExecutionAsync(
		Guid requestId,
		ErasureRequestStatus expectedStatus,
		TimeSpan claimDuration,
		CancellationToken cancellationToken);

	/// <summary>
	/// Records erasure completion.
	/// </summary>
	/// <param name="requestId">The request ID.</param>
	/// <param name="keysDeleted">Number of keys deleted.</param>
	/// <param name="recordsAffected">Number of records affected.</param>
	/// <param name="certificateId">The generated certificate ID.</param>
	/// <param name="claimIdentity">
	/// The identity of the claim this caller holds. Required, and non-nullable on purpose: this is the
	/// signed attestation, so "I hold no claim" must be INEXPRESSIBLE here rather than merely refused.
	/// Were it nullable, a superseded executor whose successor had also lapsed would satisfy a
	/// no-live-claim test and write a full-coverage certificate for a run it had lost.
	/// </param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>
	/// <see cref="ErasureWriteOutcome.Applied"/> when the completion was recorded, or
	/// <see cref="ErasureWriteOutcome.ClaimLost"/> when another claim has superseded this one — in which
	/// case the caller must stand down silently and write nothing further, including any terminal-failure
	/// record, because the request belongs to somebody else now.
	/// </returns>
	/// <exception cref="KeyNotFoundException">
	/// No request with the given <paramref name="requestId"/> exists in the ambient tenant, or its status
	/// no longer permits completion. This throws rather than returning quietly: a completion is an attestation that
	/// an erasure was carried out, so accepting one for a request that does not exist would record that
	/// data was erased which nobody asked to erase, with nothing anywhere reporting it.
	/// </exception>
	Task<ErasureWriteOutcome> RecordCompletionAsync(
		Guid requestId,
		int keysDeleted,
		int recordsAffected,
		Guid certificateId,
		string claimIdentity,
		CancellationToken cancellationToken);

	/// <summary>
	/// Stages the generation this request is ABOUT TO destroy at <paramref name="keyHandle"/>, durably, BEFORE
	/// the destruction is attempted.
	/// </summary>
	/// <param name="requestId">The request whose pass is about to destroy the key.</param>
	/// <param name="keyHandle">The key handle whose material is about to be destroyed.</param>
	/// <param name="keyGeneration">
	/// The generation identifier of the material about to be destroyed, read from the key backend while that
	/// material still existed.
	/// </param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <remarks>
	/// <para>
	/// <b>The destruction destroys this write's own input, which is why it has to happen first.</b> On every
	/// backend the framework supports, the generation identifier IS backend material — a key-vault version id,
	/// a CMK id, a sidecar marker, an in-memory entry — and none of them is readable once the material is gone.
	/// So a crash between destroying the key and recording its generation is not a window a retry repairs: the
	/// generation is unreadable forever, the retry cannot land the record either, and the subject's ciphertext
	/// is then permanently undecryptable with no way to report their erasure. Staging first is what leaves the
	/// retry something to work from.
	/// </para>
	/// <para>
	/// <b>A staged row is NOT a destruction record and MUST NOT be read as one.</b> Between this call and the
	/// destruction it names LIVE material, so a predicate that could match it would report a live key as
	/// destroyed — the catastrophic direction. Staged intents and destruction records are therefore held apart,
	/// and <see cref="IKeyDestructionLedger.IsGenerationDestroyedAsync"/> resolves only against the records: a
	/// row's existence in the ledger IS the destruction statement, so there is no column to interpret and no
	/// clause to forget.
	/// </para>
	/// <para>
	/// <b>Idempotent, and appends rather than replaces.</b> Staging a generation already staged for the same
	/// request and handle is a no-op. A later pass adds to what earlier passes staged and never truncates it.
	/// </para>
	/// </remarks>
	/// <exception cref="ArgumentException">
	/// Thrown when <paramref name="keyHandle"/> or <paramref name="keyGeneration"/> is null or empty.
	/// </exception>
	/// <exception cref="KeyNotFoundException">
	/// No request with the given <paramref name="requestId"/> exists in this tenant, so there is nothing a
	/// destruction could be staged against.
	/// </exception>
	Task StageKeyDestructionAsync(
		Guid requestId,
		string keyHandle,
		string keyGeneration,
		CancellationToken cancellationToken);

	/// <summary>
	/// Gets the generations this request staged for destruction at <paramref name="keyHandle"/>.
	/// </summary>
	/// <param name="requestId">The request whose staged intents to read.</param>
	/// <param name="keyHandle">The key handle the intents were staged against.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>
	/// The staged generations, in no particular order; empty when nothing was staged for this request and
	/// handle. Never <see langword="null"/>.
	/// </returns>
	/// <remarks>
	/// This is the recovery read, and it is the ONLY member that may name a staged intent. It exists because a
	/// pass that destroyed material and then failed before recording it can no longer read the generation from
	/// the backend — the destruction took it — so the staged row is the only remaining copy. A caller that uses
	/// an answer here to write a destruction record is asserting that the material is gone, and it owes
	/// positive evidence of that: this member reports what was STAGED, which is a statement about an intention,
	/// never about an outcome.
	/// </remarks>
	/// <exception cref="ArgumentException">Thrown when <paramref name="keyHandle"/> is null or empty.</exception>
	Task<IReadOnlyList<string>> GetStagedKeyGenerationsAsync(
		Guid requestId,
		string keyHandle,
		CancellationToken cancellationToken);

	/// <summary>
	/// Records that this request destroyed the generation <paramref name="keyGeneration"/> at
	/// <paramref name="keyHandle"/>, durably, as soon as the destruction returns.
	/// </summary>
	/// <param name="requestId">The request whose pass destroyed the key.</param>
	/// <param name="keyHandle">The key handle whose material is now irrecoverable.</param>
	/// <param name="keyGeneration">
	/// The generation identifier of the destroyed material. This is the ledger's KEY: a generation is minted
	/// once and never reused, so one generation is one destruction and a second row for it is a contradiction
	/// the store refuses. <paramref name="requestId"/> and <paramref name="keyHandle"/> are audit attributes
	/// and are never part of that key.
	/// </param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <remarks>
	/// <para>
	/// <b>This exists because "the key is gone" and "we never held it" are different facts with opposite
	/// consequences, and the key store cannot tell them apart.</b> Asking a provider to delete a key it has
	/// already destroyed reports it as absent — the same answer it gives for a key that never existed. So an
	/// erasure that destroyed a key and then failed part-way through the remaining ones attested that key on
	/// its first pass and, on the retry, attested nothing for it: the subject's data was destroyed and their
	/// erasure could never be reported complete. This record is what lets the retry attest what its own
	/// earlier pass achieved.
	/// </para>
	/// <para>
	/// <b>Written per key, as each destruction returns, not at completion.</b> The pass that needs this record
	/// is the one that did not reach completion, so a write deferred to the end is a write that never happens
	/// in the only case that matters.
	/// </para>
	/// <para>
	/// <b>Idempotent on the GENERATION, and appends rather than replaces.</b> Recording a generation already
	/// recorded is a no-op. Two DIFFERENT generations destroyed at one handle are two destructions and produce
	/// TWO records with their own instants — keying the record on the handle instead would silently drop the
	/// second, which is a destruction that happened and is not on file.
	/// </para>
	/// <para>
	/// A store that cannot persist this cannot certify a retried erasure, and no read of the subject's
	/// ciphertext can ever report their erasure, so this is required rather than an optional capability.
	/// </para>
	/// <para>
	/// <b>DELIBERATELY OUTSIDE THE CLAIM FENCE, and this is stated rather than left to inference.</b>
	/// Every attestation write on the request row is refused when the caller's claim has been superseded.
	/// This one is not, and must never become so. It records a physical act that has ALREADY happened: if
	/// a superseded caller were refused here, the generation it destroyed would be unreadable forever —
	/// the destruction took it — so no successor could record it either, and the subject's erasure would
	/// become permanently unreportable. The sharing across claims is not an oversight: the ledger is keyed
	/// on the generation, which is minted once and never reused, so two claims recording one generation
	/// produce a single row idempotently. That is how a retried erasure attests what an earlier pass
	/// achieved.
	/// </para>
	/// </remarks>
	/// <exception cref="ArgumentException">
	/// Thrown when <paramref name="keyHandle"/> or <paramref name="keyGeneration"/> is null or empty.
	/// </exception>
	Task RecordKeyDestroyedAsync(
		Guid requestId,
		string keyHandle,
		string keyGeneration,
		CancellationToken cancellationToken);

	/// <summary>
	/// Records erasure cancellation.
	/// </summary>
	/// <param name="requestId">The request ID.</param>
	/// <param name="reason">Cancellation reason.</param>
	/// <param name="cancelledBy">Who cancelled.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>True if cancelled, false if not found or already executed.</returns>
	Task<bool> RecordCancellationAsync(
		Guid requestId,
		string reason,
		string cancelledBy,
		CancellationToken cancellationToken);

	/// <summary>
	/// Gets a sub-interface or related service from this store implementation.
	/// </summary>
	/// <param name="serviceType">The type of service to retrieve (e.g. <see cref="IErasureCertificateStore"/>, <see cref="IErasureQueryStore"/>).</param>
	/// <returns>The service instance, or <see langword="null"/> if the store does not implement the requested type.</returns>
	/// <remarks>
	/// This follows the <c>IServiceProvider.GetService</c> escape-hatch pattern from Microsoft design guidelines,
	/// allowing callers to discover optional sub-interfaces without widening the core interface.
	/// </remarks>
	object? GetService(Type serviceType);
}
