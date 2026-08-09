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
	private readonly ITenantContext? _tenantContext;
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
	/// <see cref="TenantScope.None"/>: no filter is applied, and holds keep whatever tenant value the caller
	/// supplied — byte-identical to the single-tenant behaviour, so no stored hold becomes unreachable. Mode
	/// is "did the consumer opt in", read from <see cref="TenantContextOptions.RequireTenant"/>, and
	/// deliberately not "is an <see cref="ITenantContext"/> present" — the framework always registers a
	/// single-tenant default.
	/// </para>
	/// <para>
	/// Multi-tenancy active with no resolved tenant fails closed: it throws rather than reaching an unfiltered
	/// read. A missing context is the same failure and is stated as such, because degrading it to
	/// <see cref="TenantScope.None"/> would apply no filter at all.
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
				return TenantScope.None;
			}

			return _tenantContext is null
				? throw new TenantRequiredException()
				: TenantScope.FromContext(_tenantContext);
		}
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="InMemoryLegalHoldStore"/> class.
	/// </summary>
	/// <param name="tenantContext">
	/// Ambient tenant context. Under multi-tenancy every tenant-facing operation matches on the resolved
	/// tenant, and the write path stamps it rather than the value on the incoming hold, so one tenant cannot
	/// place a hold into another tenant's partition. <c>GetExpiredHoldsAsync</c> is deliberately estate-wide
	/// and documented as such at its call site. Omitting it — the default — is the single-tenant deployment
	/// shape, in which the store resolves <see cref="TenantScope.None"/> and applies no filter.
	/// </param>
	/// <param name="tenantContextOptions">
	/// The tenant-context options. Its <see cref="TenantContextOptions.RequireTenant"/> (set by
	/// <c>AddMultiTenancy()</c>) selects the deployment mode.
	/// </param>
	public InMemoryLegalHoldStore(
		ITenantContext? tenantContext = null,
		IOptions<TenantContextOptions>? tenantContextOptions = null)
	{
		_tenantContext = tenantContext;
		_requireTenant = tenantContextOptions?.Value.RequireTenant ?? false;
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
		// ambient. Without multi-tenancy the caller's own instance is stored untouched.
		var tenant = AmbientScope;
		var stored = tenant.IsScoped ? hold with { TenantId = tenant.TenantId } : hold;

		if (!_holds.TryAdd(stored.HoldId, stored))
		{
			throw new InvalidOperationException($"Legal hold {hold.HoldId} already exists");
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

		_holds[hold.HoldId] = tenant.IsScoped ? hold with { TenantId = tenant.TenantId } : hold;
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
			query = query.Where(h => h.TenantId == tenantId || h.TenantId is null);
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
		var tenant = AmbientScope;

		var holds = _holds.Values
			.Where(h => h.IsActive &&
						h.TenantId == tenantId &&
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
			query = query.Where(h => h.TenantId == tenantId);
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
			query = query.Where(h => h.TenantId == tenantId);
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
	private static bool OwnedByAmbientTenant(TenantScope tenant, string? rowTenantId) =>
		!tenant.IsScoped || string.Equals(rowTenantId, tenant.TenantId, StringComparison.Ordinal);

	/// <summary>
	/// Decides whether a stored hold's tenant satisfies the ambient tenant term.
	/// </summary>
	/// <param name="tenant">The scope resolved once at the start of the operation.</param>
	/// <param name="rowTenantId">The tenant value stored on the hold.</param>
	/// <returns>
	/// <see langword="true"/> when multi-tenancy is not active, when the hold carries no tenant, or when the
	/// hold belongs to the ambient tenant; otherwise <see langword="false"/>.
	/// </returns>
	/// <remarks>
	/// <para>
	/// The single comparison site for this store: every tenant-facing operation routes through it rather than
	/// comparing a tenant value inline, so the match cannot be omitted at one call site and applied at
	/// another. The comparison is ordinal because a tenant identifier is case-sensitive throughout the
	/// framework — matching case-insensitively here would let two distinct tenants read each other's holds.
	/// </para>
	/// <para>
	/// A hold with no tenant is a <em>global</em> hold that blocks erasure for every tenant, so the term is
	/// <c>tenant matches OR tenant is absent</c> rather than a bare equality. A bare equality would drop
	/// global holds from a tenant's view, and a legal hold is a control that <em>blocks</em> erasure — losing
	/// one does not fail safe, it erases data a court order says to keep. It still excludes every other
	/// tenant's holds, which is the isolation this exists to provide.
	/// </para>
	/// <para>
	/// The scope is taken as a parameter rather than read here, because a caller that read it lazily — only
	/// once it had a hold in hand — would make failing closed depend on whether the store happened to hold
	/// data: an unresolved tenant would throw against a populated store and quietly return "not found"
	/// against an empty one. Each operation resolves the scope up front and passes it in, so the fail-closed
	/// throw is a property of the deployment rather than of the data.
	/// </para>
	/// </remarks>
	private static bool MatchesAmbientTenant(TenantScope tenant, string? rowTenantId) =>
		!tenant.IsScoped
		|| rowTenantId is null
		|| string.Equals(rowTenantId, tenant.TenantId, StringComparison.Ordinal);
}
