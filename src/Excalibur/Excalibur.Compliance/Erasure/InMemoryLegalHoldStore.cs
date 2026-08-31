// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections.Concurrent;

using Excalibur.Dispatch;

using Microsoft.Extensions.Options;

namespace Excalibur.Compliance.Erasure;

/// <summary>
/// In-memory implementation of <see cref="ILegalHoldStore"/> for development and testing.
/// </summary>
/// <remarks>
/// This implementation stores all data in memory and is NOT suitable for production use.
/// Data is lost when the application restarts.
/// </remarks>
internal sealed class InMemoryLegalHoldStore : ILegalHoldStore, ILegalHoldQueryStore
{
	private readonly ConcurrentDictionary<Guid, LegalHold> _holds = new();
	private readonly ITenantContext _tenantContext;
	/// <summary>
	/// Gets the tenant scope this store runs under, resolved in one place so every statement it builds binds
	/// the same term. When the deployment is not multi-tenant the store
	/// deliberately applies no tenant filter. That decision is stated here
	/// and nowhere else: a conversion cannot make it on the store's behalf without inventing a tenant
	/// decision the host never made.
	/// </summary>
	private TenantScope CurrentTenantScope =>
		TenantScope.FromContext(_tenantContext);

	private readonly bool _requireTenant;

	/// <summary>
	/// Gets the tenant scope applied to every tenant-facing operation, for both the write and the match.
	/// </summary>
	/// <remarks>
	/// <para>
	/// This is the single place the tenant term is derived. Every tenant-facing operation in this class reads
	/// it; none compares a tenant value by hand. That is what makes the leak inexpressible: the defect was
	/// that each read <em>branched on a caller-supplied nullable</em>, so a caller who passed nothing got no
	/// filter at all and a caller who passed another tenant's identifier got that tenant's holds. With the
	/// term derived here, there is no per-call-site opportunity to omit it, and a caller-supplied identifier
	/// can only ever be <em>added</em> to it — narrowing the result, never widening it.
	/// </para>
	/// <para>
	/// Deployment mode decides the shape. A deployment that has not opted into multi-tenancy resolves
	/// no filter at all, and holds keep whatever tenant value the caller
	/// supplied — byte-identical to the single-tenant behaviour, so no stored hold becomes unreachable. Mode
	/// is "did the consumer opt in", read from <see cref="TenantContextOptions.RequireTenant"/>, and
	/// deliberately not "is an <see cref="ITenantContext"/> present" — the framework always registers a
	/// single-tenant default.
	/// </para>
	/// <para>
	/// Multi-tenancy active with no resolved tenant fails closed: it throws rather than reaching an unfiltered
	/// read. A missing context is the same failure and is stated as such, because degrading it to
	/// an unfiltered read would apply no filter at all.
	/// </para>
	/// </remarks>
	/// <exception cref="TenantRequiredException">
	/// Multi-tenancy is active but no ambient tenant is established.
	/// </exception>
	private TenantScope AmbientScope
	{
		get
		{
			if (!_requireTenant)
			{
				return TenantScope.Untenanted;
			}

			return CurrentTenantScope;
		}
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="InMemoryLegalHoldStore"/> class.
	/// </summary>
	/// <param name="tenantContext">
	/// Ambient tenant context. Under multi-tenancy every tenant-facing operation matches on the resolved
	/// tenant, and the write path stamps it rather than the value on the incoming hold, so one tenant cannot
	/// place a hold into another tenant's partition. <c>GetExpiredHoldsAsync</c> is deliberately estate-wide
	/// and documented as such at its call site. It is required: the deployment mode is selected by
	/// <paramref name="tenantContextOptions"/>, not by whether a context was supplied.
	/// </param>
	/// <param name="tenantContextOptions">
	/// The tenant-context options. Its <see cref="TenantContextOptions.RequireTenant"/> (set by
	/// <c>AddMultiTenancy()</c>) selects the deployment mode.
	/// </param>
	public InMemoryLegalHoldStore(
		ITenantContext tenantContext,
		IOptions<TenantContextOptions> tenantContextOptions)
	{
		_tenantContext = tenantContext ?? throw new ArgumentNullException(nameof(tenantContext));
		ArgumentNullException.ThrowIfNull(tenantContextOptions);
		_requireTenant = tenantContextOptions.Value.RequireTenant;
	}

	/// <summary>
	/// Gets the count of holds in the store.
	/// </summary>
	public int HoldCount => _holds.Count;

	/// <summary>
	/// Gets the count of active holds in the store.
	/// </summary>
	public int ActiveHoldCount => _holds.Values.Count(h => h.IsActive);

	/// <inheritdoc />
	public Task SaveHoldAsync(
		LegalHold hold,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(hold);

		// The ambient term is authoritative on the write. Stamping the hold's own TenantId would let one
		// tenant place a hold in another tenant's partition — or, by leaving it null, a global hold that
		// blocks every other tenant's erasures. Creating a genuinely global hold is an estate-level
		// operation, not a tenant-facing one, and is not reachable through this path once a tenant is
		// ambient. Without multi-tenancy the caller's own value is kept, but it is folded to the sentinel
		// first, so an absent tenant is stored in the one spelling the relational stores also write.
		var tenant = AmbientScope;
		var stored = hold with
		{
			TenantId = KeyedTenantPartition.FromStoredValue(
				_requireTenant ? tenant.TenantId : hold.TenantId).TenantId
		};

		if (!_holds.TryAdd(stored.HoldId, stored))
		{
			// A DEDICATED type, not the base: TenantRequiredException — raised a few lines above when
			// multi-tenancy is active and no tenant resolved — is also an InvalidOperationException. A
			// caller catching the base here reads "the hold was never written" as "the hold is already on
			// file" and drops a preservation order, whose loss is silent and does not fail safe.
			throw DuplicateLegalHoldException.ForHoldId(hold.HoldId);
		}

		return Task.CompletedTask;
	}

	/// <inheritdoc />
	public Task<LegalHold?> GetHoldAsync(
		Guid holdId,
		CancellationToken cancellationToken)
	{
		// Resolved before the lookup so that an unresolved tenant fails closed whether or not the hold exists.
		var tenant = AmbientScope;

		// A hold belonging to another tenant is reported exactly as a hold that is not there. Distinguishing
		// the two would leak the existence of another tenant's hold through the difference.
		if (!_holds.TryGetValue(holdId, out var hold) || !MatchesAmbientTenant(tenant, hold.TenantId))
		{
			return Task.FromResult<LegalHold?>(null);
		}

		return Task.FromResult<LegalHold?>(hold);
	}

	/// <inheritdoc />
	public Task<bool> UpdateHoldAsync(
		LegalHold hold,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(hold);

		// Scoped on BOTH sides: the hold must already belong to this tenant to be matched, and the value
		// written back is the ambient term, so an update can neither reach nor re-home another tenant's hold.
		// The match is STRICT OWNERSHIP, not the read rule's "or the hold is global" — a tenant must SEE a
		// global hold, because it blocks that tenant's erasures, but must never MUTATE one. Reusing the read
		// rule here would let any tenant stamp its own tenant onto an estate-wide preservation order,
		// re-homing it into one partition and silently lifting it for everyone else.
		var tenant = AmbientScope;

		if (!_holds.TryGetValue(hold.HoldId, out var existing) || !OwnedByAmbientTenant(tenant, existing.TenantId))
		{
			return Task.FromResult(false);
		}

		_holds[hold.HoldId] = hold with
		{
			TenantId = KeyedTenantPartition.FromStoredValue(
				_requireTenant ? tenant.TenantId : hold.TenantId).TenantId
		};
		return Task.FromResult(true);
	}

	/// <inheritdoc />
	public Task<IReadOnlyList<LegalHold>> GetActiveHoldsForDataSubjectAsync(
		string dataSubjectIdHash,
		string? tenantId,
		CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(dataSubjectIdHash);

		var query = _holds.Values
			.Where(h => h.IsActive && h.DataSubjectIdHash == dataSubjectIdHash);

		// This read WAS the leak: with no tenantId the query carried no tenant term at all and returned every
		// tenant's holds for that data subject. The ambient term is now applied unconditionally under
		// multi-tenancy and the caller's argument is applied on top of it, so the argument can only narrow.
		// Resolved eagerly so an unresolved tenant fails closed rather than yielding an empty result.
		var tenant = AmbientScope;
		query = query.Where(h => MatchesAmbientTenant(tenant, h.TenantId));

		if (!string.IsNullOrEmpty(tenantId))
		{
			query = query.Where(h => MatchesCallerTenant(tenantId, h.TenantId));
		}

		// Exclude expired holds
		var now = DateTimeOffset.UtcNow;
		query = query.Where(h => !h.ExpiresAt.HasValue || h.ExpiresAt.Value > now);

		return Task.FromResult<IReadOnlyList<LegalHold>>(query.ToList());
	}

	/// <inheritdoc />
	public Task<IReadOnlyList<LegalHold>> GetActiveHoldsForTenantAsync(
		string tenantId,
		CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);

		var now = DateTimeOffset.UtcNow;

		// The caller names a tenant, but the ambient term is still applied on top: asking for another tenant's
		// holds now intersects to the empty set instead of returning them.
		//
		// The caller's own term admits the holds that belong to NO tenant, for the same reason the
		// ambient one does. This surface answers "which active holds are in force for this tenant",
		// and its caller is the erasure gate: a global preservation order is in force for every
		// tenant, and it carries no data subject, so the subject-scoped query cannot return it either.
		// A bare equality here therefore left a scoped erasure check seeing no global hold at all, and
		// the deletion it should have blocked is irreversible.
		var tenant = AmbientScope;

		var holds = _holds.Values
			.Where(h => h.IsActive &&
						MatchesCallerTenant(tenantId, h.TenantId) &&
						(!h.ExpiresAt.HasValue || h.ExpiresAt.Value > now))
			.Where(h => MatchesAmbientTenant(tenant, h.TenantId))
			.ToList();

		return Task.FromResult<IReadOnlyList<LegalHold>>(holds);
	}

	/// <inheritdoc />
	public Task<IReadOnlyList<LegalHold>> ListActiveHoldsAsync(
		string? tenantId,
		CancellationToken cancellationToken)
	{
		var now = DateTimeOffset.UtcNow;

		var query = _holds.Values
			.Where(h => h.IsActive && (!h.ExpiresAt.HasValue || h.ExpiresAt.Value > now));

		// Ambient term first and unconditionally under multi-tenancy; the caller's argument narrows it.
		// Resolved eagerly so an unresolved tenant fails closed rather than yielding an empty result.
		var tenant = AmbientScope;
		query = query.Where(h => MatchesAmbientTenant(tenant, h.TenantId));

		if (!string.IsNullOrEmpty(tenantId))
		{
			query = query.Where(h => MatchesCallerTenant(tenantId, h.TenantId));
		}

		var holds = query.OrderByDescending(h => h.CreatedAt).ToList();

		return Task.FromResult<IReadOnlyList<LegalHold>>(holds);
	}

	/// <inheritdoc />
	public Task<IReadOnlyList<LegalHold>> ListAllHoldsAsync(
		string? tenantId,
		DateTimeOffset? fromDate,
		DateTimeOffset? toDate,
		CancellationToken cancellationToken)
	{
		var query = _holds.Values.AsEnumerable();

		// Ambient term first and unconditionally under multi-tenancy; the caller's argument narrows it.
		// Resolved eagerly so an unresolved tenant fails closed rather than yielding an empty result.
		var tenant = AmbientScope;
		query = query.Where(h => MatchesAmbientTenant(tenant, h.TenantId));

		if (!string.IsNullOrEmpty(tenantId))
		{
			query = query.Where(h => MatchesCallerTenant(tenantId, h.TenantId));
		}

		if (fromDate.HasValue)
		{
			query = query.Where(h => h.CreatedAt >= fromDate.Value);
		}

		if (toDate.HasValue)
		{
			query = query.Where(h => h.CreatedAt <= toDate.Value);
		}

		var holds = query.OrderByDescending(h => h.CreatedAt).ToList();

		return Task.FromResult<IReadOnlyList<LegalHold>>(holds);
	}

	/// <inheritdoc />
	/// <remarks>
	/// ESTATE-WIDE BY DESIGN. The hold-expiry sweep runs from a background service with no ambient tenant and
	/// must retire every tenant's lapsed holds in one pass; scoping it would leave every tenant but one under
	/// holds that should have expired, blocking their erasures indefinitely. Each hold carries its own
	/// tenant, and the surface is reachable only through <see cref="ILegalHoldQueryStore"/>.
	/// </remarks>
	public Task<IReadOnlyList<LegalHold>> GetExpiredHoldsAsync(
		CancellationToken cancellationToken)
	{
		var now = DateTimeOffset.UtcNow;

		var expiredHolds = _holds.Values
			.Where(h => h.IsActive && h.ExpiresAt.HasValue && h.ExpiresAt.Value <= now)
			.ToList();

		return Task.FromResult<IReadOnlyList<LegalHold>>(expiredHolds);
	}

	/// <inheritdoc />
	public object? GetService(Type serviceType)
	{
		ArgumentNullException.ThrowIfNull(serviceType);

		if (serviceType == typeof(ILegalHoldQueryStore))
		{
			return this;
		}

		return null;
	}

	/// <summary>
	/// Clears all holds from the store.
	/// </summary>
	public void Clear()
	{
		_holds.Clear();
	}

	/// <summary>
	/// Whether a hold is OWNED by the ambient tenant — strict, unlike the read rule, which also admits a
	/// global hold.
	/// </summary>
	/// <remarks>
	/// Mutations use this and reads use <see cref="MatchesAmbientTenant"/>, and the difference is
	/// load-bearing. A tenant must see a global hold because it blocks that tenant's erasures; a tenant must
	/// not mutate one, or it could stamp its own tenant onto an estate-wide preservation order and lift it
	/// for every other tenant, whose next erasure would then run and report success.
	/// </remarks>
	private bool OwnedByAmbientTenant(TenantScope tenant, string? rowTenantId) =>
		!_requireTenant || string.Equals(rowTenantId, tenant.TenantId, StringComparison.Ordinal);

	/// <summary>
	/// Decides whether a stored hold's tenant satisfies the ambient tenant term.
	/// </summary>
	/// <param name="tenant">The scope resolved once at the start of the operation.</param>
	/// <param name="rowTenantId">The tenant value stored on the hold.</param>
	/// <returns>
	/// <see langword="true"/> when multi-tenancy is not active, when the hold is untenanted — carrying either
	/// the reserved sentinel or no tenant at all — or when the hold belongs to the ambient tenant; otherwise
	/// <see langword="false"/>.
	/// </returns>
	/// <remarks>
	/// <para>
	/// The single comparison site for this store: every tenant-facing operation routes through it rather than
	/// comparing a tenant value inline, so the match cannot be omitted at one call site and applied at
	/// another. The comparison is ordinal because a tenant identifier is case-sensitive throughout the
	/// framework — matching case-insensitively here would let two distinct tenants read each other's holds.
	/// </para>
	/// <para>
	/// An untenanted hold is a <em>global</em> hold that blocks erasure for every tenant, so the term is
	/// <c>tenant matches OR the hold is untenanted</c> rather than a bare equality. A bare equality would drop
	/// global holds from a tenant's view, and a legal hold is a control that <em>blocks</em> erasure — losing
	/// one does not fail safe, it erases data a court order says to keep. It still excludes every other
	/// tenant's holds, which is the isolation this exists to provide.
	/// </para>
	/// <para>
	/// "Untenanted" has TWO spellings and this term must admit both, because the two exist for different
	/// reasons and neither is going away. <see cref="TenantScope.UntenantedSentinel"/> is what a global hold
	/// carries once the tenant column is total — both SQL providers store it and match it, and it is what a
	/// hold holds after any round trip through one of them. <see langword="null"/> is the pre-migration
	/// spelling, retained as transition tolerance for a database whose column has not yet been closed.
	/// </para>
	/// <para>
	/// Admitting only <see langword="null"/> was a real divergence rather than a tidiness problem: a hold
	/// carrying the sentinel would have been read as belonging to a tenant <em>literally named</em> by that
	/// reserved string, so the same global hold was visible to every tenant on SQL and to none here. The
	/// direction of that failure is the dangerous one — a global hold that disappears does not block an
	/// erasure it was filed to prevent. The sibling in-memory data-inventory store already matched the
	/// sentinel; this store was the one place in the family that did not.
	/// </para>
	/// <para>
	/// The scope is taken as a parameter rather than read here, because a caller that read it lazily — only
	/// once it had a hold in hand — would make failing closed depend on whether the store happened to hold
	/// data: an unresolved tenant would throw against a populated store and quietly return "not found"
	/// against an empty one. Each operation resolves the scope up front and passes it in, so the fail-closed
	/// throw is a property of the deployment rather than of the data.
	/// </para>
	/// </remarks>
	private bool MatchesAmbientTenant(TenantScope tenant, string? rowTenantId) =>
		!_requireTenant
		|| IsUntenanted(rowTenantId)
		|| string.Equals(rowTenantId, tenant.TenantId, StringComparison.Ordinal);

	/// <summary>
	/// Decides whether a stored hold's tenant satisfies the tenant a CALLER named, as distinct from the
	/// ambient one.
	/// </summary>
	/// <param name="tenantId">The tenant identifier the caller passed to the query.</param>
	/// <param name="rowTenantId">The tenant value stored on the hold.</param>
	/// <returns>
	/// <see langword="true"/> when the hold belongs to the named tenant or to no tenant at all; otherwise
	/// <see langword="false"/>.
	/// </returns>
	/// <remarks>
	/// <para>
	/// This is deliberately the same disjunction as <see cref="MatchesAmbientTenant"/>, and it was
	/// previously a bare equality — which made each of these methods contradict itself. The ambient term
	/// admitted a global hold and the caller's term, ten lines later in the same method, discarded it. The
	/// consequence ran the wrong way on a blocking control: a caller who named their own tenant received a
	/// result with every global preservation order removed, and erasure — which is irreversible — then
	/// proceeded against data a court order says to keep.
	/// </para>
	/// <para>
	/// The two failure directions are not comparable, which is why the term widens rather than the ambient
	/// one narrowing. Missing a hold destroys preserved data permanently; seeing an extra one delays a
	/// deletion until someone releases the hold or re-scopes the query.
	/// </para>
	/// <para>
	/// It still excludes every OTHER tenant's holds, so the caller's argument narrows the ambient set
	/// exactly as before. Mutations keep the strict form in <see cref="OwnedByAmbientTenant"/>: a tenant
	/// must see a global hold and must never rewrite one.
	/// </para>
	/// </remarks>
	private static bool MatchesCallerTenant(string tenantId, string? rowTenantId) =>
		IsUntenanted(rowTenantId)
		|| string.Equals(rowTenantId, tenantId, StringComparison.Ordinal);

	/// <summary>
	/// Whether a stored tenant value means "this row belongs to no tenant", in either spelling.
	/// </summary>
	/// <param name="rowTenantId">The tenant value stored on the row.</param>
	/// <returns><see langword="true"/> when the value is the reserved sentinel or <see langword="null"/>.</returns>
	/// <remarks>
	/// Stated once so the two spellings cannot drift apart at separate call sites. This mirrors the SQL
	/// providers' shared tenant clause, which admits the sentinel and NULL for the same reasons; a copy of
	/// that predicate left on one spelling does not fail loudly, it silently stops matching global holds on
	/// whichever read path it governs.
	/// </remarks>
	private static bool IsUntenanted(string? rowTenantId) =>
		rowTenantId is null
		|| string.Equals(rowTenantId, TenantScope.UntenantedSentinel, StringComparison.Ordinal);
}
