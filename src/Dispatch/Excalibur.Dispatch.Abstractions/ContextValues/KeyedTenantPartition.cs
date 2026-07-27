// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch;

/// <summary>
/// The tenant partition of a request against a <strong>keyed</strong> store — one whose unique key
/// includes the tenant column (inbox, saga, snapshot, and any event store that carries a tenant column).
/// A keyed partition has exactly two inhabitants, <see cref="Scoped(string?)"/> and
/// <see cref="Untenanted"/>, and <strong>always</strong> yields a concrete, non-null tenant term.
/// </summary>
/// <remarks>
/// <para>
/// This type exists so that a keyed store whose emitted statement carries <em>no</em> tenant term is
/// <strong>unconstructable</strong>. Unlike <see cref="TenantScope"/> (the column-agnostic append-log
/// family, whose <see cref="TenantScope.None"/> deliberately emits no term), a keyed partition has no
/// <c>None</c> inhabitant: the untenanted case is the explicit <see cref="Untenanted"/> sentinel, which
/// still binds a value (<c>__untenanted__</c>). A destructive or identity-bearing keyed operation can
/// therefore never resolve to an empty predicate that matches every tenant's rows.
/// </para>
/// <para>
/// It is a reference type on purpose: a value type would admit a <c>default</c> inhabitant with a
/// <see langword="null"/> tenant — reintroducing exactly the empty-term state this type removes. A
/// <see langword="null"/> reference is a distinct, nullable-reference-flagged, throwing value, never a
/// silent empty term. Every factory is total and every inhabitant binds a non-empty tenant term, so an
/// empty predicate is unconstructable: there is no public constructor and no default. A caller that
/// still resolves a <see cref="TenantScope"/> from ambient context projects it through
/// <see cref="FromScope(TenantScope)"/>, which <em>reinterprets</em> a <see cref="TenantScope.None"/>
/// scope as <see cref="Untenanted"/> rather than rejecting it.
/// </para>
/// <para>
/// The partition owns the <em>existence and value</em> of the tenant term; the request owns
/// provider-specific <em>placement</em> (for example Oracle positional bind order). The partition never
/// exposes a mutable parameter bag a caller could strip, so the term cannot be removed after binding.
/// </para>
/// </remarks>
public sealed class KeyedTenantPartition : IEquatable<KeyedTenantPartition>
{
	private KeyedTenantPartition(string tenantId) => TenantId = tenantId;

	/// <summary>
	/// The reserved partition for a genuinely untenanted row inside a keyed multi-tenant store. It binds
	/// the reserved <c>__untenanted__</c> value, which <see cref="Scoped(string?)"/>
	/// rejects, so it can never collide with a real tenant. Because it still binds a concrete value, an
	/// untenanted keyed operation emits a real equality term (never an empty predicate).
	/// </summary>
	public static KeyedTenantPartition Untenanted { get; } = new(TenantScope.UntenantedSentinel);

	/// <summary>
	/// Gets the concrete, non-null tenant term to bind: a validated real tenant identifier for a
	/// <see cref="Scoped(string?)"/> partition, or the reserved <c>__untenanted__</c> sentinel for
	/// <see cref="Untenanted"/>. It is never <see langword="null"/> and never empty.
	/// </summary>
	public string TenantId { get; }

	/// <summary>
	/// Creates a keyed partition scoped to a real tenant. The tenant identifier is required: a
	/// <see langword="null"/> or whitespace value throws, so a keyed request can never be constructed
	/// without a tenant term.
	/// </summary>
	/// <param name="tenantId">The non-empty tenant identifier.</param>
	/// <returns>A <see cref="KeyedTenantPartition"/> bound to <paramref name="tenantId"/>.</returns>
	/// <exception cref="TenantRequiredException">
	/// <paramref name="tenantId"/> is <see langword="null"/>, empty, or whitespace.
	/// </exception>
	/// <exception cref="ArgumentException">
	/// <paramref name="tenantId"/> is the reserved framework sentinel and therefore cannot name a real tenant.
	/// </exception>
	public static KeyedTenantPartition Scoped(string? tenantId)
	{
		if (string.IsNullOrWhiteSpace(tenantId))
		{
			throw new TenantRequiredException();
		}

		if (string.Equals(tenantId, TenantScope.UntenantedSentinel, StringComparison.Ordinal))
		{
			throw new ArgumentException(
				$"The tenant identifier '{TenantScope.UntenantedSentinel}' is reserved for the framework and cannot name a real tenant.",
				nameof(tenantId));
		}

		return new KeyedTenantPartition(tenantId);
	}

	/// <summary>
	/// Projects a column-agnostic <see cref="TenantScope"/> onto the keyed family: a scoped scope becomes
	/// <see cref="Scoped(string?)"/>; the <see cref="TenantScope.None"/> scope becomes
	/// <see cref="Untenanted"/> (the sentinel term), because a keyed store must never emit an empty
	/// predicate. This is the sanctioned migration bridge for callers that still resolve a
	/// <see cref="TenantScope"/> from ambient context.
	/// </summary>
	/// <param name="scope">The column-agnostic scope to project.</param>
	/// <returns>
	/// <see cref="Untenanted"/> for an unscoped scope or one already bound to the reserved sentinel;
	/// otherwise <see cref="Scoped(string?)"/> for a real identifier.
	/// </returns>
	/// <remarks>
	/// <see cref="TenantScope.Untenanted"/> reports <c>IsScoped == true</c> — it binds a concrete term — so
	/// this projection must route through the sentinel-aware conversion rather than calling
	/// <see cref="Scoped(string?)"/> directly, which rejects the sentinel outright. Without that, the one
	/// scope whose entire purpose is to name the untenanted partition is the one scope that cannot be
	/// projected onto it.
	/// </remarks>
	public static KeyedTenantPartition FromScope(TenantScope scope)
		=> scope.IsScoped ? FromTenantTerm(scope.TenantId) : Untenanted;

	/// <summary>
	/// The single total conversion from a raw tenant term to a partition. Every entry point on <em>this type</em>
	/// that turns a context-resolved or store-read tenant term into a partition delegates here, so the
	/// sentinel decision is stated once per type rather than re-derived at each entry point.
	/// <see cref="TenantScope"/> carries the sibling of this function for its own return type; the two are
	/// deliberately parallel rather than shared, because a conversion cannot return both shapes.
	/// </summary>
	/// <param name="tenantId">The tenant term as resolved from a scope, a context, or read back from a store.</param>
	/// <returns>
	/// <see cref="Untenanted"/> when <paramref name="tenantId"/> is the reserved sentinel; otherwise
	/// <see cref="Scoped(string?)"/> for a real identifier.
	/// </returns>
	/// <remarks>
	/// Total for the sentinel and <strong>deliberately partial for null/whitespace</strong>: multi-tenancy
	/// active with an unresolved tenant must keep failing closed, because yielding a partition there would
	/// bind a term the caller never established. That rejection lives in <see cref="Scoped(string?)"/> and
	/// is not bypassed here.
	/// </remarks>
	/// <exception cref="TenantRequiredException"><paramref name="tenantId"/> is null or whitespace.</exception>
	internal static KeyedTenantPartition FromTenantTerm(string? tenantId)
		=> string.Equals(tenantId, TenantScope.UntenantedSentinel, StringComparison.Ordinal)
			? Untenanted
			: Scoped(tenantId);

	/// <summary>
	/// Rehydrates a partition from a tenant value <strong>read back from a store</strong>, mapping every
	/// value that cannot name a real tenant onto <see cref="Untenanted"/>.
	/// </summary>
	/// <param name="storedValue">The tenant term as the store returned it.</param>
	/// <returns>
	/// <see cref="Untenanted"/> when <paramref name="storedValue"/> is the reserved sentinel, or is
	/// <see langword="null"/>, empty, or whitespace; otherwise a partition bound to that exact value.
	/// </returns>
	/// <remarks>
	/// <para>
	/// This is the <strong>read-back</strong> counterpart to <see cref="Scoped(string?)"/>, and the two are
	/// deliberately not interchangeable. <see cref="Scoped(string?)"/> guards <em>authoring</em> a partition
	/// from caller input, so it rejects the reserved sentinel; a value coming back off disk already passed
	/// that guard on the way in, so re-applying it would turn a legitimately-stored sentinel into a throw —
	/// aborting a whole cross-tenant pass on the first legacy row. This factory is therefore
	/// <strong>total</strong>: it has no rejecting input, and callers reading persisted tenant columns must
	/// use it rather than hand-branching on the sentinel.
	/// </para>
	/// <para>
	/// By the store's own contract, <see langword="null"/>, empty, whitespace and the sentinel are all
	/// <em>untenanted</em> — rows written before tenancy existed, or written by a non-multi-tenant host.
	/// A caller that must distinguish <em>"the store holds no tenant for this row"</em> from <em>"the query
	/// never supplied the column"</em> cannot get that distinction here, because both arrive as the same
	/// value: it must reject an absent column at the projection, before calling this.
	/// </para>
	/// </remarks>
	public static KeyedTenantPartition FromStoredValue(string? storedValue)
		=> string.IsNullOrWhiteSpace(storedValue)
			|| string.Equals(storedValue, TenantScope.UntenantedSentinel, StringComparison.Ordinal)
			? Untenanted
			: new KeyedTenantPartition(storedValue);

	/// <summary>
	/// Derives the keyed partition from an optional ambient tenant context: a <see langword="null"/>
	/// context yields <see cref="Untenanted"/> (the sentinel term — never an empty predicate); a non-null
	/// context yields <see cref="Scoped(string?)"/> for its resolved tenant (fail-closed when unresolved).
	/// </summary>
	/// <param name="tenantContext">The ambient tenant context, or <see langword="null"/> when multi-tenancy is not registered.</param>
	/// <returns>
	/// <see cref="Untenanted"/> when <paramref name="tenantContext"/> is <see langword="null"/> or resolves
	/// the reserved sentinel; otherwise <see cref="Scoped(string?)"/>.
	/// </returns>
	/// <remarks>
	/// A store that has already encoded "no tenant" as the reserved sentinel hands that value back through
	/// the ambient context, so this conversion must accept it. Routing through the single sentinel-aware
	/// conversion keeps that decision in one place — a context resolving the sentinel yields the untenanted
	/// partition instead of throwing on a read path.
	/// </remarks>
	/// <exception cref="TenantRequiredException">
	/// <paramref name="tenantContext"/> is non-null but resolves a null/whitespace tenant.
	/// </exception>
	public static KeyedTenantPartition FromContext(ITenantContext? tenantContext)
		=> tenantContext is null ? Untenanted : FromTenantTerm(tenantContext.TenantId);

	/// <summary>
	/// Gets a value indicating whether this partition names a real tenant (<see langword="true"/>) or the
	/// reserved untenanted sentinel (<see langword="false"/>). Both cases emit a tenant term; this only
	/// distinguishes a real tenant from the sentinel.
	/// </summary>
	public bool IsRealTenant => !string.Equals(TenantId, TenantScope.UntenantedSentinel, StringComparison.Ordinal);

	/// <inheritdoc />
	public bool Equals(KeyedTenantPartition? other)
		=> other is not null && string.Equals(TenantId, other.TenantId, StringComparison.Ordinal);

	/// <inheritdoc />
	public override bool Equals(object? obj) => Equals(obj as KeyedTenantPartition);

	/// <inheritdoc />
	public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(TenantId);
}
