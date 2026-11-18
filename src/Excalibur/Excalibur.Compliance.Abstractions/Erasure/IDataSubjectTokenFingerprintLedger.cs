// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

namespace Excalibur.Compliance;

/// <summary>
/// The durable, write-once record of which data-subject token function produced the pseudonymisation
/// tokens in one store — so a process whose token function differs is refused instead of silently writing
/// records nothing will ever find again.
/// </summary>
/// <remarks>
/// <para>
/// <b>The guarantee.</b> A store that holds token-keyed records also holds exactly one fingerprint of the
/// token function that produced them, recorded the first time the store was used and never changed
/// afterwards. A process whose own token function fingerprints differently cannot perform a token-bound
/// operation against that store.
/// </para>
/// <para>
/// <b>The failure it removes, in the consumer's terms.</b> Every durable compliance record — erasure
/// requests, the key-destruction ledger, the key-mint ledger an erasure enumerates from, legal holds, and
/// the data inventory — is keyed by a pseudonymisation token. Change the token function and every one of
/// those keys changes at once: the mint-ledger enumeration returns empty, so an erasure destroys only what
/// it can derive from declared retentions and then signs a completion certificate over everything it
/// missed; a legal-hold lookup returns empty, so the erasure proceeds over data the law required kept; and
/// already-encrypted personal data is no longer decryptable. None of that throws and none of it is logged.
/// <b>Ordinary quarterly secret-rotation hygiene applied to the hashing key is sufficient to cause it.</b>
/// </para>
/// <para>
/// <b>A FINGERPRINT OF BEHAVIOUR, NOT OF CONFIGURATION.</b> The value recorded here is derived by putting
/// one fixed canary identifier through the <i>registered</i> hasher and running the result through a
/// key-derivation function. It is deliberately not derived from configuration: the hasher contract is
/// public and registered first-one-wins, so a consumer's own implementation can be the one producing
/// tokens while the configured key is read by nobody. A fingerprint of configuration would then pass while
/// the invariant was violated. Fingerprinting output also detects a changed ALGORITHM, which a
/// configuration check cannot see at all.
/// </para>
/// <para>
/// <b>IT IS NOT A SECRET AND MUST NOT BE DESCRIBED AS PROTECTING ONE.</b> The canary is a framework
/// constant, so anyone who can read a recorded fingerprint has the same offline oracle they would have had
/// from any token whose input they can guess. It is no more sensitive than the tokens stored beside it, and
/// its derivation adds no secrecy.
/// </para>
/// <para>
/// <b>WRITE-ONCE. The invariant is that a recorded fingerprint NEVER changes</b>, and the claim below
/// depends on it rather than merely benefiting from it. The claim is an insert-if-absent followed by a
/// read of what is now recorded; if any statement anywhere could update the value, two processes can
/// interleave so that the one with the CORRECT function refuses while the one with the WRONG function
/// proceeds. So no implementation may expose an update or delete path, by any route, including an
/// administrative one.
/// </para>
/// <para>
/// <b>NO TENANT TERM, DELIBERATELY, AND NOT AN OVERSIGHT.</b> The token function is a property of the
/// process, not of a tenant, so there is no honest tenant term to carry — and a per-tenant fingerprint
/// would be actively harmful: a tenant onboarded after a rotation would record the new function as correct
/// while every earlier tenant's records were already orphaned, and the check would then pass forever on
/// exactly the deployment where the damage is worst. Implementations store it in a table with <b>no tenant
/// column at all</b>, so that a tenant predicate is inexpressible rather than merely discouraged, and an
/// attempt to add one fails loudly at the database instead of silently narrowing one row to none.
/// </para>
/// <para>
/// <b>ONE LEDGER PER TOKEN-KEYED STORE, NOT ONE PER DEPLOYMENT.</b> The erasure, legal-hold and
/// data-inventory stores are registered independently and each take their own connection, so they may sit
/// on different databases. A single claim could therefore only ever speak for the database it was made
/// against. Each store supplies its own ledger and a verifier resolves them as a set; a deployment with
/// three token-keyed databases holds three fingerprints and makes three claims. A verifier that resolved
/// only one would report a green check covering a third of a consumer's records.
/// </para>
/// <para>
/// <b>Known gap: this cannot cover key NAMES.</b> Per-subject encryption keys are named by a handle in a
/// key vault, which has nowhere to put a row beside it. So a changed token function is detected for
/// RECORDS and not for key names, and the consequence that survives detection is that personal data
/// already encrypted under a handle derived from the old function stays undecryptable. Stated here because
/// a consumer told "a changed hashing key is detected" and given "detected for records only" has been
/// told something false by omission.
/// </para>
/// <para>
/// <b>A volatile store does not implement this.</b> An in-memory store's records do not outlive the
/// process that wrote them, so no later process can be orphaned by a changed token function and there is
/// nothing to verify. Supplying a satisfiable claim there would report PASS over a check that could never
/// fail, which is worse than having none; so the contract is simply absent, and a verifier with nothing to
/// resolve is correct rather than blind.
/// </para>
/// </remarks>
public interface IDataSubjectTokenFingerprintLedger
{
	/// <summary>
	/// Records <paramref name="fingerprint"/> if this store has none, and returns the fingerprint this
	/// store holds after the operation.
	/// </summary>
	/// <param name="fingerprint">The calling process's own token-function fingerprint.</param>
	/// <param name="cancellationToken">A token to cancel the operation.</param>
	/// <returns>
	/// The fingerprint recorded for this store once the operation has completed — the value just written
	/// when none was present, or the value already present otherwise. <b>Never <see langword="null"/> and
	/// never the caller's argument by assumption:</b> a caller compares the returned value with its own and
	/// refuses when they differ, so returning the argument without reading would defeat the whole contract.
	/// </returns>
	/// <remarks>
	/// <para>
	/// <b>The test and the write are ONE indivisible operation, and a lost race is a SUCCESS.</b> Several
	/// processes may start together against a store that has no fingerprint; exactly one insert wins and
	/// every other is a no-op whose read observes the winner's value. An implementation whose
	/// insert-if-absent is not atomic in the substrate MUST catch the duplicate-key violation and continue
	/// to the read rather than surfacing it: surfacing it turns a specified no-op into a startup failure on
	/// a correctly configured deployment. It must catch only that violation, identified precisely — a
	/// blanket catch would make the read-back the only evidence and turn a transient fault into a silent
	/// pass.
	/// </para>
	/// <para>
	/// <b>Durable independently of caller transactions.</b> Successful initialization and fingerprint
	/// claims persist even if the caller's ambient transaction later rolls back. Implementations restore
	/// the caller's transaction context before returning; this boundary does not commit caller business
	/// data or change the transaction participation of ordinary store operations.
	/// </para>
	/// <para>
	/// <b>Idempotent, and safe to call on every operation.</b> Callers invoke it once per process behind
	/// their own initialisation latch; calling it again returns the same value and writes nothing.
	/// </para>
	/// <para>
	/// <b>It never overwrites and never deletes</b>, whatever the caller passes. See the write-once
	/// invariant on this interface.
	/// </para>
	/// </remarks>
	/// <exception cref="ArgumentException">
	/// <paramref name="fingerprint"/> is <see langword="null"/>, empty or whitespace. An absent fingerprint
	/// would be a value every misconfigured deployment agreed on.
	/// </exception>
	/// <exception cref="ErasureStoreNotProvisionedException">
	/// The backing table is absent and this store is not configured to provision it. A store that cannot
	/// say which token function produced its records must not be written to — the alternative is to proceed
	/// on the assumption that nothing has changed, which is the silence this ledger exists to remove.
	/// </exception>
	Task<string> ClaimTokenFingerprintAsync(string fingerprint, CancellationToken cancellationToken);
}
