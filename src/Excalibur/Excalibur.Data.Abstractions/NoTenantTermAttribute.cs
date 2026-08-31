// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Data;

/// <summary>
/// The argument that makes it correct for a relational data request to carry no tenant term.
/// </summary>
/// <remarks>
/// <para>
/// A statement without a tenant term is correct for several unrelated reasons, and they are not
/// interchangeable. Recording only that a request is "not tenant-scoped" produces an index in which an
/// operator report and a primary-key update are the same entry, so a reviewer auditing estate-wide reach
/// has to re-read every statement to find the ones that actually have it. The kind is what makes the
/// index answerable.
/// </para>
/// </remarks>
public enum TenantConfinement
{
	/// <summary>
	/// The statement addresses the whole estate on purpose.
	/// </summary>
	/// <remarks>
	/// The operation takes no tenant and its result carries no tenant field, so a confined answer could not
	/// tell its caller which partition it described. Operator statistics, health reads, retention purges and
	/// the cross-tenant delivery drain are all of this kind. Adding a tenant term here does not harden the
	/// statement; it makes it report or act on the wrong population.
	/// </remarks>
	EstateWide,

	/// <summary>
	/// The statement is addressed by a globally unique key the caller already holds.
	/// </summary>
	/// <remarks>
	/// <para>
	/// <c>WHERE Id = @X</c> selects at most one row. <c>WHERE Id = @X AND TenantId = @T</c> selects a subset
	/// of at most one row, which is that row or nothing — so the tenant term cannot cause a different row to
	/// be addressed, because there is no other row available to address. Its only reachable effect is to turn
	/// the correct row into zero rows.
	/// </para>
	/// <para>
	/// This holds regardless of the identifier's entropy: the exposure comes from the operation accepting a
	/// caller-supplied identifier at all, not from the missing tenant term. On a read the trade is real
	/// rather than free — a tenant term would have made the result empty for a caller scoped to a different
	/// tenant than the identifier it supplied — so a read using this kind states that trade in its
	/// justification and on its public surface.
	/// </para>
	/// </remarks>
	IdentityAddressed,

	/// <summary>
	/// The statement is reached through a foreign key to a row that is itself confined.
	/// </summary>
	/// <remarks>
	/// Every row the statement can match belongs to the one parent the caller named, so no other tenant's row
	/// is reachable even when the filtered columns are not themselves a key. This is a different argument
	/// from <see cref="IdentityAddressed"/> and does not follow from it: it bounds which rows exist, not how
	/// many.
	/// </remarks>
	ForeignKeyConfined,

	/// <summary>
	/// The table declares no tenant column, so there is no term to bind.
	/// </summary>
	/// <remarks>
	/// A statement about the schema as it exists, not a judgement that tenant-owned work never flows through
	/// the table. If the table later gains a tenant column this justification expires, which is why the kind
	/// is recorded separately from the others rather than folded into them.
	/// </remarks>
	NoTenantDimension,
}

/// <summary>
/// Declares that a relational data request binds no tenant term, and states the argument that makes that
/// correct.
/// </summary>
/// <remarks>
/// <para>
/// <strong>This is documentation, not enforcement, and the distinction is the point.</strong> Nothing reads
/// this attribute at run time and nothing derives behaviour from it; the tenant term is applied where the
/// request builds its parameters, and that is the only thing that decides what the query does. What the
/// attribute delivers is a searchable, per-kind index of the statements that deliberately carry no tenant
/// term, so a reviewer can find them without re-reading every request's SQL.
/// </para>
/// <para>
/// <strong>There is deliberately no marker for the ordinary tenant-confined case.</strong> A request that
/// binds a tenant term already says so, in the parameter binding, in the one place that determines the
/// behaviour. A second declaration restating it could only ever agree with it or lie about it, and a
/// declaration that can lie is worse than no declaration at all. The shape is the framework's own
/// authorization convention: no attribute for the default, an explicit attribute only for the exception.
/// </para>
/// <para>
/// The justification is required and must be a non-empty compile-time constant. A statement may span every
/// tenant, or decline a tenant term for one of the other reasons, but it may not do so silently — an
/// unexplained omission is indistinguishable from an oversight, which is the confusion this attribute
/// exists to remove.
/// </para>
/// <para>
/// <strong>Scope.</strong> This covers the relational surface routed through
/// <see cref="IDataRequest{TConnection, TModel}"/>. Document and key-value stores compose the tenant into a
/// document identifier and do not pass through this seam, so it does not speak for them.
/// </para>
/// </remarks>
/// <example>
/// <code language="csharp">
/// [NoTenantTerm(
///     TenantConfinement.IdentityAddressed,
///     "The outbox Id is the table's primary key, so this statement already addresses at most one row.")]
/// public sealed class MarkMessageSentRequest : DataRequestBase&lt;IDbConnection, int&gt;
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
public sealed class NoTenantTermAttribute : Attribute
{
	/// <summary>
	/// Initializes a new instance of the <see cref="NoTenantTermAttribute"/> class.
	/// </summary>
	/// <param name="confinement">The argument that makes the absent tenant term correct.</param>
	/// <param name="justification">
	/// Why this argument applies to this statement, stated in terms of the behaviour and the real key —
	/// for example <c>"the outbox Id is the table's primary key, so a tenant term could only subtract"</c>.
	/// </param>
	public NoTenantTermAttribute(TenantConfinement confinement, string justification)
	{
		Confinement = confinement;
		Justification = justification;
	}

	/// <summary>Gets the argument that makes the absent tenant term correct.</summary>
	/// <value>One of the <see cref="TenantConfinement"/> kinds.</value>
	public TenantConfinement Confinement { get; }

	/// <summary>Gets the stated reason this argument applies to this statement.</summary>
	/// <value>The justification supplied at the declaration site.</value>
	public string Justification { get; }
}
