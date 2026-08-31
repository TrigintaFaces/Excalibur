// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch;
using Excalibur.Saga.Abstractions;
using Excalibur.Saga.Storage;

namespace Excalibur.Saga.Tests.Storage;

/// <summary>
/// Binds the tenant semantics of <see cref="InMemorySagaTimeoutStore"/> — the store a consumer gets by
/// default, since the timeout registration adds it via <c>TryAddSingleton</c>.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why this lock exists:</b> the two relational timeout stores carry the tenant on the row and bind it in
/// every cancel and mark-delivered predicate; the in-memory store carried no tenant term at all. Because
/// <c>SagaId</c> is caller-supplied — parsed off an inbound event — two tenants can present the same saga id
/// in one multi-tenant host, and a cancel-by-saga-id then deleted the other tenant's pending timeouts. That
/// is the consequence the SQL schema comment names in as many words.
/// </para>
/// <para>
/// <b>Both arms (testing-patterns §3), and the liveness arm is the load-bearing one.</b> A store that
/// silently cancelled nothing for anybody, or leased nothing at all, satisfies every isolation assertion
/// here perfectly. Two liveness arms exist to make that inert store fail: a tenant still cancels its
/// <em>own</em> timeout, and the claim path still returns <em>every</em> tenant's due timeouts.
/// </para>
/// <para>
/// <b>What deliberately stays unscoped.</b> <see cref="ISagaTimeoutStore.ClaimDueTimeoutsAsync"/> and
/// <see cref="ISagaTimeoutStore.GetDueTimeoutsAsync"/> are estate-wide by design: the delivery service runs
/// with no tenant established and re-establishes each row's tenant from the row before dispatching. Scoping
/// them would lease only the untenanted partition, leaving every tenant's timeouts due forever — a total
/// stall that presents as silence, and one a safety-only isolation test still passes.
/// </para>
/// <para>
/// <b>RED-on-mutant:</b> drop the tenant term from the cancel-by-saga-id predicate (restore
/// <c>kvp.Value.SagaId == sagaId</c>) ⇒ tenant A's cancel removes tenant B's timeout ⇒ the safety arm goes
/// RED. Scope the claim path to the ambient partition ⇒ the estate-wide liveness arm goes RED.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Saga")]
public sealed class InMemorySagaTimeoutStoreTenantIsolationShould
{
	private const string TenantA = "tenant-a";
	private const string TenantB = "tenant-b";

	/// <summary>The saga id both tenants present — the collision the tenant term has to survive.</summary>
	private const string SharedSagaId = "saga-shared";

	// The multi-tenant shape: the context the multi-tenancy composition installs resolves the ambient
	// value, which is what BeginScope below sets. The store now takes that context as a required
	// dependency instead of reading the holder itself, so these arms bind the same tenant terms through
	// the seam a real multi-tenant host uses.
	private readonly InMemorySagaTimeoutStore _store = new(new AmbientReadingTenantContext());

	[Fact]
	public async Task ConfineACancelBySagaIdToTheCallingTenantWhileStillCancellingItsOwn()
	{
		var ct = CancellationToken.None;
		await SeedBothTenantsAsync(ct).ConfigureAwait(false);

		// LIVENESS (a) — tenant A cancels its OWN timeout for the shared saga id. Without this arm a store
		// that cancels nothing for anyone passes the safety arm below.
		using (TenantContextHolder.BeginScope(TenantA))
		{
			await _store.CancelAllTimeoutsAsync(SharedSagaId, ct).ConfigureAwait(false);
		}

		var remaining = await _store.GetDueTimeoutsAsync(Far, ct).ConfigureAwait(false);

		// SAFETY — tenant B's timeout for the SAME saga id survives tenant A's cancel.
		_ = remaining.ShouldHaveSingleItem();
		remaining[0].TenantId.ShouldBe(TenantB, "tenant A's cancel must not reach tenant B's row.");
		remaining[0].TimeoutId.ShouldBe("timeout-b");
	}

	[Fact]
	public async Task ConfineACancelOfASingleTimeoutToTheCallingTenant()
	{
		var ct = CancellationToken.None;
		await SeedBothTenantsAsync(ct).ConfigureAwait(false);

		// SAFETY — tenant A naming tenant B's timeout id (and the shared saga id) removes nothing. Both
		// identifiers are caller-supplied, so the tenant term is an authorization control on this statement.
		using (TenantContextHolder.BeginScope(TenantA))
		{
			await _store.CancelTimeoutAsync(SharedSagaId, "timeout-b", ct).ConfigureAwait(false);
		}

		_store.GetPendingCount().ShouldBe(2, "a foreign-scoped cancel must remove nothing.");

		// LIVENESS — the same caller still cancels its own.
		using (TenantContextHolder.BeginScope(TenantA))
		{
			await _store.CancelTimeoutAsync(SharedSagaId, "timeout-a", ct).ConfigureAwait(false);
		}

		_store.GetPendingCount().ShouldBe(1, "tenant A still cancels its own timeout.");
	}

	[Fact]
	public async Task ConfineAMarkDeliveredToTheCallingTenant()
	{
		var ct = CancellationToken.None;
		await SeedBothTenantsAsync(ct).ConfigureAwait(false);

		// SAFETY — retiring a timeout is scoped, so one tenant cannot retire another's pending row.
		using (TenantContextHolder.BeginScope(TenantA))
		{
			await _store.MarkDeliveredAsync("timeout-b", ct).ConfigureAwait(false);
		}

		_store.GetPendingCount().ShouldBe(2, "a foreign-scoped mark-delivered must retire nothing.");

		// LIVENESS — under the row's own tenant it retires, which is what the delivery service does after
		// re-establishing that tenant. A mark that matched nothing would leave the row pending and it would
		// redeliver forever.
		using (TenantContextHolder.BeginScope(TenantB))
		{
			await _store.MarkDeliveredAsync("timeout-b", ct).ConfigureAwait(false);
		}

		_store.GetPendingCount().ShouldBe(1, "the owning tenant retires its own row.");
	}

	[Fact]
	public async Task StampTheAmbientTenantOnScheduleRatherThanTheCallerSuppliedTerm()
	{
		var ct = CancellationToken.None;

		// A caller claiming to be tenant B while scoped to tenant A. The row must land in A's partition:
		// a scheduled timeout cannot claim a tenant its caller never established.
		using (TenantContextHolder.BeginScope(TenantA))
		{
			await _store
				.ScheduleTimeoutAsync(NewTimeout("timeout-a", SharedSagaId) with { TenantId = TenantB }, ct)
				.ConfigureAwait(false);
		}

		var stored = await _store.GetDueTimeoutsAsync(Far, ct).ConfigureAwait(false);

		_ = stored.ShouldHaveSingleItem();
		stored[0].TenantId.ShouldBe(TenantA, "the ambient scope places the row, never the caller's payload.");
	}

	[Fact]
	public async Task LeaseDueTimeoutsAcrossEveryTenantFromAnUnscopedDeliveryLoop()
	{
		var ct = CancellationToken.None;
		await SeedBothTenantsAsync(ct).ConfigureAwait(false);

		// LIVENESS — the delivery service runs with NO tenant established. If the claim path were scoped it
		// would lease only the untenanted partition and both rows below would sit due forever. This arm is
		// the one that fails when the claim path is "secured" by scoping it.
		var claimed = await _store.ClaimDueTimeoutsAsync(Far, batchSize: 10, ct).ConfigureAwait(false);

		claimed.Count.ShouldBe(2, "the claim path is deliberately estate-wide.");
		var tenants = claimed.Select(t => t.TenantId).OrderBy(t => t, StringComparer.Ordinal).ToList();
		tenants.ShouldBe(
			[TenantA, TenantB],
			customMessage: "and it carries each row's own tenant back for re-establishment.");
	}

	/// <summary>A due-at far enough in the future that every seeded timeout is due.</summary>
	private static DateTimeOffset Far => DateTimeOffset.UtcNow.AddHours(1);

	/// <summary>
	/// Seeds one timeout per tenant, both under <see cref="SharedSagaId"/> — the cross-tenant saga-id
	/// collision the tenant term exists to survive.
	/// </summary>
	private async Task SeedBothTenantsAsync(CancellationToken ct)
	{
		using (TenantContextHolder.BeginScope(TenantA))
		{
			await _store.ScheduleTimeoutAsync(NewTimeout("timeout-a", SharedSagaId), ct).ConfigureAwait(false);
		}

		using (TenantContextHolder.BeginScope(TenantB))
		{
			await _store.ScheduleTimeoutAsync(NewTimeout("timeout-b", SharedSagaId), ct).ConfigureAwait(false);
		}

		_store.GetPendingCount().ShouldBe(2, "both tenants' timeouts coexist under the shared saga id.");
	}

	/// <summary>
	/// Mirrors the ambient context the multi-tenancy composition registers: it resolves whatever tenant the
	/// surrounding <see cref="TenantContextHolder.BeginScope"/> established.
	/// </summary>
	private sealed class AmbientReadingTenantContext : ITenantContext
	{
		public string? TenantId => TenantContextHolder.Current;

		public bool HasTenant => !string.IsNullOrEmpty(TenantId);
	}

	private static SagaTimeout NewTimeout(string timeoutId, string sagaId) => new(
		timeoutId,
		sagaId,
		"TestSaga",
		"TestTimeout",
		TimeoutData: null,
		DueAt: DateTimeOffset.UtcNow.AddMinutes(-1),
		ScheduledAt: DateTimeOffset.UtcNow.AddMinutes(-2));
}
