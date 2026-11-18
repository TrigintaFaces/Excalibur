// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch;

namespace Excalibur.Compliance;

/// <summary>
/// The durable, write-ahead record of every key handle this framework has INTENDED TO MINT for a data subject:
/// the enumeration an erasure destroys from, and the only thing that makes one read of the retention registry
/// sufficient.
/// </summary>
/// <remarks>
/// <para>
/// <b>The guarantee.</b> Every handle holding a data subject's personal fields has a row here before any key
/// material exists at that handle. An erasure therefore learns the complete set of handles it must decide about
/// by reading this ledger, rather than by re-deriving candidates from configuration that may have changed since
/// the data was written.
/// </para>
/// <para>
/// <b>It records an INTENT, not a completed mint</b>, and the name says so deliberately. A crash between the row
/// and the mint leaves a row for which no key was ever created. That row is correct and permanent: it says this
/// framework intended to place data at that handle, which is exactly what an erasure needs to know.
/// </para>
/// <para>
/// <b>Why that matters, stated as the failure it removes.</b> A subject's fields are placed under a key named by
/// a handle derived from the subject and the obligation class the data belongs to. If the derivation consulted
/// the retention registry, the write and the erasure would each read that registry at their own instant: a
/// declaration added between them leaves the erasure sparing a handle that was never minted while destroying the
/// one holding the data, and a declaration removed between them leaves a live key for a subject reported erased.
/// Neither raises. Both end in a completed certificate. With this ledger the derivation needs no registry read at
/// all, so the two instants cannot disagree.
/// </para>
/// <para>
/// <b>THE ROW IS WRITTEN AND DURABLE BEFORE THE MINT, AND THE ORDER IS THE WHOLE CONTRACT.</b> Mint-then-record
/// admits a crash that leaves live key material with no row — never enumerated, never destroyed, and a
/// certificate issued over it. Record-then-mint leaves a row with no key, which the erasure handles. One order
/// fails silently and catastrophically; the other does not. Implementations MUST NOT reorder this.
/// <b>Returning is not durability:</b> a write acknowledged before it is durable — an unreplicated acknowledgement,
/// a deferred commit, a session-consistency write — reproduces the mint-then-record failure exactly, because a
/// crash loses the row while the key survives. An implementation returns only once the row would survive the loss
/// of the process that wrote it.
/// </para>
/// <para>
/// <b>IMPLEMENTATIONS MUST BE DURABLE, and this is a requirement the sibling ledger does not have.</b> Losing a
/// row here is not the mirror of losing a destruction record — it is the opposite. A lost destruction record
/// fails safe: no tombstone is produced and the read proceeds. A lost mint row fails CATASTROPHICALLY: the
/// erasure enumerates fewer handles than exist, destroys what it found, and certifies. <b>Inverting the polarity
/// inverts which implementations may be volatile</b>, so a store that does not survive a process restart must not
/// supply this ledger in any deployment whose erasures are relied upon.
/// </para>
/// <para>
/// <b>This is the OPPOSITE write order from <see cref="IKeyDestructionLedger"/>, and the asymmetry is not an
/// inconsistency.</b> That ledger records a destruction only AFTER it completed, because a tombstone must be
/// <i>sound</i>: a row asserting a destruction that did not happen reports recoverable data as erased. This
/// ledger must be <i>complete</i>: a handle missing a row is a key nothing will ever look for. Soundness wants
/// the record last; completeness wants it first.
/// </para>
/// <para>
/// <b>A ROW IS NEVER EVIDENCE OF COVERAGE.</b> It says a handle was intended, and nothing more. It is not
/// evidence that the key exists, that it was destroyed, or that an erasure covered it. Only a positive
/// destruction record — or an erasure's own destruction of the handle in the pass that is certifying — discharges
/// a handle. If anything ever reads the ABSENCE of a row as "no key ever existed here" and serves an erasure
/// tombstone on that basis, this ledger has become a tombstone source and the soundness failure described above
/// reappears in full.
/// </para>
/// <para>
/// <b>ROWS ARE INSERT-ONLY AND PERMANENT.</b> There is no update and no delete. Nothing may remove a row — no
/// sweep, no expiry, no administrative reset, and least of all the erasure of the subject it names: a handle that
/// has been destroyed keeps its row, because a later erasure of the same subject must still learn that the handle
/// existed.
/// </para>
/// <para>
/// <b>Keyed by the handle, carrying NO generation and NO obligation class.</b> Existence is a per-handle fact;
/// destruction is a per-generation fact. A row carrying a generation would need updating when a handle is
/// re-minted, which makes the row mutable and rebuilds a create-or-rotate race one layer down. The obligation
/// class is omitted for a different reason — <b>sufficiency, not derivability</b>: the owner and the subject
/// token are the only key an erasure can enumerate by, and the class is not needed for that, so storing it buys
/// nothing. (The class is a derivation input, but so are the owner and the token, and those two ARE stored
/// because the enumeration needs them. An implementer must not "fix" the apparent inconsistency by dropping them.)
/// </para>
/// <para>
/// <b>A handle is at most 81 characters and round-trips exactly.</b> That is the bound the shipped derivation
/// produces — a 64-character digest, optionally composed with a separator and a 16-character obligation-class
/// discriminator. An implementation whose column cannot store 81 characters whole must refuse rather than
/// truncate: a truncated handle is a row that matches no envelope, which is an omission from the enumeration.
/// </para>
/// <para>
/// <b>Consumer obligation: this store is on the write path and MUST fail closed.</b> Recording the intent makes
/// this ledger a write-availability dependency of the first personal-data write per subject and obligation class.
/// The usual rule that cross-cutting infrastructure fails open is inapplicable here: failing open mints a key
/// that nothing will ever enumerate, which is the catastrophic direction. An implementation that cannot write the
/// row throws, and the write fails.
/// </para>
/// <para>
/// <b>Scope: this binds data written after it was adopted.</b> Fields encrypted before this ledger existed have
/// no rows, so an erasure cannot enumerate them from here. Handles derived the old way remain reachable by
/// derivation, and an erasure continues to destroy the subject's own handle unconditionally; what it cannot do is
/// discover a handle it was never told about. <b>An absent row therefore does not distinguish "this handle was
/// never recorded" from "this data predates me", and no caller may treat the absence as either.</b>
/// </para>
/// </remarks>
public interface IKeyMintLedger
{
	/// <summary>
	/// Gets a value indicating whether a recorded row survives the loss of the process that wrote it, so an
	/// enumeration can never silently under-report.
	/// </summary>
	/// <value>
	/// <see langword="true"/> when a row, once recorded, outlives the writing process;
	/// <see langword="false"/> for a ledger that holds its rows only in memory.
	/// </value>
	/// <remarks>
	/// <para>
	/// <b>This exists because the durability requirement above was stated as prose and nothing could check
	/// it.</b> Every clause of that requirement is an obligation on the DEPLOYMENT — "a store that does not
	/// survive a process restart must not supply this ledger" — and a client had no way to tell a durable
	/// ledger from a volatile one, through this interface or through the type. A volatile implementation's
	/// empty enumeration is a CONFORMING answer: the documented meanings of empty are "no fields were ever
	/// written" and "the fields predate this ledger", and "the rows were discarded" is indistinguishable
	/// from both. So the catastrophic direction this contract exists to remove was reachable through an
	/// implementation that violated nothing.
	/// </para>
	/// <para>
	/// <b>It is a property of the implementation, deliberately, and NOT a separately registrable marker.</b>
	/// A capability token that can be registered apart from the thing it attests can be registered
	/// truthfully-looking over an implementation that does not have the property — which is how an
	/// advertised-but-unwired guarantee gets through a startup check that reads the token. Reading it off
	/// the implementation makes a false claim require lying in the implementation itself.
	/// </para>
	/// <para>
	/// <b>A ledger that never answers an enumeration reports <see langword="true"/>, and that is not a
	/// loophole.</b> The property is about whether an enumeration can UNDER-REPORT. An implementation that
	/// refuses to enumerate at all — the one a deployment performing no erasure registers — cannot
	/// under-report, because it never reports. It fails loudly instead, which is the behaviour this property
	/// exists to distinguish from.
	/// </para>
	/// <para>
	/// Startup validation refuses a composition that relies on erasure while its mint ledger reports
	/// <see langword="false"/>, unless the consumer has stated the choice by name. It is not a hint.
	/// </para>
	/// </remarks>
	bool IsDurable { get; }

	/// <summary>
	/// Records, durably and before any key material is created, that this handle is about to hold personal
	/// fields for this data subject.
	/// </summary>
	/// <param name="keyHandle">
	/// The handle the key will be minted at, exactly as the value's envelope will name it, at most 81
	/// characters. A row recorded under a different spelling is a row no erasure will match, which is the
	/// omission this ledger exists to prevent.
	/// </param>
	/// <param name="tenantId">
	/// The resolved OWNER of the data being protected — the same owner the handle was derived from, resolved by
	/// the HOST. It is stored because the handle is an opaque digest: nothing can be recovered from it, so the
	/// enumeration key must be carried alongside.
	/// <para>
	/// <b>An owner is NOT a storage partition, and the distinction is load-bearing rather than pedantic.</b>
	/// This subsystem carries both on one erasure status and uses them four lines apart — the key handle is
	/// derived from the owner, while retention declarations are joined on the partition. In the DEFAULT
	/// single-tenant deployment the two are UNEQUAL: the owner is the resolved default identity and the
	/// partition is the reserved untenanted marker. An implementation that bound the partition here instead
	/// would enumerate the empty set, and the erasure would destroy the subject's plain handle and certify.
	/// The parameter type is what makes that substitution fail to compile.
	/// </para>
	/// </param>
	/// <param name="dataSubjectIdHash">
	/// The data subject's pseudonymisation token, as an erasure request records it. With
	/// <paramref name="tenantId"/> it forms the only key an erasure can enumerate by.
	/// </param>
	/// <param name="cancellationToken">A token to cancel the operation.</param>
	/// <remarks>
	/// <para>
	/// <b>Idempotent by the handle, and a lost race is a SUCCESS.</b> Recording a handle that already has a row
	/// is a no-op, not an error: the first write per subject and obligation class races with every other, and
	/// the row carries nothing a later call could legitimately change. An implementation inserts if absent and
	/// never updates. <b>An implementation MUST NOT surface a duplicate-key error from a lost insert race</b> —
	/// that turns a specified no-op into a failed personal-data write on a path deliberately made fail-closed,
	/// so an insert-if-absent that is not atomic must catch the duplicate and report success.
	/// </para>
	/// <para>
	/// <b>A handle re-recorded under a DIFFERENT owner or subject is REFUSED, loudly.</b> Keeping the first
	/// spelling silently would leave a row whose enumeration key does not match the data it names: the handle
	/// would exist while the correct owner's enumeration returned nothing, which is worse than having no row at
	/// all. A handle is derived from the owner and the subject, so a collision between two of them is either a
	/// derivation defect or a hash collision, and both must be loud.
	/// </para>
	/// <para>
	/// A failure to write is thrown, never swallowed. See the fail-closed obligation on the interface.
	/// </para>
	/// </remarks>
	/// <exception cref="ArgumentException">
	/// Thrown when <paramref name="keyHandle"/> or <paramref name="dataSubjectIdHash"/> is <see langword="null"/>,
	/// empty or whitespace, or when the handle exceeds 81 characters.
	/// </exception>
	/// <exception cref="InvalidOperationException">
	/// Thrown when a row already exists for this handle under a different owner or a different data subject.
	/// </exception>
	Task RecordMintIntentAsync(
		string keyHandle,
		TenantId tenantId,
		string dataSubjectIdHash,
		CancellationToken cancellationToken);

	/// <summary>
	/// Records that the key behind this handle has been created, moving the row from pending to minted.
	/// </summary>
	/// <param name="keyHandle">The handle whose key creation has been confirmed.</param>
	/// <param name="cancellationToken">A token to cancel the operation.</param>
	/// <returns>A task that completes when the row records the confirmation.</returns>
	/// <remarks>
	/// <para>
	/// <b>Called AFTER the key exists, and it is the second half of a two-step write whose first half is
	/// deliberately useless on its own.</b> The intent row cannot say whether the key was created, because
	/// it is written before the creation is attempted. This call is what supplies that fact, and the fact is
	/// what lets an erasure tell a write that is mid-flight from a write that was abandoned — two states
	/// that are otherwise identical in everything an erasure can observe.
	/// </para>
	/// <para>
	/// <b>FAIL-OPEN, and the asymmetry with <see cref="RecordMintIntentAsync"/> is the design.</b> The
	/// intent write fails CLOSED because returning without a durable row would let a key be created that no
	/// erasure enumerates. This write fails OPEN because a row stuck at pending is conservative in the
	/// direction that matters: it withholds a clean certificate rather than granting one. A caller SHOULD
	/// NOT fail a consumer's personal-data write because a confirmation could not be recorded — the key
	/// exists, the row exists, and the only consequence is that erasures for this subject report a
	/// concurrent write until the row is confirmed by a later mint at the same handle.
	/// </para>
	/// <para>
	/// <b>Idempotent, and it never moves a confirmation already on file.</b> Confirming a handle already
	/// marked minted is a no-op; the first confirmation instant stands. Confirming a handle with NO row is
	/// a programming error and is refused — the row is written first, always, so its absence here means the
	/// caller skipped the intent step and a key may exist that nothing enumerates.
	/// </para>
	/// </remarks>
	/// <exception cref="ArgumentException">
	/// Thrown when <paramref name="keyHandle"/> is <see langword="null"/>, empty or whitespace.
	/// </exception>
	/// <exception cref="InvalidOperationException">
	/// Thrown when no row exists for this handle.
	/// </exception>
	Task MarkMintedAsync(string keyHandle, CancellationToken cancellationToken);

	/// <summary>
	/// Lists every handle recorded for this data subject under this owner — the set an erasure must decide
	/// about.
	/// </summary>
	/// <param name="tenantId">The owner the host resolved for this erasure.</param>
	/// <param name="dataSubjectIdHash">The data subject's pseudonymisation token.</param>
	/// <param name="cancellationToken">A token to cancel the operation.</param>
	/// <returns>
	/// The recorded handles at one instant, split into those whose key creation was confirmed and those
	/// still pending, without duplicates and in no guaranteed order. An empty snapshot means no row exists
	/// for this subject under this owner — which covers a subject who never had personal fields written, and
	/// a subject whose fields predate this ledger. Those two are not distinguished here, and neither may be
	/// read as evidence that a subject's data was covered.
	/// </returns>
	/// <remarks>
	/// <para>
	/// <b>Matched on the PARAMETERS ordinally, and on nothing else.</b> No folding, no trimming, no prefix
	/// matching: two owners differing only in case are two owners, and a row belonging to one must never be
	/// enumerated for the other. <b>NO AMBIENT TENANT TERM PARTICIPATES.</b> This enumeration is addressed by
	/// (owner, subject) rather than by the ledger's own key, so unlike every other read on a store of this kind
	/// the owner term is load-bearing rather than redundant — and an ambient term ANDed onto it can only turn
	/// the correct set into a smaller one, which here means handles an erasure fails to destroy.
	/// </para>
	/// <para>
	/// <b>This is a complete enumeration or it is a failure.</b> There is no paging and no limit, because a
	/// truncated answer is a handle an erasure does not destroy — the set is bounded by the obligation classes
	/// one subject's data appears in, which is small. An implementation whose backend pages internally MUST
	/// drain every continuation, and MUST throw rather than return a short set.
	/// </para>
	/// <para>
	/// <b>Being unable to READ the ledger is an exception, never an empty set</b>, and an absent or
	/// uninitialised schema is a read failure rather than an empty answer. Collapsing those into "no rows"
	/// reports a subject as having no handles, which is precisely the state in which an erasure destroys
	/// nothing and certifies.
	/// </para>
	/// <para>
	/// <b>The two sets come from ONE read because the second exists to close a window.</b> See
	/// <see cref="MintLedgerSnapshot"/> for why a pending row is the only observable that can separate a
	/// write in flight from a write abandoned, and why an erasure that finds one must not certify.
	/// </para>
	/// </remarks>
	Task<MintLedgerSnapshot> ListHandlesAsync(
		TenantId tenantId,
		string dataSubjectIdHash,
		CancellationToken cancellationToken);
}
