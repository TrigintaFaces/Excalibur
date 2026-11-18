// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

namespace Excalibur.Compliance.Erasure;

/// <summary>
/// Pseudonymizes data-subject identifiers (the lookup key for GDPR erasure, legal-hold, and data-inventory
/// records) into a stable, non-reversible token.
/// </summary>
/// <remarks>
/// <para>
/// All GDPR erasure, legal-hold, and data-inventory components MUST use the same registered hasher so a
/// given identifier maps to the same token across services and stores. Implementations use a keyed
/// construction (a secret pepper held outside the record store) so the token is not reversible by
/// rainbow-table / dictionary attack against low-entropy identifiers such as e-mail addresses.
/// </para>
/// <para>
/// <b>CONSUMER OBLIGATION — THE TOKEN IS A PERMANENT IDENTITY, NOT A DERIVED VALUE, AND ROTATING ITS KEY
/// IS NOT A SAFE OPERATION.</b> Every durable compliance record is keyed by this token: erasure requests,
/// the key destruction ledger, and the ledger of minted key handles that an erasure enumerates the
/// subject's keys from. So changing what this method returns for an identifier does not re-key those
/// records — it makes them unreachable, all at once, and nothing reports it.
/// </para>
/// <para>
/// <b>What follows, concretely.</b> The enumeration of a subject's key handles returns empty, so an erasure
/// destroys only the handles it can derive from declared retentions and then signs a completion certificate
/// over everything it missed. Existing erasure requests and destruction records become unfindable by the
/// subject's new token. And every key handle derived for encryption changes, so personal data already
/// encrypted is no longer decryptable. None of that throws and none of it is logged.
/// </para>
/// <para>
/// <b>So two things are required of an implementation, and of the deployment that configures one.</b>
/// First, it MUST return the same token for the same identifier <b>for the lifetime of the DATA, not the
/// lifetime of the process</b>: a token that changes with reloadable configuration silently orphans every
/// durable record keyed by the old one, and this interface is the seam where that invariant has to hold,
/// because a reload is invisible to every caller. Second, the keying material MUST be treated as permanent
/// for the life of the data it protects.
/// Ordinary secret-rotation hygiene applied to it destroys the deployment's own erasure audit trail. If it
/// must change — a suspected compromise, say — the records keyed under the old token have to be migrated
/// before the change, because afterwards the old token cannot be recomputed from anything that remains.
/// </para>
/// <para>
/// <b>THE SECOND REQUIREMENT IS NOW ENFORCED, NOT MERELY DOCUMENTED.</b> The durable erasure store
/// records a non-reversible fingerprint of the token function the first time it is used, write-once, and
/// <b>refuses to start</b> when a later process computes a different one. Because a process has one token
/// function, that refusal stops the whole process — so a deployment whose compliance stores share one
/// database is covered by it. A host that registers the legal-hold or data-inventory store with no erasure
/// store performs no such check; those stores do not yet carry the record. So a deployment
/// whose hashing key changed fails loudly, naming the setting to restore, instead of proceeding to
/// enumerate nothing, destroy nothing, and sign a completion certificate over what it missed. The
/// fingerprint is taken from this interface's own OUTPUT over a fixed canary identifier — not from
/// configuration — so it detects a replaced <i>implementation</i> exactly as it detects a rotated key.
/// </para>
/// <para>
/// <b>Two limits on that protection, stated because a consumer who thinks it is total would be misled.</b>
/// It covers RECORDS, not key NAMES: a per-subject encryption key is named by a handle in a key vault,
/// which has nowhere to keep a fingerprint beside it, so personal data already encrypted under a handle
/// derived from the old token function stays undecryptable and no check will report that. And it covers
/// stores that outlive their process; a purely in-memory store has nothing to orphan and therefore
/// records nothing.
/// </para>
/// <para>
/// <b>CORRECTION TO THE OBLIGATION ABOVE, and the superseded wording is quoted because an implementation
/// written against it is conforming and broken.</b> The first requirement previously read "it MUST return
/// the same token for the same identifier for the lifetime of the process". That is too weak to be the
/// obligation it was standing in for. A pepper read through a reloadable options monitor satisfies it
/// exactly — a single process can legitimately observe a reload — and the durable fingerprint check cannot
/// see that, because it runs once at startup. The consequence is the whole set described above, arriving
/// mid-process with nothing to report it. The obligation is the LIFETIME OF THE DATA.
/// </para>
/// <para>
/// <b>REQUIRED OF THE RETURNED TOKEN'S SHAPE, because a per-subject key handle is composed from it rather
/// than merely stored.</b> The token MUST be exactly 64 characters drawn from <c>0-9A-F</c>: a fixed-width
/// uppercase hexadecimal digest. This is not a formatting preference and a consumer cannot be shown correct
/// without the reason. A per-subject encryption key is named by a handle derived from this token, and the
/// handle for a RETAINED aggregate type is composed by appending a separator and a discriminator to that
/// name. Fixed width, a single case and an alphabet that excludes the separator are the only things
/// keeping the plain and retained namespaces disjoint. A token that varies in length, uses lower case, or
/// can contain a separator lets one data subject's handle equal another's retained handle — so erasing the
/// first destroys the key the second's declared retention exists to preserve. Key-vault object names are
/// additionally case-insensitive, so two tokens differing only in case name ONE key.
/// </para>
/// <para>
/// <b>REQUIRED OF THE MAPPING: it MUST be injective within a deployment.</b> Two different identifiers must
/// never produce one token. Every durable compliance record and every per-subject key is addressed by it,
/// so a collision merges two data subjects: one subject's erasure destroys the other's key, and a legal
/// hold placed on one blocks the other. A keyed cryptographic digest gives this; truncating one, or
/// returning the identifier itself, does not.
/// </para>
/// <para>
/// <b>These three are now ENFORCED at the point a key handle is derived, not merely documented.</b> The
/// derivation refuses a token that is not exactly 64 uppercase hexadecimal characters and names the
/// registered implementation type in the failure. Injectivity cannot be checked by inspecting one token
/// and remains the implementation's obligation.
/// </para>
/// <para>
/// <b>CONSUMER OBLIGATION — THE FULL SET OF PERMANENT DERIVATION PARAMETERS IS WIDER THAN THE PEPPER.</b>
/// A per-subject key handle is derived from the pepper, the hasher implementation, AND the exact spelling
/// and case of the tenant identifier. All three are permanent for the life of the data, for the same
/// reason and with the same silence. A deployment that re-cases, normalises, merges or migrates a tenant
/// identifier — <c>"acme"</c> to <c>"acme-eu"</c>, or a change of letter case — permanently orphans every
/// key handle for that tenant: the data stays encrypted and no handle derived afterwards names the key
/// that protects it. The startup fingerprint described above covers the token function and does NOT cover
/// the tenant term, so nothing reports this one. Migrate the affected records and re-encrypt before
/// changing a tenant identifier, never afterwards.
/// </para>
/// </remarks>
public interface IDataSubjectHasher
{
	/// <summary>
	/// Computes the stable pseudonymization token for a data-subject identifier.
	/// </summary>
	/// <param name="dataSubjectId">The raw data-subject identifier (e-mail, username, national ID, …).</param>
	/// <returns>
	/// A keyed hash of the identifier, as exactly 64 characters drawn from <c>0-9A-F</c>. The width, the
	/// single case and the injectivity of the mapping are all required rather than conventional — see the
	/// obligations on this interface for what each one prevents.
	/// </returns>
	string HashDataSubjectId(string dataSubjectId);
}
