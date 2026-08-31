// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch;

/// <summary>
/// Provides default values for tenant configuration.
/// </summary>
/// <remarks>
/// Single-tenant applications use <see cref="DefaultTenantId"/> automatically when no explicit
/// tenant identifier is configured. Multi-tenant applications establish the ambient tenant per
/// operation (via <c>TenantContextHolder.BeginScope</c> / tenant middleware) and read it through
/// <see cref="ITenantContext"/>.
/// </remarks>
public static class TenantDefaults
{
	/// <summary>
	/// The single canonical tenant identifier used when multi-tenancy is not configured.
	/// Single-tenant applications use this value automatically, and every tenant-scoped write
	/// and read path references this one field so all writers agree on a single value.
	/// </summary>
	/// <remarks>
	/// <para>
	/// The double-underscore shape is chosen so this value is unlikely to collide with a tenant a real
	/// deployment would name — a deployment may legitimately have a tenant called <c>"Default"</c>. The
	/// value is also deliberately non-empty and portable across relational stores: an empty string is
	/// coerced to <see langword="null"/> by some databases, which would silently widen a tenant-scoped
	/// query.
	/// </para>
	/// <para>
	/// <strong>This is a real tenant identity, not a reserved sentinel, despite sharing that shape.</strong>
	/// It is the identity a single-tenant deployment actually operates as, it is what
	/// <c>SingleTenantContext</c> returns, and it is a legal value everywhere a tenant identifier is
	/// accepted. Only one identifier in this framework is genuinely reserved and rejected on construction —
	/// the untenanted marker on <c>TenantScope</c> — and this is not it. Do not treat the two
	/// interchangeably because they look alike: rejecting this value, or substituting the untenanted marker
	/// for it, changes which rows a single-tenant deployment can see.
	/// </para>
	/// <para>
	/// <strong>Deliberately <see langword="static" /> <see langword="readonly" /> rather than a
	/// <see langword="const" />.</strong> A <see langword="const" /> is inlined into every assembly that
	/// references it, so a consumer compiled against one value keeps writing and querying that value after
	/// the framework adopts another — with no compile error and no runtime failure, only rows landing under
	/// a tenant identity the framework no longer uses. Because this value decides which rows an operation
	/// can see, that divergence is silent data misplacement rather than a cosmetic mismatch. A field is
	/// resolved at run time, so every assembly in the process necessarily agrees on one identity.
	/// </para>
	/// </remarks>
	public static readonly string DefaultTenantId = "__default__";

	// A wildcard "all tenants" identifier was removed from this type rather than repaired.
	//
	// Its documentation described a mechanism that did not exist: it claimed the value was compared against
	// stored tenant terms to decide whether an operation spans every tenant. No such comparison was ever
	// implemented, anywhere. Nothing folded it, nothing reconciled it, and no scoped read could return a row
	// carrying it — so every row written under it landed in a partition addressable by nothing, while a
	// reader of the documentation reasonably believed a wildcard comparison was protecting them.
	//
	// Estate-wide operations in this framework are reached by NAME, not by a magic tenant value: see
	// PurgeAllTenantsCompletedBeforeAsync and CleanupAllTenantsSentMessagesAsync. The method name is the
	// safety control, because it cannot be reached by accident from a value that flowed in from somewhere
	// else. That mechanism is implemented and tested; the wildcard was not.
	//
	// A host with no tenant of its own does not name one. It leaves the tenant unresolved, which the keyed
	// partition maps onto the reserved untenanted sentinel — a value with one meaning that every store
	// agrees on.
}
