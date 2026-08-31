// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.EventSourcing.Projections;

using Shouldly;

using Excalibur.Dispatch;

using Xunit;

namespace Excalibur.EventSourcing.Tests.Projections;

/// <summary>
/// Binds the requirement that two tenants running the same projection keep separate cursor positions, so
/// one tenant's progress can never move or erase another's.
/// </summary>
/// <remarks>
/// <para>
/// The tenant term is AMBIENT, not a parameter. <c>ICursorMapStore</c> is unchanged: the identity is
/// injected into each implementation at construction and resolved per call, so a caller cannot widen the
/// lookup by omitting a tenant nor redirect it by naming another. Adding a <c>tenantId</c> argument was
/// considered and rejected -- it rebuilds the authorisation hole the compliance stores closed by refusing
/// to consult such a field at all.
/// </para>
/// <para>
/// This lock was written RED ahead of the repair, because the defect it describes is a silent projection
/// gap: no error, no exception, just events never projected for one tenant because another tenant's cursor
/// said they already had been. It is green now, and it stays as the regression lock.
/// </para>
/// <para>
/// Every implementation partitions, not just this one. <c>PostgresCursorMapStore</c> and
/// <c>SqlServerCursorMapStore</c> now carry the tenant inside the primary key and in every predicate --
/// including the reset DELETE and the SQL Server MERGE match, where an omitted term would delete or
/// overwrite another tenant's row rather than merely read it.
/// </para>
/// <para>
/// This arm still exercises the in-memory store, because that is what makes the property provable without
/// infrastructure. <b>It is therefore NOT evidence for the SQL providers</b> -- no unit test can bind those
/// without a real database. Their equivalent arms now exist and run against real containers:
/// <c>PostgresCursorMapTenantIsolationShould</c> and <c>SqlServerCursorMapTenantIsolationShould</c> in the
/// integration suite construct each SQL store and hold it to the same safety and liveness pair. Read this
/// arm as "the contract is expressible and the in-memory implementation honours it", and read those two as
/// the evidence for the sentence above.
/// </para>
/// <para>
/// Direction of failure matters. A cursor moved FORWARD by another tenant means the projector skips events
/// it never processed -- data missing from a read model, permanently, with nothing to alert on. A cursor
/// moved backward merely reprojects, which an idempotent projection absorbs.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "EventSourcing")]
public sealed class CursorMapTenantIsolationShould
{
	private const string SharedProjectionName = "OrderSummaryProjection";
	private const string StreamId = "stream-1";

	/// <summary>
	/// SAFETY: one tenant's cursor must not be visible to another tenant of the same projection.
	/// </summary>
	[Fact]
	public async Task NotExposeOneTenantsCursorToAnotherTenant()
	{
		// Two tenants running the SAME projection -- the ordinary multi-tenant topology.
		//
		// The tenant is AMBIENT, resolved at construction, so each tenant's view is its own store instance
		// over the same projection name. This arm was written as EXPECTED RED while the contract carried no
		// tenant at all, and it anticipated the repair arriving as a tenantId PARAMETER. It did not, and
		// deliberately so: a caller-supplied tenant would let a reader widen or redirect the lookup, which
		// is the authorisation hole the compliance stores closed by refusing to consult such a field. The
		// identity now comes from context that the caller cannot name, so the arm binds that instead.
		var tenantA = new InMemoryCursorMapStore(new FixedTenantContext("tenant-a"));
		var tenantB = new InMemoryCursorMapStore(new FixedTenantContext("tenant-b"));

		// Tenant A has projected up to position 500.
		await tenantA.SaveCursorMapAsync(
			SharedProjectionName,
			new Dictionary<string, long> { [StreamId] = 500 },
			CancellationToken.None).ConfigureAwait(false);

		// Tenant B has projected nothing and must start from the beginning.
		var tenantBCursor = await tenantB
			.GetCursorMapAsync(SharedProjectionName, CancellationToken.None).ConfigureAwait(false);

		tenantBCursor.ShouldBeEmpty(
			"tenant B has projected nothing, so its cursor map must be empty. If it reads tenant A's "
			+ "position of 500 it skips every event below that mark -- never projected for B, and nothing "
			+ "reports an error. That is a silent, permanent gap in a read model, and it is the direction "
			+ "that matters: a cursor moved BACKWARD merely reprojects, which an idempotent projection "
			+ "absorbs.");
	}

	/// <summary>
	/// Implements <see cref="ITenantContext"/> DIRECTLY, inheriting no first-party base, so these arms bind
	/// the interface's own requirement rather than re-testing an inherited convenience.
	/// </summary>
	private sealed class FixedTenantContext(string tenantId) : ITenantContext
	{
		public string? TenantId { get; } = tenantId;

		public bool HasTenant => !string.IsNullOrWhiteSpace(TenantId);
	}

	/// <summary>
	/// LIVENESS: a tenant must still read back its own saved cursor.
	/// </summary>
	/// <remarks>
	/// Not optional. A store that returned an empty map to everyone would satisfy the isolation arm above
	/// perfectly while losing all projection progress -- every projection restarting from zero on every
	/// run. Whatever tenant term the repair introduces, re-reading with the SAME identity must still return
	/// what was written.
	/// </remarks>
	[Fact]
	public async Task StillReturnATenantsOwnSavedCursor()
	{
		var store = new InMemoryCursorMapStore(new FixedTenantContext("tenant-a"));

		await store.SaveCursorMapAsync(
			SharedProjectionName,
			new Dictionary<string, long> { [StreamId] = 500 },
			CancellationToken.None).ConfigureAwait(false);

		var reread = await store.GetCursorMapAsync(SharedProjectionName, CancellationToken.None)
			.ConfigureAwait(false);

		reread.ShouldContainKey(
			StreamId,
			"the tenant that saved this cursor must read it back -- a store returning empty to everyone "
			+ "would pass the isolation arm while losing all projection progress");
		reread[StreamId].ShouldBe(500);
	}

	/// <summary>
	/// A different projection name is a different cursor, for the same tenant.
	/// </summary>
	/// <remarks>
	/// Guards the opposite over-correction: a repair keyed on the tenant that DROPPED the projection name
	/// would collapse every projection a tenant runs into one shared position.
	/// </remarks>
	[Fact]
	public async Task NotShareACursorBetweenDifferentProjections()
	{
		var store = new InMemoryCursorMapStore(new FixedTenantContext("tenant-a"));

		await store.SaveCursorMapAsync(
			SharedProjectionName,
			new Dictionary<string, long> { [StreamId] = 500 },
			CancellationToken.None).ConfigureAwait(false);

		var otherProjection = await store
			.GetCursorMapAsync("InvoiceSummaryProjection", CancellationToken.None).ConfigureAwait(false);

		otherProjection.ShouldBeEmpty(
			"a different projection is a different cursor and must not inherit this one's position");
	}
}
