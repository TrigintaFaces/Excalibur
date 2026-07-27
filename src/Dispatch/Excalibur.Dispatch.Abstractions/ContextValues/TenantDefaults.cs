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
	/// and read path references this one constant so all writers agree on a single value.
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
	/// </remarks>
	public const string DefaultTenantId = "__default__";

	/// <summary>
	/// A wildcard tenant identifier indicating all tenants.
	/// Used by infrastructure services (e.g., job hosts, background processors)
	/// that operate across all tenants rather than within a single tenant scope.
	/// </summary>
	public const string AllTenants = "*";
}
