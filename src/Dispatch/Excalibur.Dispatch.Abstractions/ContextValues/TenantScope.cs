// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch;

/// <summary>
/// The tenant scope applied to a single tenant-owned data request. A scope is either
/// <see cref="None"/> — the genuine non-multi-tenant path, which emits no tenant predicate, column, or
/// parameter — or a <see cref="Scoped(string?)"/> value that carries a validated, non-empty tenant
/// identifier and always emits the tenant predicate/column.
/// </summary>
/// <remarks>
/// <para>
/// This type makes one specific unsafe state <em>unrepresentable</em>: a predicate-less query <em>while a
/// tenant is active</em> cannot be written, because the only way to obtain a scoped request is through
/// <see cref="Scoped(string?)"/>, whose precondition rejects a null or whitespace tenant. A non-multi-tenant
/// read is the distinct, explicit <see cref="None"/> construction — greppable and auditable — rather than an
/// accidental omission of a nullable string.
/// </para>
/// <para>
/// The claim is deliberately scoped to that axis, because a second one is <em>not</em> structurally enforced.
/// For a <strong>keyed</strong> store — one whose unique key includes the tenant column — the correct
/// unscoped value is <see cref="Untenanted"/> (which emits the sentinel equality term), not
/// <see cref="None"/> (which emits no term at all and so cannot satisfy a key that requires one). Nothing in
/// this type prevents an implementer from choosing <see cref="None"/> there; the guidance below says which
/// to use, but it is guidance, not a constraint. Treat that choice as a reviewed decision rather than one
/// the compiler will catch.
/// </para>
/// <para>
/// The empty-tenant predicate lives here in one place (<see cref="string.IsNullOrWhiteSpace(string?)"/>), so
/// the fail-closed guard has a single definition shared by every provider request and the tenant-scoping
/// decorator, and yields exactly one exception type (<see cref="TenantRequiredException"/>).
/// </para>
/// </remarks>
public readonly struct TenantScope : IEquatable<TenantScope>
{
	private TenantScope(string tenantId) => TenantId = tenantId;

	/// <summary>
	/// The single reserved tenant identifier: the sentinel bound for a genuinely untenanted system row inside
	/// a multi-tenant keyed store (and the anchor for a single-tenant→multi-tenant migration). A caller can
	/// never construct it through <see cref="Scoped(string?)"/>, so it can never collide with a real tenant.
	/// It is a concrete, non-null value (never <c>''</c>, which Oracle folds to <see langword="null"/>).
	/// </summary>
	/// <remarks>
	/// <para>
	/// Exposed so that schema and migration scripts can be generated from this constant instead of retyping
	/// the literal. Tenant-bound reads match this exact value, so a migration that backfills rows to a
	/// mistyped variant strands every one of them in a partition nothing queries — and does so silently:
	/// there is no compile-time signal, no constraint violation, and no runtime error, only rows that stop
	/// being returned. Reference this constant wherever the value has to appear in SQL or configuration.
	/// </para>
	/// <para>
	/// The value is <strong>case-sensitive</strong>, like every tenant term here, and contains leading and
	/// trailing double underscores. Copy it from this field rather than from rendered documentation:
	/// markdown renderers treat the surrounding underscores as emphasis and can display a form that is not
	/// the value.
	/// </para>
	/// <para>
	/// Deliberately <see langword="static" /> <see langword="readonly" /> rather than a <c>const</c>. A public
	/// constant is inlined into every consuming assembly at compile time, so a consumer that referenced it
	/// would keep the old literal until recompiled — leaving their rows in a partition this framework no
	/// longer queries, with no error to signal it. A field is read at run time, so the value stays fixable
	/// after ship and a consumer picks up the corrected one by upgrading the package alone.
	/// </para>
	/// </remarks>
	public static readonly string UntenantedSentinel = "__untenanted__";

	/// <summary>
	/// Gets the unscoped tenant scope — the non-multi-tenant default path. No tenant predicate, column, or
	/// parameter is emitted for a request built with this scope.
	/// </summary>
	/// <remarks>
	/// This is the correct untenanted scope for the <em>column-agnostic</em> store family (event store /
	/// append log), where the tenant is a filter rather than a component of identity. Keyed stores whose
	/// unique key includes the tenant column (inbox, saga, snapshot) must use <see cref="Untenanted"/>
	/// instead, so the tenant equality term is never omitted.
	/// </remarks>
	public static TenantScope None => default;

	/// <summary>
	/// Gets the reserved system-row scope for a <strong>multi-tenant</strong> keyed store — the explicit,
	/// opt-in partition for a genuinely untenanted row inside an MT deployment (for example, a system-owned
	/// record, or a row written before multi-tenancy and anchored during a non-MT→MT migration). It binds the
	/// reserved <c>__untenanted__</c> sentinel, which <see cref="Scoped(string?)"/> rejects outright, so it
	/// can never be bound as a real tenant and never collide with one.
	/// </summary>
	/// <remarks>
	/// This is <strong>not</strong> the non-multi-tenant path: a non-MT deployment has no tenant column and
	/// uses <see cref="None"/> (no term emitted). The sentinel exists only within a multi-tenant schema.
	/// </remarks>
	public static TenantScope Untenanted { get; } = new(UntenantedSentinel);

	/// <summary>
	/// Gets a value indicating whether this scope is bound to a tenant. When <see langword="true"/> the
	/// request emits the tenant predicate/column and binds <see cref="TenantId"/>; when
	/// <see langword="false"/> (the <see cref="None"/> path) it emits none of them.
	/// </summary>
	public bool IsScoped => TenantId is not null;

	/// <summary>
	/// Gets the validated tenant identifier when <see cref="IsScoped"/> is <see langword="true"/>; otherwise
	/// <see langword="null"/>.
	/// </summary>
	/// <remarks>
	/// A non-null value is not necessarily a real tenant. For the <see cref="Untenanted"/> scope this getter
	/// returns the framework's reserved untenanted marker, which is observable through this property like any
	/// other value. Callers that surface a tenant identifier to a user, log it, or compare it against tenant
	/// data supplied from outside should account for that case rather than assuming every non-null value
	/// names a real tenant. <see cref="Scoped(string?)"/> rejects the reserved marker, so it can never arrive
	/// here by that route.
	/// </remarks>
	public string? TenantId { get; }

	/// <summary>
	/// Creates a tenant-scoped scope. The tenant identifier is <strong>required</strong>: a
	/// <see langword="null"/> or whitespace value throws <see cref="TenantRequiredException"/> so a
	/// tenant-active query can never be constructed without a tenant (fail-closed by construction).
	/// </summary>
	/// <param name="tenantId">The non-empty tenant identifier to scope the request to.</param>
	/// <returns>A scoped <see cref="TenantScope"/> bound to <paramref name="tenantId"/>.</returns>
	/// <exception cref="TenantRequiredException">
	/// <paramref name="tenantId"/> is <see langword="null"/>, empty, or whitespace.
	/// </exception>
	/// <exception cref="ArgumentException">
	/// <paramref name="tenantId"/> is the reserved framework sentinel and therefore cannot name a real tenant,
	/// so an internal sentinel can never collide with a caller-supplied tenant.
	/// </exception>
	public static TenantScope Scoped(string? tenantId)
	{
		if (string.IsNullOrWhiteSpace(tenantId))
		{
			throw new TenantRequiredException();
		}

		if (string.Equals(tenantId, UntenantedSentinel, StringComparison.Ordinal))
		{
			throw new ArgumentException(
				$"The tenant identifier '{UntenantedSentinel}' is reserved for the framework and cannot name a real tenant.",
				nameof(tenantId));
		}

		return new TenantScope(tenantId);
	}

	/// <summary>
	/// The single total conversion from a raw tenant term to a scope. Every entry point on <em>this type</em>
	/// that turns a caller-supplied or store-read tenant term into a scope delegates here, so the sentinel
	/// decision is stated once per type rather than re-derived at each entry point.
	/// <see cref="KeyedTenantPartition"/> carries the sibling of this function for its own return type; the
	/// two are deliberately parallel rather than shared, because a conversion cannot return both shapes.
	/// </summary>
	/// <param name="tenantId">The tenant term as resolved from a context or read back from a store.</param>
	/// <returns>
	/// <see cref="Untenanted"/> when <paramref name="tenantId"/> is the reserved sentinel; otherwise
	/// <see cref="Scoped(string?)"/> for a real identifier.
	/// </returns>
	/// <remarks>
	/// <para>
	/// The sentinel is a <em>storage encoding</em>, not a tenant name: a store that has already converted
	/// "no tenant" into the reserved value hands that value straight back on read, so a conversion that
	/// rejects it turns a legitimately-stored row into a throw on the read path.
	/// </para>
	/// <para>
	/// This function is total for the sentinel and <strong>deliberately partial for null/whitespace</strong>.
	/// Multi-tenancy active with an unresolved tenant must keep failing closed: yielding a scope there would
	/// emit a predicate-less query, which is the cross-tenant leak this type exists to prevent. The
	/// null/whitespace rejection lives in <see cref="Scoped(string?)"/> and is not bypassed here.
	/// </para>
	/// </remarks>
	/// <exception cref="TenantRequiredException"><paramref name="tenantId"/> is null or whitespace.</exception>
	internal static TenantScope FromTenantTerm(string? tenantId)
		=> string.Equals(tenantId, UntenantedSentinel, StringComparison.Ordinal)
			? Untenanted
			: Scoped(tenantId);

	/// <summary>
	/// Derives the scope from an optional ambient tenant context: a <see langword="null"/> context is the
	/// non-multi-tenant path (<see cref="None"/>); a non-null context is converted through the single
	/// sentinel-aware conversion, which fails closed when the context resolves no tenant.
	/// </summary>
	/// <param name="tenantContext">The ambient tenant context, or <see langword="null"/> when multi-tenancy is not registered.</param>
	/// <returns>
	/// <see cref="None"/> when <paramref name="tenantContext"/> is <see langword="null"/>;
	/// <see cref="Untenanted"/> when the context resolves the reserved sentinel; otherwise
	/// <see cref="Scoped(string?)"/> for the resolved tenant.
	/// </returns>
	/// <exception cref="TenantRequiredException">
	/// <paramref name="tenantContext"/> is non-null but resolves a null/whitespace tenant (multi-tenancy
	/// active but unresolved) — the store fails closed rather than emitting a predicate-less query.
	/// </exception>
	public static TenantScope FromContext(ITenantContext? tenantContext)
		=> tenantContext is null ? None : FromTenantTerm(tenantContext.TenantId);

	/// <inheritdoc />
	public bool Equals(TenantScope other) => string.Equals(TenantId, other.TenantId, StringComparison.Ordinal);

	/// <inheritdoc />
	public override bool Equals(object? obj) => obj is TenantScope other && Equals(other);

	/// <inheritdoc />
	public override int GetHashCode() => TenantId is null ? 0 : StringComparer.Ordinal.GetHashCode(TenantId);

	/// <summary>Determines whether two scopes are equal.</summary>
	/// <param name="left">The left scope.</param>
	/// <param name="right">The right scope.</param>
	/// <returns><see langword="true"/> if the scopes carry the same tenant identifier; otherwise <see langword="false"/>.</returns>
	public static bool operator ==(TenantScope left, TenantScope right) => left.Equals(right);

	/// <summary>Determines whether two scopes are not equal.</summary>
	/// <param name="left">The left scope.</param>
	/// <param name="right">The right scope.</param>
	/// <returns><see langword="true"/> if the scopes carry different tenant identifiers; otherwise <see langword="false"/>.</returns>
	public static bool operator !=(TenantScope left, TenantScope right) => !left.Equals(right);
}
