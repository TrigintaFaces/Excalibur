// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Diagnostics.CodeAnalysis;

using Excalibur.Dispatch;

namespace Excalibur.Compliance;

/// <summary>
/// A declaration that one aggregate type must survive an erasure, because the law requires the data it
/// holds to be kept.
/// </summary>
/// <remarks>
/// <para>
/// <b>The case this exists for.</b> Article 17(3) withholds the right to erasure to the extent processing
/// is necessary to comply with a legal obligation. For many businesses the obligation covers the
/// customer's IDENTITY, not merely the transaction: a vehicle title transfer, a lien entry or a safety
/// recall notice is worthless without the buyer, and a warranty cannot be honoured without knowing who
/// holds it. Erasing such a record is not compliance; it is a different breach.
/// </para>
/// <para>
/// <b>The unit is the AGGREGATE TYPE, WHOLE, and that is a legal fact rather than a convenience.</b> An
/// obligation to keep a record attaches to the RECORD. A statute requiring sales records to be kept does
/// not require the buyer and permit deleting the salesperson: a partly-erased record is a MUTATED record,
/// and a mutated record has no evidentiary value — which is the entire reason it was being kept. So
/// EVERY data subject named in a retained aggregate type is covered by that record's obligation for as
/// long as it is in force, and nothing inside it is destroyed for anyone.
/// </para>
/// <para>
/// <b>An event belongs to exactly one aggregate, which is what makes the unit implementable.</b> An
/// aggregate boundary is also an event boundary, so a retention is honoured without ever having to split
/// a stored event — whose erasure is total, all fields and its type name together. A finer unit has no
/// boundary to use, and would force destroying retained data or keeping erased data with no third option.
/// </para>
/// <para>
/// <b>What that means for the other people named in the record, and why the certificate must say it.</b>
/// A data subject who appears in a retained aggregate is not erased from it. They are entitled to be told
/// that their personal data lawfully persists there and on what basis, so this declaration's basis,
/// justification and period are carried onto the erasure record for every such subject — not only for
/// whoever the obligation was written with in mind.
/// </para>
/// <para>
/// <b>What this asks of you in return.</b> The retained aggregate must carry its own copy of whatever it
/// needs. A sales record that reaches its buyer by following a reference into a customer aggregate breaks
/// when that customer is erased. This is ordinary aggregate independence and it is the modelling
/// discipline the retention depends on.
/// </para>
/// <para>
/// <b>Re-linking is yours.</b> If the subject returns and a new aggregate is created for them, matching
/// it to the retained record is a business decision — on the identifiers, the addresses or the
/// transaction data you hold. The framework does not attempt it and does not keep a hidden link, because
/// a hidden link would be exactly the re-identification the erasure was supposed to remove.
/// </para>
/// </remarks>
public sealed record ErasureRetention
{
	/// <summary>
	/// Gets the aggregate type this retention protects, as it appears in the event store.
	/// </summary>
	/// <value>The aggregate type name, matched ordinally.</value>
	public required string AggregateType { get; init; }

	/// <summary>
	/// Gets the tenant whose obligation this is.
	/// </summary>
	/// <value>
	/// The tenant identifier, matched ordinally, or <see cref="TenantScope.UntenantedSentinel"/> for a
	/// deployment that is not multi-tenant.
	/// </value>
	/// <remarks>
	/// <para>
	/// <b>Why a retention is tenanted at all.</b> A retention is a statement about a jurisdiction and a
	/// controller, and in a multi-tenant host neither is a property of the process. A tenant-blind retention
	/// makes one tenant inherit another's statute, which is over-retention — the Article 17 breach in the
	/// other direction, and the one the mandatory period exists to prevent at the type level.
	/// </para>
	/// <para>
	/// <b>A single-tenant deployment declares <see cref="TenantScope.UntenantedSentinel"/>, and this is the
	/// one detail worth reading twice.</b> The value is compared against the tenant an erasure REQUEST
	/// carries, and an untenanted request carries that sentinel — every spelling of <em>untenanted</em>
	/// (absent, empty, whitespace, or the sentinel itself) collapses onto it through
	/// <see cref="KeyedTenantPartition.FromStoredValue(string)"/>, which is this framework's single predicate
	/// for what counts as absent. Declaring the sentinel is therefore how a single-tenant declaration names
	/// the same tenant its own erasure requests do.
	/// </para>
	/// <para>
	/// It is NOT the identity an ambient tenant context reports for such a deployment, and that is not an
	/// inconsistency — the two describe different things. A context reports the identity an operation is
	/// running as; this names the tenant a legal obligation belongs to, and it is only ever compared against
	/// another erasure-domain value of the same kind. Nothing on the write path reads a tenant at all.
	/// </para>
	/// <para>
	/// <b>A mismatch erases rather than retains, and that is the chosen direction.</b> A retention declared
	/// for the wrong tenant does not match, so the record is destroyed. That is visible — the erasure
	/// record will not name the retention — whereas the opposite default would silently withhold erasure
	/// from every other tenant's data subjects.
	/// </para>
	/// </remarks>
	public required string TenantId { get; init; }

	/// <summary>
	/// Gets the lawful basis under which the data is retained.
	/// </summary>
	/// <value>The Article 17(3) ground being relied on.</value>
	public required LegalHoldBasis Basis { get; init; }

	/// <summary>
	/// Gets the written justification recorded against every erasure this retention narrows.
	/// </summary>
	/// <value>
	/// A statement naming the obligation, in terms an auditor can evaluate — the statute, the retention
	/// schedule or the regulator's requirement. Never a restatement of the basis.
	/// </value>
	/// <remarks>
	/// <b>Write it about the RECORD, not about a person.</b> The obligation attaches to the record, and
	/// this text is shown to every data subject who appears in it — including people the statute was not
	/// written with in mind. "Vehicle sales records are kept for six years under the tax code's
	/// record-keeping requirement" is a statement an auditor and any named subject can both evaluate;
	/// "the buyer's identity is required" reads as one person's obligation and is wrong for everyone else
	/// named on the same record. This cannot be checked mechanically — startup validation only refuses a
	/// blank justification and one that merely restates the basis.
	/// </remarks>
	public required string Justification { get; init; }

	/// <summary>
	/// Gets how long the obligation lasts.
	/// </summary>
	/// <value>The statutory retention period. Must be greater than zero.</value>
	/// <remarks>
	/// <para>
	/// <b>Required, and required for a reason.</b> A statutory retention ENDS. A retention with no period
	/// converts a time-bounded obligation into permanent retention, which is a breach of Article 17 in the
	/// other direction — so the period cannot be left unsaid. Being required makes an OMITTED period
	/// impossible; startup validation rejects zero and below. Neither makes an absurdly long one
	/// impossible, and nothing here pretends otherwise: a period of a thousand years is constructable and
	/// is the declaring party's statement to defend.
	/// </para>
	/// <para>
	/// <b>What the framework does and does not do with it.</b> The period is recorded and REPORTED; it is
	/// not a timer that erases anything. The instant at which a particular record's obligation lapses
	/// depends on facts the framework does not hold — the transaction date, the jurisdiction, whether the
	/// period was extended — and acting on someone else's statutory clock is not a promise we can keep. So
	/// the obligation this type creates for you is to release the retention when it expires. What we
	/// guarantee is that the period is declared, carried onto the erasure record, and visible.
	/// </para>
	/// </remarks>
	public required TimeSpan RetentionPeriod { get; init; }
}

/// <summary>
/// Answers whether an aggregate type is under a declared retention.
/// </summary>
/// <remarks>
/// Consulted by the erasure path before it destroys anything. Nothing declared means every aggregate is
/// erased exactly as it was before this existed, so the behaviour of an application that declares no
/// retention is unchanged.
/// </remarks>
public interface IErasureRetentionRegistry
{
	/// <summary>
	/// Attempts to find the retention declared for an aggregate type.
	/// </summary>
	/// <param name="tenantId">
	/// The tenant this erasure belongs to, as the erasure request recorded it. Every spelling of
	/// <em>untenanted</em> is accepted and collapsed onto <see cref="TenantScope.UntenantedSentinel"/>, so a
	/// single-tenant deployment matches its own declarations whether its requests name no tenant, an empty
	/// one, or the sentinel itself.
	/// </param>
	/// <param name="aggregateType"> The aggregate type name as the event store records it. </param>
	/// <param name="retention"> The declared retention when one exists; otherwise <see langword="null"/>. </param>
	/// <returns><see langword="true"/> when a retention is declared for the type; otherwise <see langword="false"/>.</returns>
	/// <remarks>
	/// <para>
	/// <b>Total: it throws nothing.</b> A null, empty or whitespace type is not declared, so it answers
	/// <see langword="false"/>. This is consulted on the path that decides whether to destroy a record,
	/// and an exception there would turn an unknown type into a failed erasure rather than an erased one.
	/// </para>
	/// <para>
	/// <b>Matching is ORDINAL, and the consequence of the alternative is data loss rather than a missed
	/// lookup.</b> A declaration of <c>salesrecord</c> against a store recording <c>SalesRecord</c> is not
	/// a retention, and the record is destroyed. Declare the type exactly as the event store records it.
	/// </para>
	/// <para>
	/// Consistent with <see cref="Declared"/> in both directions: every declared retention is findable by
	/// its own aggregate type, and every retention this returns is one of them.
	/// </para>
	/// </remarks>
	bool TryGetRetention(
		string? tenantId,
		string? aggregateType,
		[NotNullWhen(true)] out ErasureRetention? retention);

	/// <summary>
	/// Answers whether ANY tenant has declared a retention for an aggregate type.
	/// </summary>
	/// <param name="aggregateType">The aggregate type name as the event store records it.</param>
	/// <returns>
	/// <see langword="true"/> when at least one declaration names this aggregate type, whatever tenant it
	/// belongs to; otherwise <see langword="false"/>.
	/// </returns>
	/// <remarks>
	/// <para>
	/// <b>This is the WRITE path's question, and it is deliberately tenant-blind.</b> A write decides only
	/// whether to place a subject's fields under a separately destroyable key for the aggregate type being
	/// written into. The tenant is no part of that key's name, so it is no part of that decision — which
	/// is why nothing on the write path reads a tenant, and why none can be inferred there.
	/// </para>
	/// <para>
	/// <b>Being blind here withholds erasure from nobody, because the two decisions are independent.</b>
	/// Widening is per aggregate type; SPARING is per tenant and is decided later, through
	/// <see cref="TryGetRetention"/>. A subject belonging to a tenant that declared nothing therefore has
	/// their fields written under a widened handle and then destroyed, because no declaration matches their
	/// erasure. The only thing widening changes is WHICH key an erasure has to decide about — never
	/// whether it may decide.
	/// </para>
	/// <para>
	/// <b>Total: it throws nothing.</b> A null, empty or whitespace type is not declared, so it answers
	/// <see langword="false"/>. This runs on the path that writes personal data, and an exception here would
	/// refuse a write the framework has no reason to refuse.
	/// </para>
	/// <para>
	/// Consistent with <see cref="Declared"/>: it answers <see langword="true"/> for exactly the aggregate
	/// types named there.
	/// </para>
	/// </remarks>
	bool IsRetainedForAnyTenant(string? aggregateType);

	/// <summary>
	/// Gets every declared retention, for reporting and for startup diagnostics.
	/// </summary>
	/// <value>The declared retentions, in no particular order.</value>
	/// <remarks>
	/// At most one entry per (tenant, aggregate type). Every entry has a non-blank
	/// <see cref="ErasureRetention.AggregateType"/>, a defined <see cref="ErasureRetention.Basis"/>, a
	/// <see cref="ErasureRetention.Justification"/> that says more than the basis does, and a
	/// <see cref="ErasureRetention.RetentionPeriod"/> greater than zero — established once, at startup, so
	/// no consumer has to re-check it.
	/// </remarks>
	IReadOnlyCollection<ErasureRetention> Declared { get; }
}
