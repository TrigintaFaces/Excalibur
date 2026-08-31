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
/// <strong>unconstructable</strong>. It has no absent inhabitant: the untenanted case is the explicit
/// <see cref="Untenanted"/> sentinel, which still binds a value (<c>__untenanted__</c>). A destructive or
/// identity-bearing keyed operation can therefore never resolve to an empty predicate that matches every
/// tenant's rows.
/// </para>
/// <para>
/// It is a reference type on purpose: it has no public constructor and no default, so a
/// <see langword="null"/> reference is a distinct, nullable-reference-flagged, throwing value rather than
/// a silent empty term. Every factory is total and every inhabitant binds a non-empty tenant term, so an
/// empty predicate is unconstructable. <see cref="TenantScope"/> reaches the same totality by a different
/// route — its tenant term folds onto the sentinel for <see langword="default"/> — so a caller that
/// resolves a scope from ambient context projects it through <see cref="FromScope(TenantScope)"/>.
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
	/// <paramref name="tenantId"/> is the reserved framework sentinel and therefore cannot name a real tenant;
	/// or is longer than <see cref="Excalibur.Dispatch.TenantId.MaxLength"/> characters, which no shipped provider can store
	/// whole — rejected here rather than truncated later by a store, where a truncated identifier could
	/// collide with another tenant's.
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

		if (tenantId.Length > Excalibur.Dispatch.TenantId.MaxLength)
		{
			throw new ArgumentException(
				$"Tenant identifier exceeds the maximum length of {Excalibur.Dispatch.TenantId.MaxLength} characters supported by every shipped provider.",
				nameof(tenantId));
		}

		return new KeyedTenantPartition(tenantId);
	}

	/// <summary>
	/// Projects a column-agnostic <see cref="TenantScope"/> onto the keyed family: a scope naming a real
	/// tenant becomes <see cref="Scoped(string?)"/>; a scope bound to the reserved sentinel becomes
	/// <see cref="Untenanted"/>. This is the bridge for callers that resolve a <see cref="TenantScope"/>
	/// from ambient context and need a keyed partition.
	/// </summary>
	/// <param name="scope">The column-agnostic scope to project.</param>
	/// <returns>
	/// <see cref="Untenanted"/> for a scope bound to the reserved sentinel; otherwise
	/// <see cref="Scoped(string?)"/> for a real identifier.
	/// </returns>
	/// <remarks>
	/// This projection routes through the sentinel-aware conversion rather than calling
	/// <see cref="Scoped(string?)"/> directly, which rejects the sentinel outright. Without that, the one
	/// scope whose entire purpose is to name the untenanted partition is the one scope that cannot be
	/// projected onto it. Because <see cref="TenantScope.TenantId"/> is total, this conversion is total.
	/// </remarks>
	public static KeyedTenantPartition FromScope(TenantScope scope)
		=> FromTenantTerm(scope.TenantId);

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
	/// Maps a stored tenant term back to the identifier a caller supplied, with every spelling of
	/// <em>untenanted</em> collapsing to <see langword="null"/>.
	/// </summary>
	/// <param name="storedValue">The tenant term as persisted.</param>
	/// <returns>
	/// The originating tenant identifier, or <see langword="null"/> when the row belongs to no tenant.
	/// </returns>
	/// <remarks>
	/// <para>
	/// This is the inverse of <see cref="FromStoredValue"/> and is deliberately written in terms of it, so
	/// there is exactly one predicate in the codebase deciding what counts as absent. Two hand-rolled
	/// copies of that decision is how the two spellings drift apart: one integrity tag computed over
	/// <see langword="null"/> while the sentinel was stored made every untouched audit trail verify as
	/// tampered.
	/// </para>
	/// <para>
	/// Use it wherever a stored term must be turned back into the value a caller signed, compared, or
	/// re-entered as a scope — never a local <c>== sentinel</c> comparison, which silently disagrees with
	/// this one about empty and whitespace.
	/// </para>
	/// </remarks>
	public static string? ToSignedTenantId(string? storedValue)
		=> ReferenceEquals(FromStoredValue(storedValue), Untenanted) ? null : storedValue;

	/// <summary>
	/// Derives the keyed partition from an ambient tenant context, which is <strong>required</strong>: the
	/// resolved tenant yields <see cref="Scoped(string?)"/>, and a context resolving the reserved sentinel
	/// yields <see cref="Untenanted"/> (the sentinel term — never an empty predicate).
	/// </summary>
	/// <param name="tenantContext">
	/// The ambient tenant context. Required: this store partitions rows by tenant, and it resolves that
	/// partition from here, so there is no state in which the partition is undecided. A single-tenant host
	/// receives the framework default context and operates as the one canonical tenant.
	/// </param>
	/// <returns>
	/// <see cref="Untenanted"/> when <paramref name="tenantContext"/> resolves the reserved sentinel;
	/// otherwise <see cref="Scoped(string?)"/>.
	/// </returns>
	/// <remarks>
	/// <para>
	/// There is deliberately <strong>no</strong> null-accepting form of this conversion. A caller holding an
	/// optional context — because multi-tenancy may not be registered in that deployment — must state at its
	/// own call site what a missing context means for that store. It is not a decision this type can make on
	/// the caller's behalf, and a conversion that made it silently is how a store whose context was never
	/// wired came to report a well-formed untenanted partition: <em>"no context was supplied"</em> quietly
	/// acquired the meaning <em>"this row has no tenant"</em>. Requiring the argument turns that substitution
	/// into a compile error instead of a plausible wrong answer at run time.
	/// </para>
	/// <para>
	/// A store that has already encoded "no tenant" as the reserved sentinel hands that value back through
	/// the ambient context, so this conversion must accept it. Routing through the single sentinel-aware
	/// conversion keeps that decision in one place — a context resolving the sentinel yields the untenanted
	/// partition instead of throwing on a read path.
	/// </para>
	/// </remarks>
	/// <exception cref="ArgumentNullException"><paramref name="tenantContext"/> is <see langword="null"/>.</exception>
	/// <exception cref="TenantRequiredException">
	/// <paramref name="tenantContext"/> resolves a null/whitespace tenant.
	/// </exception>
	public static KeyedTenantPartition FromContext(ITenantContext tenantContext)
	{
		ArgumentNullException.ThrowIfNull(tenantContext);
		return FromTenantTerm(tenantContext.TenantId);
	}

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
