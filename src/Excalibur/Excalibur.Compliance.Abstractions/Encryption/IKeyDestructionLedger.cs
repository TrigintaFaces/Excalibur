// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0


namespace Excalibur.Compliance;

/// <summary>
/// The durable record of key generations this framework destroyed. It is the sole basis on which a read may
/// report a crypto-shredded field as erased.
/// </summary>
/// <remarks>
/// <para>
/// <b>The guarantee.</b> A tombstone -- a decrypt answering <see langword="null"/> for a personal field, which
/// a consumer reads as a discharged erasure -- is produced only in states where a durable record, written by
/// the actor that performed the destruction, says that generation was destroyed. The ledger holds those
/// records and nothing else. A row's existence <i>is</i> the statement that the destruction happened, so there
/// is no column to interpret and no predicate clause to forget.
/// </para>
/// <para>
/// <b>Why this is not asked of the key backend.</b> A backend can only report what it is able to serve or
/// restore now. It retains nothing about material it never held, so "we destroyed this" and "this was never
/// here" are the same observation there -- permanently, and by construction, not for want of care. Deriving a
/// tombstone from a backend query therefore computes a different function from the one the guarantee names,
/// and its failure mode is the catastrophic one: recoverable personal data reported as lawfully erased, with
/// nothing downstream able to tell. A backend's own attestation is a separate question with a separate
/// consumer -- see <see cref="IKeyDestructionStatusProvider"/>, which erasure verification asks about a handle
/// at completion time and which a read must never consult.
/// </para>
/// <para>
/// <b>The unit is a HANDLE AND a generation together, and neither alone works.</b> A handle or a version
/// ordinal ALONE describes the handle as it is now: destroy a subject's key, let an ordinary write provision
/// another at the same handle, and both report "not destroyed" -- truthfully, about material the reader is not
/// holding. A generation ALONE rests on an assumption nothing enforces: that a generation identifies material
/// uniquely across every handle. The shipped providers all satisfy it, because they mint from
/// <see cref="System.Security.Cryptography.RandomNumberGenerator"/> through <see cref="KeyGeneration.Mint"/>,
/// but <see cref="KeyGeneration"/> constrains SHAPE and not ENTROPY -- so a provider that DERIVED its
/// generation from the subject would yield one identifier for two distinct handles, one ledger row, and one
/// tenant's destruction reporting another tenant's live data as erased.
/// </para>
/// <para>
/// Keying on the pair removes that obligation rather than documenting it. Two distinct handles cannot share a
/// row whatever a provider does, and the pair keeps everything the generation alone bought: many generations
/// may exist at one handle, so a destroyed generation stays destroyed whatever is provisioned at that handle
/// afterwards, and a SECOND destruction at one handle is a distinct row rather than a dropped one.
/// </para>
/// <para>
/// <b>Consumer obligation.</b> A tombstone requires a ledger row, and a ledger row is written only by an
/// erasure this framework performed. A consumer who provisioned or destroyed keys outside this framework has
/// no row for those generations, so a read of their ciphertext FAILS LOUDLY rather than reporting an erasure.
/// That is the intended behaviour and it is not a gap to work around: we will not claim an erasure we did not
/// perform. A consumer migrating such data, or destroying keys through their own process, records those
/// destructions themselves with <see cref="RecordDestroyedGenerationAsync"/>, or accepts that those reads fail.
/// </para>
/// <para>
/// <b>Two writers, and they do NOT carry the same evidence.</b> An erasure this framework performs stages the
/// generation before destroying it and records it only once the destruction is established, so the row rests
/// on the framework's own observation of both halves. <see cref="RecordDestroyedGenerationAsync"/> rests on the
/// CALLER's assertion instead, and nothing re-verifies it. The framework's own erasure path never uses it. The
/// two are kept apart deliberately: collapsing the erasure service onto the weaker-evidenced member would
/// discard the staged-intent evidence that makes its rows sound.
/// </para>
/// <para>
/// <b>Caching direction.</b> This sits on the hot path of every field decrypt, so an implementation may cache.
/// Ledger rows are immutable once written, so a cached <see langword="true"/> can never become wrong. A cached
/// <see langword="false"/> is a cached "we have no record", which is the safe direction and may be held
/// briefly. An implementation MUST NOT cache a failure to reach the ledger as either answer -- that is how a
/// transient outage outlives its cause.
/// </para>
/// </remarks>
public interface IKeyDestructionLedger
{
	/// <summary>
	/// States whether the ledger records that this generation's key material was destroyed.
	/// </summary>
	/// <param name="keyHandle">
	/// The handle of the key the ciphertext was produced under, as carried on its envelope
	/// (<see cref="EncryptedData.KeyId"/>).
	/// <para>
	/// <b>It is a key component, not a filter.</b> Without it the answer rests on a generation identifying
	/// material uniquely across every handle -- true of every shipped provider, which mint from a
	/// cryptographic random source, and unenforceable for a consumer-supplied one. A provider deriving its
	/// generation would give two tenants' distinct keys one identifier, one row, and one tenant's destruction
	/// would report the other's live data as erased. Supplying the handle makes that state unreachable
	/// regardless of how a provider produces its generations.
	/// </para>
	/// <para>
	/// Pass the handle the ENVELOPE names, not one re-derived from the subject: the two agree for a value
	/// this framework wrote, and where they could disagree the envelope is the one that identifies the
	/// material actually being read.
	/// </para>
	/// </param>
	/// <param name="keyGeneration">
	/// The generation identifier the ciphertext was produced under, as carried on its envelope and reported by
	/// <see cref="KeyMetadata.Generation"/>.
	/// </param>
	/// <param name="cancellationToken">A token to cancel the operation.</param>
	/// <returns>
	/// <see langword="true"/> only when the ledger holds a record that THIS handle's THIS generation was
	/// destroyed. <see langword="false"/> when it holds no such record -- which covers a generation that is
	/// live, one destroyed outside this framework, one that never existed, and one destroyed at a DIFFERENT
	/// handle. Those four are not distinguished, and none of them may produce a tombstone.
	/// </returns>
	/// <remarks>
	/// <para>
	/// <b>A row's existence IS the destruction statement.</b> There is no status column, no flag and no
	/// timestamp comparison to interpret: a row is written only after a destruction completed irreversibly, so
	/// the question this answers is whether the row is there. That is what leaves no clause to forget.
	/// </para>
	/// <para>
	/// <b>No row is <see langword="false"/>. Being unable to READ the ledger is an exception.</b> The two are
	/// different outcomes and MUST NOT be collapsed. An implementation that returns <see langword="false"/>
	/// because the ledger was unreachable produces a value indistinguishable, in logs and in metrics, from
	/// "asked, and there is no row" -- and absence read as an answer is the whole failure history of this seam.
	/// An unreachable ledger throws, naming the cause.
	/// </para>
	/// <para>
	/// <b>The claim is scoped to the ENVELOPE, not to the subject.</b> A <see langword="true"/> means the
	/// plaintext behind <i>this envelope</i> is unrecoverable. It does NOT mean the subject is erased: a
	/// plaintext copy held elsewhere, or a second ciphertext written under a different key, leaves this answer
	/// correct and an erasure claim false. Whether a subject is erased is a question about every copy of their
	/// data, and nothing here answers it.
	/// </para>
	/// <para>
	/// <b>An affirmative answer is monotone, so it never needs invalidating.</b> A destroyed generation cannot
	/// become undestroyed: ledger rows are only ever added, and there is no update and no delete. Do NOT add
	/// cache-invalidation logic for a <see langword="true"/> -- there is no state change for it to react to.
	/// Nothing in this member's name tells you that, which is exactly why it is written here. A
	/// <see langword="false"/> is the opposite and must not inherit the same treatment: it means "no row YET",
	/// so it may be held only briefly.
	/// </para>
	/// <para>
	/// That asymmetry is why the return is a <see cref="ValueTask{TResult}"/> rather than a
	/// <see cref="Task{TResult}"/>. This is asked once per annotated field on every read, and because an
	/// affirmative answer may be held indefinitely the common path completes synchronously from cache, where a
	/// <see cref="Task{TResult}"/> would allocate on every hit.
	/// </para>
	/// </remarks>
	ValueTask<bool> IsGenerationDestroyedAsync(string keyHandle, string keyGeneration, CancellationToken cancellationToken);

	/// <summary>
	/// Records that this generation's key material has been irreversibly destroyed, on the caller's assertion.
	/// </summary>
	/// <param name="keyHandle">
	/// The handle of the key whose material was destroyed.
	/// <para>
	/// REQUIRED, and a caller asserting a destruction has it: you cannot destroy a key without naming it. It
	/// was previously absent, which left the row keyed on the generation alone and the tombstone resting on an
	/// assumption no provider is obliged to satisfy. It must be the handle an envelope written under that key
	/// NAMES, because that is the handle a read will ask about -- a destruction recorded under a different
	/// spelling leaves those reads FAILING rather than reporting the erasure, which is the safe direction and
	/// not the one you intended.
	/// </para>
	/// </param>
	/// <param name="keyGeneration">
	/// The generation identifier of the destroyed material, as it was reported by
	/// <see cref="KeyMetadata.Generation"/> while the material still existed. With
	/// <paramref name="keyHandle"/> it forms the record's whole key: a generation is minted once and never
	/// reused, so recording one pair twice is a no-op rather than an error, while a SECOND destruction at the
	/// same handle is a different generation and therefore a distinct record.
	/// </param>
	/// <param name="cancellationToken">A token to cancel the operation.</param>
	/// <remarks>
	/// <para>
	/// <b>THIS IS AN ASSERTION YOU OWN.</b> By calling it you are asserting that an irreversible destruction of
	/// this generation's material has already completed. On the strength of this row alone, this framework will
	/// report every field encrypted under that generation as lawfully erased, to your users and on your
	/// compliance evidence, and it will NOT re-verify the assertion — not now and not at read time. If the
	/// material still exists, or can be restored, you have published a false erasure claim.
	/// </para>
	/// <para>
	/// <b>Why this exists rather than being walled off.</b> Under the erasure right the CONTROLLER owns the
	/// claim, not this framework. Our obligation is never to fabricate the claim ourselves, which is why no
	/// backend query may produce a tombstone; it is not to prevent you from asserting a destruction you
	/// performed and are answerable for. A consumer who crypto-shreds through their own process, or who
	/// migrates data erased before adopting this framework, has no erasure request for those generations and so
	/// cannot use the request-scoped path — without this member their correctly-erased subjects' reads would
	/// fail forever with no way to say so.
	/// </para>
	/// <para>
	/// <b>Call it AFTER the destruction returns, never before.</b> A row written ahead of the destruction
	/// reports live material as erased for however long the gap lasts, which is the one failure this ledger
	/// exists to make impossible. If you cannot read the generation after destroying — on most backends you
	/// cannot, because the identifier is backend material the destruction takes with it — read it first, keep
	/// it, destroy, then record.
	/// </para>
	/// <para>
	/// A failure to write is thrown, never swallowed. A caller that believes it recorded a destruction it did
	/// not would see the subject's reads fail with no indication why.
	/// </para>
	/// </remarks>
	/// <exception cref="ArgumentException">
	/// Thrown when <paramref name="keyHandle"/> or <paramref name="keyGeneration"/> is <see langword="null"/>
	/// or empty.
	/// </exception>
	Task RecordDestroyedGenerationAsync(string keyHandle, string keyGeneration, CancellationToken cancellationToken);
}
