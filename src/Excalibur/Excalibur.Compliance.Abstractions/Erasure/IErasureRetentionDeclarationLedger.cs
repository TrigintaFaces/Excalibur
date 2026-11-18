// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

namespace Excalibur.Compliance;

/// <summary>
/// One retention declaration, named the way a durable record must name it — the tenant that owes the
/// obligation and the aggregate type it covers.
/// </summary>
/// <param name="TenantId">
/// The tenant partition the declaration names, exactly as the registry normalises it. Every spelling of
/// "untenanted" collapses onto one value before it reaches here, so a single-tenant deployment matches its
/// own declarations.
/// </param>
/// <param name="AggregateType">The aggregate type the obligation covers, as the event store records it.</param>
/// <remarks>
/// <b>Both terms, always, and the pair is the unit.</b> The aggregate type alone is the WRITE path's
/// question — whether to place a subject's fields under a separately destroyable key — and that question is
/// deliberately tenant-blind. Whether an erasure may SPARE a key is a different question, decided per
/// (tenant, type) against the tenant the erasure request itself recorded. One durable record of pairs
/// answers both, the second directly and the first by projecting the tenant away; two records would be two
/// things to keep in step.
/// </remarks>
public readonly record struct DeclaredRetentionKey(string TenantId, string AggregateType);

/// <summary>
/// One recorded declaration, with the fact a reader needs about it: whether it has been withdrawn.
/// </summary>
/// <param name="Declaration">The (tenant, aggregate type) pair.</param>
/// <param name="IsWithdrawn">
/// <see langword="true"/> when an administrator has withdrawn this declaration; otherwise
/// <see langword="false"/>.
/// </param>
/// <remarks>
/// <para>
/// <b>THERE ARE THREE STATES PER PAIR AND A CALLER NEEDS ALL THREE.</b> Two live on this record —
/// live and withdrawn — and the third is ABSENCE from the collection, meaning no process in this
/// deployment has ever recorded the pair. Collapsing withdrawn into absent is the defect this type
/// exists to prevent: an instance whose code still declares a withdrawn retention is the mirror of
/// the stale instance, and a caller that cannot see the tombstone cannot detect it at all.
/// </para>
/// <para>
/// <b>Withdrawal is MONOTONE, so a withdrawn reading is a TERMINAL fact rather than a stale one.</b>
/// The advance path never updates and the withdrawal keeps its first instant, so once this reads
/// <see langword="true"/> it reads <see langword="true"/> forever. That is what makes it safe to act
/// on: the reader is not racing anything.
/// </para>
/// </remarks>
public readonly record struct RecordedRetentionDeclaration(
	DeclaredRetentionKey Declaration,
	bool IsWithdrawn);

/// <summary>
/// What a withdrawal did, which is three distinguishable outcomes rather than two.
/// </summary>
/// <remarks>
/// <b>A <see langword="bool"/> would merge the first two and lose the one an auditor asks about.</b>
/// Withdrawing a declaration that was never recorded is not the same fact as withdrawing one that is
/// already withdrawn, and neither is the same as performing the withdrawal — so the caller, and the audit
/// trail behind it, can tell what happened.
/// </remarks>
public enum RetentionWithdrawalOutcome
{
	/// <summary>The declaration was recorded and live; it is now tombstoned.</summary>
	Withdrawn,

	/// <summary>The declaration was already tombstoned. Nothing changed, and the original withdrawal's recorded facts are untouched.</summary>
	AlreadyWithdrawn,

	/// <summary>No process in this deployment has ever recorded this declaration, so there was nothing to withdraw.</summary>
	NotDeclared,
}

/// <summary>
/// The durable record of which retention declarations <i>some</i> process in this deployment has witnessed
/// — so an instance whose own view is demonstrably behind the deployment's refuses to destroy a key rather
/// than destroying one the deployment is obliged to keep.
/// </summary>
/// <remarks>
/// <para>
/// <b>The guarantee, and it is deliberately narrower than the one a reader will expect.</b> No erasure
/// proceeds on a view that is KNOWN-STALE. <i>Not</i> "on a current view" — that stronger sentence
/// quantifies over a deployment-wide instant at which a declaration is "in force", and no such instant
/// exists for a declaration written in code and deployed as code. This ledger never asks for one. It asks a
/// question that durable state can answer on its own: <b>has any process ever witnessed more than I
/// witness.</b> No clock, no ordering, no coordination.
/// </para>
/// <para>
/// <b>The failure it removes, in the consumer's terms.</b> Retention declarations live in CODE; the data
/// they govern lives in a store. So a rolling deploy that ADDS a retention has a window in which two
/// instances disagree about the law. An instance predating the declaration enumerates the subject's
/// already-widened key handle from the mint ledger, holds no local declaration that would spare it, and
/// irreversibly destroys the key — for an aggregate type that deploy declares retained, under a completion
/// certificate attesting a lawful erasure. Nothing downstream ever learns: the ciphertext is simply
/// unrecoverable, and an unrecoverable field is exactly what this framework reports as a discharged
/// erasure.
/// </para>
/// <para>
/// <b>Why this subsystem needs a durable record at all, stated as the asymmetry rather than as a
/// preference.</b> Everything else here that governs destruction is already durable — the erasure store,
/// the legal holds, the key mint ledger, the token-function fingerprint. The retention declaration was the
/// only input to an irreversible act that existed solely as a local snapshot. That asymmetry is the defect;
/// this contract removes it.
/// </para>
/// <para>
/// <b>IT IS NOT WRITE-ONCE, and copying the fingerprint ledger's shape here would be the error.</b> A
/// token-function fingerprint must never change: a change orphans every record, so write-once is its
/// invariant. A retention declaration set is different in kind — <b>it legitimately GROWS</b>, because
/// adding retentions over time is the supported workflow. The primitive is therefore insert-if-absent,
/// enforced by a unique key alone, with no trigger and no immutability claim.
/// </para>
/// <para>
/// <b>THE TENANT GOES IN THE PRIMARY KEY AND NOWHERE ELSE.</b> The pair is the identity of a row, so the
/// key addresses it completely — and a tenant predicate added to a statement the key already addresses can
/// only ever turn the correct row into zero rows. That is not a hypothetical: a framework-wide sweep that
/// added tenant terms uniformly is what stopped the outbox marking messages it had claimed. The acceptance
/// criterion for tenancy here is "a new provider that omits the tenant from the key fails", never "every
/// statement carries a predicate".
/// </para>
/// <para>
/// <b>Cost, stated rather than assumed.</b> Both operations are cold. The advance runs once per process at
/// startup, over a set the registry's own reasoning describes as small, known at startup and fully
/// enumerable. The read runs once per erasure, which is a rare, already-durable, already-multi-round-trip
/// operation. <b>Nothing here executes on a per-message, per-event or per-dispatch path, so no benchmark is
/// owed</b> — and if a later change puts either call on such a path, that change owes one.
/// </para>
/// <para>
/// <b>A volatile store does not implement this.</b> An in-memory deployment is one process, so no other
/// process can hold a view it is behind, and there is nothing for a comparison to be against. Supplying a
/// satisfiable record there would report a passing staleness check that could never fail, which is worse
/// than having none — so the contract is simply absent, and a comparison with nothing to read is correct
/// rather than blind.
/// </para>
/// </remarks>
public interface IErasureRetentionDeclarationLedger
{
	/// <summary>
	/// Records every declaration in <paramref name="declarations"/> that this deployment has not recorded
	/// before, leaving the rest exactly as they are.
	/// </summary>
	/// <param name="declarations">This process's own declared retentions, as its registry holds them.</param>
	/// <param name="cancellationToken">A token to cancel the operation.</param>
	/// <remarks>
	/// <para>
	/// <b>IT NEVER REFUSES ANYTHING.</b> An advance is a WRITE, not a gate — that separation is what makes
	/// this safe to deploy rather than hostile to deploy. A refusal here would mean the first updated
	/// instance to boot halts every instance that predates it, which is an outage; the comparison that CAN
	/// refuse lives on the erasure path, where it withholds only the irreversible act and only from the
	/// instances that cannot perform it correctly.
	/// </para>
	/// <para>
	/// <b>CALLED BEHIND A PER-PROCESS LATCH, BEFORE THIS PROCESS EXECUTES ITS FIRST ERASURE — NOT AS A
	/// PRECONDITION OF SERVING.</b> The weaker precondition buys the same invariant: what the comparison
	/// needs is that an updated instance has advanced before any erasure runs anywhere, and an instance
	/// that never erases has nothing to contribute. Binding this to startup instead would make the
	/// application's readiness depend on the compliance store's availability, so a store outage would stop
	/// new instances serving and could stall a rollout — a liveness cost for no safety gain. The precedent
	/// contract this one follows says the same thing of itself: callers invoke it once per process behind
	/// their own initialisation latch.
	/// </para>
	/// <para>
	/// <b>ONE ROW PER PAIR, and N independent insert-if-absent operations — never one set-valued row.</b>
	/// A set-valued row makes advancing a read-modify-write, and therefore a lost update: two processes
	/// starting together each read the set, each write their own union, and the first one's additions are
	/// gone. Per-pair rows make the union form BY CONSTRUCTION — for each pair exactly one insert wins and
	/// every other is a no-op — so there is no read in the write path at all, no compare-and-set and no
	/// version column. Two processes starting together is the case the primitive is specified for, not an
	/// edge case to survive.
	/// </para>
	/// <para>
	/// <b>The test and the write are ONE indivisible operation, and a lost race is a SUCCESS.</b> An
	/// implementation whose insert-if-absent is not atomic in the substrate MUST catch the duplicate-key
	/// violation and continue rather than surfacing it: surfacing it turns a specified no-op into a startup
	/// failure on a correctly configured deployment. It must catch <b>only</b> that violation, identified
	/// precisely — a blanket catch would turn a transient fault into a silent pass, and a silently skipped
	/// advance is a declaration this deployment never records.
	/// </para>
	/// <para>
	/// <b>THIS PATH CONTAINS NO UPDATE. Insert-if-absent, never upsert.</b> Load-bearing, and it is the
	/// whole mechanism that stops a withdrawal being resurrected: a withdrawal tombstones the row rather
	/// than deleting it, so the tombstone OCCUPIES the key, and an instance whose code still declares a
	/// withdrawn retention conflicts and does nothing. Change this to an update and a stale deploy silently
	/// re-declares an obligation an administrator deliberately withdrew. One token, and it owes its own
	/// arm.
	/// </para>
	/// <para>
	/// <b>Partial application is SAFE here, and that is why this returns nothing.</b> Each pair is
	/// independent, so a failure part-way leaves a prefix recorded and the remainder not — and since the
	/// operation is idempotent, the next process to start, or the next restart of this one, records the
	/// rest. There is no partially-applied state a caller could act on differently, so there is no outcome
	/// to report. It throws on failure, because a startup that cannot record its own declarations has not
	/// established the premise the erasure path will rely on.
	/// </para>
	/// </remarks>
	/// <exception cref="ArgumentNullException"><paramref name="declarations"/> is <see langword="null"/>.</exception>
	/// <exception cref="ArgumentException">
	/// A declaration carries a <see langword="null"/>, empty or whitespace tenant or aggregate type. A blank
	/// term would be a row every misconfigured deployment agreed on.
	/// </exception>
	/// <exception cref="ErasureStoreNotProvisionedException">
	/// The backing table is absent and this store is not configured to provision it.
	/// </exception>
	Task RecordDeclaredRetentionsAsync(
		IReadOnlyCollection<DeclaredRetentionKey> declarations,
		CancellationToken cancellationToken);

	/// <summary>
	/// Lists every declaration this deployment has recorded for <paramref name="tenantId"/>, live and
	/// withdrawn alike, each carrying which it is.
	/// </summary>
	/// <param name="tenantId">The tenant partition the erasure request itself recorded.</param>
	/// <param name="cancellationToken">A token to cancel the operation.</param>
	/// <returns>
	/// Every declaration recorded for that tenant, with its withdrawn state. Empty means this deployment
	/// has recorded none for it, which is a real and common answer — it never means the question could not
	/// be answered.
	/// </returns>
	/// <remarks>
	/// <para>
	/// <b>CALLED ON THE ERASURE PATH, AND IT IS READ-ONLY.</b> The caller compares this against its own
	/// registry and refuses THIS erasure when the record holds a declaration it does not hold. That takes
	/// the write off the irreversible path entirely: advances happen at startup, never concurrently with a
	/// destruction.
	/// </para>
	/// <para>
	/// <b>FILTERED TO THE REQUESTING TENANT, deliberately, and the unfiltered version over-refuses.</b> It
	/// is tempting to refuse on any declaration this instance is missing, whoever declared it, on the
	/// reasoning that mis-widening is cross-tenant by construction. The MECHANISM is cross-tenant; the HARM
	/// is not. Only the requesting tenant's declarations ever decide whether a handle is spared, so a
	/// declaration belonging to another tenant cannot change this erasure's outcome — and refusing over it
	/// would withhold an erasure that would have been performed correctly. <b>An over-refused erasure is a
	/// subject not erased</b>, which has its own regulator: fail-closed is not a free move on a path whose
	/// job is destruction, because both directions do harm.
	/// </para>
	/// <para>
	/// <b>A WITHDRAWN DECLARATION IS RETURNED, FLAGGED, AND IS NOT THE SAME AS AN ABSENT ONE.</b> The row
	/// survives as a tombstone so the two facts stay distinguishable to an auditor, and so the advance path
	/// cannot re-create it — and the caller needs to SEE it. An instance whose own code still declares a
	/// withdrawn retention is the stale instance mirrored, and it will spare rows whose key another actor
	/// is entitled to destroy; a read that hid the tombstone would make that case undetectable.
	/// <i>(This paragraph previously said a withdrawn declaration is NOT returned, and that the read
	/// collapses the distinction "in the direction the caller needs". That was wrong: it made the mirror
	/// case inexpressible, which an adversarial review caught before any provider implemented it.)</i>
	/// </para>
	/// <para>
	/// <b>FAIL CLOSED: THROW. NEVER RETURN EMPTY BECAUSE THE READ FAILED.</b> This is the obligation most
	/// likely to be dropped, and dropping it inverts the whole contract. An enumeration that could not be
	/// completed is indistinguishable from a deployment that has declared nothing — and "nothing declared"
	/// is precisely the answer that lets the destruction proceed. A swallowed failure certifies over a key
	/// it should have spared.
	/// </para>
	/// <para>
	/// <b>BUT A THROW IS NOT BY ITSELF A DELAY, AND THE CALLER MUST NOT ASSUME IT IS.</b> Whether the
	/// erasure is retried afterwards is a property of the EXECUTING SCHEDULER, not of this contract: a
	/// scheduler that counts a throw against a bounded retry budget turns a sequence of honest refusals
	/// into a permanently failed erasure, which is a subject never erased. So a caller on a destruction
	/// path must give a refusal its own non-terminal disposition rather than letting it surface as a
	/// generic failure. <i>(An earlier revision of this paragraph asserted that "a throw leaves the erasure
	/// non-terminal and retryable". That is false of the shipped scheduler and is corrected here rather
	/// than quietly deleted, because it is the reassuring reading and it was committed.)</i>
	/// </para>
	/// </remarks>
	/// <exception cref="ArgumentException"><paramref name="tenantId"/> is <see langword="null"/>, empty or whitespace.</exception>
	/// <exception cref="ErasureStoreNotProvisionedException">
	/// The backing table is absent and this store is not configured to provision it. An erasure that cannot
	/// read the deployment's declarations must not proceed — the alternative is to assume it is current,
	/// which is the assumption this ledger exists to stop anyone making.
	/// </exception>
	Task<IReadOnlyCollection<RecordedRetentionDeclaration>> ListDeclaredRetentionsAsync(
		string tenantId,
		CancellationToken cancellationToken);
}

/// <summary>
/// Withdraws a retention declaration — the deliberate administrative statement that a deployment is no
/// longer obliged to keep an aggregate type's data for a tenant.
/// </summary>
/// <remarks>
/// <para>
/// <b>THIS IS A SEPARATE CONTRACT SO THAT A WITHDRAWAL IS NOT EXPRESSIBLE BY THE CODE THAT DEPLOYS.</b>
/// A withdrawal reverses a legal obligation, so it must never be something a deploy accomplishes as a side
/// effect — and the way to guarantee that is not a rule asking implementers to be careful. Neither the
/// startup advance nor the erasure path resolves this interface, so neither can call it: the violation is
/// not prevented by a check, it cannot be written. An administrative host resolves it deliberately.
/// </para>
/// <para>
/// <b>Rolling a deploy BACK does not withdraw anything, and that is the intended behaviour.</b> After a
/// rollback the durable record is ahead of every running instance, so every instance is known-stale and
/// erasures are withheld — which is the mechanism working, not failing. The remedy is an explicit
/// withdrawal, which is a recorded act with the same standing as the declaration it reverses. An operator
/// override would be a hole wide enough to make the guard theatre; a withdrawal is the narrow, auditable
/// door that does the same job honestly.
/// </para>
/// <para>
/// <b>It tombstones; it never deletes.</b> Deleting the row would make "withdrawn" indistinguishable from
/// "never declared", which are different facts to an auditor — and it would free the key for the advance
/// path to re-insert, silently restoring the obligation on the next deploy of any instance whose code still
/// declares it.
/// </para>
/// </remarks>
public interface IErasureRetentionWithdrawalAuthority
{
	/// <summary>
	/// Tombstones <paramref name="declaration"/>, recording who withdrew it, when, and why.
	/// </summary>
	/// <param name="declaration">The declaration to withdraw.</param>
	/// <param name="actor">
	/// The administrator or system accountable for the withdrawal. Recorded verbatim, because the auditor's
	/// question is who decided, not which process executed it.
	/// </param>
	/// <param name="reason">
	/// Why the obligation no longer applies. Required, and not a formality: a tombstone with no reason
	/// records that an obligation ended and nothing about the ground for ending it, which is the half an
	/// auditor needs.
	/// </param>
	/// <param name="cancellationToken">A token to cancel the operation.</param>
	/// <returns>What the withdrawal did — see <see cref="RetentionWithdrawalOutcome"/>.</returns>
	/// <remarks>
	/// <b>The four recorded facts are the pair, the instant, the actor and the reason</b>, and all four are
	/// the auditor's question rather than ours. <see cref="RetentionWithdrawalOutcome.AlreadyWithdrawn"/>
	/// leaves the original four untouched: a second withdrawal does not re-date the first, because the
	/// instant that matters is when the obligation ended.
	/// </remarks>
	/// <exception cref="ArgumentException">
	/// Any of the declaration's terms, <paramref name="actor"/> or <paramref name="reason"/> is
	/// <see langword="null"/>, empty or whitespace.
	/// </exception>
	/// <exception cref="ErasureStoreNotProvisionedException">The backing table is absent.</exception>
	Task<RetentionWithdrawalOutcome> WithdrawDeclaredRetentionAsync(
		DeclaredRetentionKey declaration,
		string actor,
		string reason,
		CancellationToken cancellationToken);
}
