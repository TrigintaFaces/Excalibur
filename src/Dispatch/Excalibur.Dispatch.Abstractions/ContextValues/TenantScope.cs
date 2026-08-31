// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch;

/// <summary>
/// The tenant a single tenant-owned data request is bound to. A scope is either a
/// <see cref="Scoped(string?)"/> value carrying a validated, non-empty tenant identifier, or
/// <see cref="Untenanted"/> — the reserved partition for a row that belongs to no tenant. Both bind a
/// concrete tenant term; there is no third state and no absent one.
/// </summary>
/// <remarks>
/// <para>
/// <strong>This type has no representation of "absent".</strong> <see cref="TenantId"/> is total: it is
/// never <see langword="null"/> and never empty, for every inhabitant including <see langword="default"/>.
/// A scope therefore always yields a tenant term to bind, so a statement that silently carries no tenant
/// predicate cannot be produced from one.
/// </para>
/// <para>
/// That totality is the entire point, and it is why <see langword="default"/> is defined rather than
/// merely discouraged. A value type always admits a <see langword="default"/> inhabitant, so an
/// uninitialised scope is constructible whatever the API offers; making the default fold onto
/// <see cref="Untenanted"/> means it is a legitimate, meaningful value rather than a hole that reads as
/// "no tenant term" and disappears from an emitted statement. Absence cannot be reintroduced by omission.
/// </para>
/// <para>
/// Whether a deployment applies tenant confinement <em>at all</em> is a property of the deployment, not of
/// a scope. A store that serves both single-tenant and multi-tenant hosts reads that from its own
/// configuration and decides there whether to emit a predicate; it does not infer it from a missing tenant
/// term. Keeping the two separate is what allows this type to stay total.
/// </para>
/// <para>
/// The empty-tenant guard lives here in one place (<see cref="string.IsNullOrWhiteSpace(string?)"/>), so
/// the fail-closed check has a single definition shared by every provider request and the tenant-scoping
/// decorator, and yields exactly one exception type (<see cref="TenantRequiredException"/>).
/// </para>
/// </remarks>
public readonly struct TenantScope : IEquatable<TenantScope>
{
	private readonly string? _tenantId;

	private TenantScope(string tenantId) => _tenantId = tenantId;

	/// <summary>
	/// The single reserved tenant identifier: the term bound for a genuinely untenanted row. A caller can
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
	/// Gets the reserved scope for a row that belongs to no tenant — a system-owned record, or a row
	/// written before multi-tenancy and anchored during a migration onto it. It binds the reserved
	/// <see cref="UntenantedSentinel"/> value, which <see cref="Scoped(string?)"/> rejects outright, so it
	/// can never be bound as a real tenant and never collide with one.
	/// </summary>
	/// <remarks>
	/// This is the value <see langword="default"/> folds onto, so an uninitialised scope names the
	/// untenanted partition explicitly rather than carrying no term.
	/// </remarks>
	public static TenantScope Untenanted { get; } = new(UntenantedSentinel);

	/// <summary>
	/// Gets the concrete tenant term to bind: a validated real tenant identifier for a
	/// <see cref="Scoped(string?)"/> scope, or <see cref="UntenantedSentinel"/> for
	/// <see cref="Untenanted"/> and for <see langword="default"/>. It is never <see langword="null"/> and
	/// never empty.
	/// </summary>
	/// <remarks>
	/// A non-null value is not necessarily a real tenant: for the untenanted partition this returns the
	/// framework's reserved marker, which is observable here like any other value. Callers that surface a
	/// tenant identifier to a user, log it, or compare it against tenant data supplied from outside should
	/// account for that case rather than assuming every value names a real tenant.
	/// <see cref="Scoped(string?)"/> rejects the reserved marker, so it can never arrive here by that route.
	/// </remarks>
	public string TenantId => _tenantId ?? UntenantedSentinel;

	/// <summary>
	/// Creates a scope bound to a real tenant. The tenant identifier is <strong>required</strong>: a
	/// <see langword="null"/> or whitespace value throws <see cref="TenantRequiredException"/> so a
	/// tenant-active request can never be constructed without a tenant (fail-closed by construction).
	/// </summary>
	/// <param name="tenantId">The non-empty tenant identifier to scope the request to.</param>
	/// <returns>A <see cref="TenantScope"/> bound to <paramref name="tenantId"/>.</returns>
	/// <exception cref="TenantRequiredException">
	/// <paramref name="tenantId"/> is <see langword="null"/>, empty, or whitespace.
	/// </exception>
	/// <exception cref="ArgumentException">
	/// <paramref name="tenantId"/> is the reserved framework sentinel and therefore cannot name a real tenant,
	/// so an internal sentinel can never collide with a caller-supplied tenant; or is longer than
	/// <see cref="Excalibur.Dispatch.TenantId.MaxLength"/> characters, which no shipped provider can store whole — rejected here
	/// rather than truncated later by a store, where a truncated identifier could collide with another
	/// tenant's.
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

		if (tenantId.Length > Excalibur.Dispatch.TenantId.MaxLength)
		{
			throw new ArgumentException(
				$"Tenant identifier exceeds the maximum length of {Excalibur.Dispatch.TenantId.MaxLength} characters supported by every shipped provider.",
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
	/// bind a tenant term the caller never established, which is the cross-tenant read this type exists to
	/// prevent. The null/whitespace rejection lives in <see cref="Scoped(string?)"/> and is not bypassed here.
	/// </para>
	/// </remarks>
	/// <exception cref="TenantRequiredException"><paramref name="tenantId"/> is null or whitespace.</exception>
	internal static TenantScope FromTenantTerm(string? tenantId)
		=> string.Equals(tenantId, UntenantedSentinel, StringComparison.Ordinal)
			? Untenanted
			: Scoped(tenantId);

	/// <summary>
	/// Derives the scope from an ambient tenant context, which is <strong>required</strong>: the context is
	/// converted through the single sentinel-aware conversion, which fails closed when the context resolves
	/// no tenant.
	/// </summary>
	/// <param name="tenantContext">
	/// The ambient tenant context. Required: this store partitions rows by tenant, and it resolves that
	/// partition from here, so there is no state in which the partition is undecided. A single-tenant host
	/// receives the framework default context and operates as the one canonical tenant.
	/// </param>
	/// <returns>
	/// <see cref="Untenanted"/> when the context resolves the reserved sentinel; otherwise
	/// <see cref="Scoped(string?)"/> for the resolved tenant.
	/// </returns>
	/// <remarks>
	/// There is deliberately <strong>no</strong> null-accepting form of this conversion. A conversion that
	/// accepted a missing context would have to invent a tenant term for it, silently turning
	/// <em>"multi-tenancy was never registered here"</em> into <em>"this row belongs to no tenant"</em>.
	/// Those are not synonyms: the first is a question about the deployment, which a store answers from its
	/// own configuration, and conflating them has produced both an unscoped read and a delete that could
	/// only ever match nothing.
	/// </remarks>
	/// <exception cref="ArgumentNullException"><paramref name="tenantContext"/> is <see langword="null"/>.</exception>
	/// <exception cref="TenantRequiredException">
	/// <paramref name="tenantContext"/> resolves a null/whitespace tenant (multi-tenancy active but
	/// unresolved) — the store fails closed rather than binding a tenant the caller never established.
	/// </exception>
	public static TenantScope FromContext(ITenantContext tenantContext)
	{
		ArgumentNullException.ThrowIfNull(tenantContext);
		return FromTenantTerm(tenantContext.TenantId);
	}

	/// <inheritdoc />
	public bool Equals(TenantScope other) => string.Equals(TenantId, other.TenantId, StringComparison.Ordinal);

	/// <inheritdoc />
	public override bool Equals(object? obj) => obj is TenantScope other && Equals(other);

	/// <inheritdoc />
	public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(TenantId);

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
