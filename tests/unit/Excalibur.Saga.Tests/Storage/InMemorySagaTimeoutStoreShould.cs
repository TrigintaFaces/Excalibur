// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch;

using Excalibur.Saga.Abstractions;
using Excalibur.Saga.Storage;

namespace Excalibur.Saga.Tests.Storage;

/// <summary>
/// Unit tests for <see cref="InMemorySagaTimeoutStore"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Saga")]
public sealed class InMemorySagaTimeoutStoreShould
{
	private readonly InMemorySagaTimeoutStore _store;

	public InMemorySagaTimeoutStoreShould()
	{
		_store = new InMemorySagaTimeoutStore(new TestTenantContext());
	}

	#region ScheduleTimeoutAsync Tests

	[Fact]
	public async Task ScheduleTimeoutAsync_AddsTimeout()
	{
		// Arrange
		var timeout = CreateTimeout("timeout-1", "saga-1");

		// Act
		await _store.ScheduleTimeoutAsync(timeout, CancellationToken.None);

		// Assert
		_store.GetPendingCount().ShouldBe(1);
	}

	[Fact]
	public async Task ScheduleTimeoutAsync_ThrowsArgumentNullException_WhenTimeoutIsNull()
	{
		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentNullException>(() =>
			_store.ScheduleTimeoutAsync(null!, CancellationToken.None));
	}

	[Fact]
	public async Task ScheduleTimeoutAsync_OverwritesExistingTimeout_WithSameId()
	{
		// Arrange
		var timeout1 = CreateTimeout("timeout-1", "saga-1", DateTime.UtcNow.AddMinutes(5));
		var timeout2 = CreateTimeout("timeout-1", "saga-1", DateTime.UtcNow.AddMinutes(10));

		// Act
		await _store.ScheduleTimeoutAsync(timeout1, CancellationToken.None);
		await _store.ScheduleTimeoutAsync(timeout2, CancellationToken.None);

		// Assert
		_store.GetPendingCount().ShouldBe(1);
		var dueTimeouts = await _store.GetDueTimeoutsAsync(DateTime.UtcNow.AddMinutes(15), CancellationToken.None);
		dueTimeouts[0].DueAt.ShouldBe(timeout2.DueAt);
	}

	[Fact]
	public async Task ScheduleTimeoutAsync_AllowsMultipleTimeoutsForSameSaga()
	{
		// Arrange
		var timeout1 = CreateTimeout("timeout-1", "saga-1");
		var timeout2 = CreateTimeout("timeout-2", "saga-1");

		// Act
		await _store.ScheduleTimeoutAsync(timeout1, CancellationToken.None);
		await _store.ScheduleTimeoutAsync(timeout2, CancellationToken.None);

		// Assert
		_store.GetPendingCount().ShouldBe(2);
	}

	#endregion ScheduleTimeoutAsync Tests

	#region CancelTimeoutAsync Tests

	[Fact]
	public async Task CancelTimeoutAsync_RemovesTimeout()
	{
		// Arrange
		var timeout = CreateTimeout("timeout-1", "saga-1");
		await _store.ScheduleTimeoutAsync(timeout, CancellationToken.None);

		// Act
		await _store.CancelTimeoutAsync("saga-1", "timeout-1", CancellationToken.None);

		// Assert
		_store.GetPendingCount().ShouldBe(0);
	}

	[Fact]
	public async Task CancelTimeoutAsync_ThrowsArgumentException_WhenTimeoutIdIsNull()
	{
		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentException>(() =>
			_store.CancelTimeoutAsync("saga-1", null!, CancellationToken.None));
	}

	[Fact]
	public async Task CancelTimeoutAsync_ThrowsArgumentException_WhenTimeoutIdIsEmpty()
	{
		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentException>(() =>
			_store.CancelTimeoutAsync("saga-1", "", CancellationToken.None));
	}

	[Fact]
	public async Task CancelTimeoutAsync_IsIdempotent_WhenTimeoutDoesNotExist()
	{
		// Act & Assert - Should not throw
		await Should.NotThrowAsync(() =>
			_store.CancelTimeoutAsync("saga-1", "nonexistent", CancellationToken.None));
	}

	#endregion CancelTimeoutAsync Tests

	#region CancelAllTimeoutsAsync Tests

	[Fact]
	public async Task CancelAllTimeoutsAsync_RemovesAllTimeoutsForSaga()
	{
		// Arrange
		var timeout1 = CreateTimeout("timeout-1", "saga-1");
		var timeout2 = CreateTimeout("timeout-2", "saga-1");
		var timeout3 = CreateTimeout("timeout-3", "saga-2");

		await _store.ScheduleTimeoutAsync(timeout1, CancellationToken.None);
		await _store.ScheduleTimeoutAsync(timeout2, CancellationToken.None);
		await _store.ScheduleTimeoutAsync(timeout3, CancellationToken.None);

		// Act
		await _store.CancelAllTimeoutsAsync("saga-1", CancellationToken.None);

		// Assert
		_store.GetPendingCount().ShouldBe(1);
		var remaining = await _store.GetDueTimeoutsAsync(DateTimeOffset.MaxValue, CancellationToken.None);
		remaining[0].SagaId.ShouldBe("saga-2");
	}

	[Fact]
	public async Task CancelAllTimeoutsAsync_ThrowsArgumentException_WhenSagaIdIsNull()
	{
		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentException>(() =>
			_store.CancelAllTimeoutsAsync(null!, CancellationToken.None));
	}

	[Fact]
	public async Task CancelAllTimeoutsAsync_ThrowsArgumentException_WhenSagaIdIsEmpty()
	{
		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentException>(() =>
			_store.CancelAllTimeoutsAsync("", CancellationToken.None));
	}

	[Fact]
	public async Task CancelAllTimeoutsAsync_IsIdempotent_WhenNoTimeoutsExist()
	{
		// Act & Assert - Should not throw
		await Should.NotThrowAsync(() =>
			_store.CancelAllTimeoutsAsync("saga-1", CancellationToken.None));
	}

	#endregion CancelAllTimeoutsAsync Tests

	#region GetDueTimeoutsAsync Tests

	[Fact]
	public async Task GetDueTimeoutsAsync_ReturnsEmptyList_WhenNoTimeouts()
	{
		// Act
		var result = await _store.GetDueTimeoutsAsync(DateTime.UtcNow, CancellationToken.None);

		// Assert
		result.ShouldBeEmpty();
	}

	[Fact]
	public async Task GetDueTimeoutsAsync_ReturnsOnlyDueTimeouts()
	{
		// Arrange
		var now = DateTime.UtcNow;
		var pastTimeout = CreateTimeout("timeout-1", "saga-1", now.AddMinutes(-5));
		var futureTimeout = CreateTimeout("timeout-2", "saga-2", now.AddMinutes(5));

		await _store.ScheduleTimeoutAsync(pastTimeout, CancellationToken.None);
		await _store.ScheduleTimeoutAsync(futureTimeout, CancellationToken.None);

		// Act
		var result = await _store.GetDueTimeoutsAsync(now, CancellationToken.None);

		// Assert
		result.Count.ShouldBe(1);
		result[0].TimeoutId.ShouldBe("timeout-1");
	}

	[Fact]
	public async Task GetDueTimeoutsAsync_ReturnsTimeoutsOrderedByDueAt()
	{
		// Arrange
		var now = DateTime.UtcNow;
		var timeout1 = CreateTimeout("timeout-1", "saga-1", now.AddMinutes(-3));
		var timeout2 = CreateTimeout("timeout-2", "saga-2", now.AddMinutes(-1));
		var timeout3 = CreateTimeout("timeout-3", "saga-3", now.AddMinutes(-5));

		await _store.ScheduleTimeoutAsync(timeout1, CancellationToken.None);
		await _store.ScheduleTimeoutAsync(timeout2, CancellationToken.None);
		await _store.ScheduleTimeoutAsync(timeout3, CancellationToken.None);

		// Act
		var result = await _store.GetDueTimeoutsAsync(now, CancellationToken.None);

		// Assert
		result.Count.ShouldBe(3);
		result[0].TimeoutId.ShouldBe("timeout-3"); // Oldest first
		result[1].TimeoutId.ShouldBe("timeout-1");
		result[2].TimeoutId.ShouldBe("timeout-2");
	}

	[Fact]
	public async Task GetDueTimeoutsAsync_IncludesTimeoutsWithExactDueTime()
	{
		// Arrange
		var now = DateTime.UtcNow;
		var timeout = CreateTimeout("timeout-1", "saga-1", now);

		await _store.ScheduleTimeoutAsync(timeout, CancellationToken.None);

		// Act
		var result = await _store.GetDueTimeoutsAsync(now, CancellationToken.None);

		// Assert
		result.Count.ShouldBe(1);
	}

	#endregion GetDueTimeoutsAsync Tests

	#region MarkDeliveredAsync Tests

	[Fact]
	public async Task MarkDeliveredAsync_RemovesTimeout()
	{
		// Arrange
		var timeout = CreateTimeout("timeout-1", "saga-1");
		await _store.ScheduleTimeoutAsync(timeout, CancellationToken.None);

		// Claim first: retirement is claim-conditional, so a caller that never claimed cannot retire.
		var claims = await _store.ClaimDueTimeoutsAsync(DateTimeOffset.UtcNow, 10, CancellationToken.None);
		var claim = claims.Single(c => c.Timeout.TimeoutId == "timeout-1");

		// Act
		var outcome = await _store.MarkDeliveredAsync(claim, CancellationToken.None);

		// Assert
		outcome.ShouldBe(SagaTimeoutRetirementOutcome.Retired);
		_store.GetPendingCount().ShouldBe(0);
	}

	[Fact]
	public async Task MarkDeliveredAsync_ThrowsArgumentNullException_WhenClaimIsNull()
	{
		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentNullException>(() =>
			_store.MarkDeliveredAsync(null!, CancellationToken.None));
	}

	[Fact]
	public async Task MarkDeliveredAsync_ReportsSuperseded_WhenTheClaimWasNeverHeld()
	{
		// A fabricated claim for a timeout this caller never claimed. It does not throw -- a store cannot
		// distinguish "never existed" from "already retired by the live owner" once the row is gone, which
		// is exactly why the outcome has two values and not three. What it must NOT do is report success.
		var neverHeld = new ClaimedSagaTimeout(CreateTimeout("nonexistent", "saga-1"), "not-a-real-token");

		var outcome = await _store.MarkDeliveredAsync(neverHeld, CancellationToken.None);

		outcome.ShouldBe(SagaTimeoutRetirementOutcome.Superseded);
	}


	[Fact]
	public async Task RefuseAStaleClaimantAfterTheLeaseWasTakenOver_AndStillLetTheLiveOneRetire()
	{
		// THE INTERLEAVING THIS WHOLE CONTRACT EXISTS FOR, and it needs no concurrency to construct.
		//
		//   A claims T                      -> token A
		//   A stalls past the lease         -> the lease goes stale; this is the state the lease exists for
		//   B re-claims T                   -> token B, legitimately
		//   A resumes and retires T         <- MUST BE REFUSED. If it lands, B's own delivery has no row
		//                                      left to retry, and the saga waits forever for a timeout that
		//                                      no longer exists: zero deliveries plus deletion.
		//
		// The lease is 120s, so a second claim 121s later is stale by construction -- no wall-clock waiting
		// and no sleep, because asOf is a parameter rather than a read of the system clock.
		var ct = CancellationToken.None;
		var start = DateTimeOffset.UtcNow;
		await _store.ScheduleTimeoutAsync(CreateTimeout("timeout-contested", "saga-1"), ct);

		var claimA = (await _store.ClaimDueTimeoutsAsync(start, 10, ct)).Single();
		var claimB = (await _store.ClaimDueTimeoutsAsync(start.AddSeconds(121), 10, ct)).Single();

		claimB.ClaimToken.ShouldNotBe(
			claimA.ClaimToken,
			"a re-claim after lease expiry must mint a NEW token -- if the token were the process identity "
			+ "it would be identical here and the predicate below could not tell the two claims apart");

		// SAFETY - the stale claimant is refused and the row survives for the live claimant to retry.
		var stale = await _store.MarkDeliveredAsync(claimA, ct);
		stale.ShouldBe(SagaTimeoutRetirementOutcome.Superseded);
		_store.GetPendingCount().ShouldBe(1, "the row must survive a stale claimant's retirement attempt");

		// LIVENESS - the live claimant still retires it. Without this arm a store that refused EVERY
		// retirement would satisfy the safety half above and be completely broken.
		var live = await _store.MarkDeliveredAsync(claimB, ct);
		live.ShouldBe(SagaTimeoutRetirementOutcome.Retired);
		_store.GetPendingCount().ShouldBe(0, "the holder of the current claim retires the row");
	}

	#endregion MarkDeliveredAsync Tests

	#region Clear Tests

	[Fact]
	public async Task Clear_RemovesAllTimeouts()
	{
		// Arrange
		var timeout1 = CreateTimeout("timeout-1", "saga-1");
		var timeout2 = CreateTimeout("timeout-2", "saga-2");

		await _store.ScheduleTimeoutAsync(timeout1, CancellationToken.None);
		await _store.ScheduleTimeoutAsync(timeout2, CancellationToken.None);

		// Act
		_store.Clear();

		// Assert
		_store.GetPendingCount().ShouldBe(0);
	}

	#endregion Clear Tests

	#region GetPendingCount Tests

	[Fact]
	public void GetPendingCount_ReturnsZero_WhenEmpty()
	{
		// Act
		var count = _store.GetPendingCount();

		// Assert
		count.ShouldBe(0);
	}

	[Fact]
	public async Task GetPendingCount_ReturnsCorrectCount()
	{
		// Arrange
		await _store.ScheduleTimeoutAsync(CreateTimeout("timeout-1", "saga-1"), CancellationToken.None);
		await _store.ScheduleTimeoutAsync(CreateTimeout("timeout-2", "saga-2"), CancellationToken.None);
		await _store.ScheduleTimeoutAsync(CreateTimeout("timeout-3", "saga-3"), CancellationToken.None);

		// Act
		var count = _store.GetPendingCount();

		// Assert
		count.ShouldBe(3);
	}

	#endregion GetPendingCount Tests

	#region Interface Implementation Tests

	[Fact]
	public void ImplementsISagaTimeoutStore()
	{
		// Assert
		_ = _store.ShouldBeAssignableTo<ISagaTimeoutStore>();
	}

	#endregion Interface Implementation Tests

	#region Tenant Partition Tests

	/// <summary>
	/// The partition a scheduled timeout is stamped with must be the one the rest of the framework
	/// addresses.
	/// </summary>
	/// <remarks>
	/// This store resolved its partition by feeding an ambient read into the fold that rehydrates a value
	/// read back from storage. That fold maps an absent value onto the untenanted sentinel, which is the
	/// right answer for a column that holds no tenant and the wrong one for a context that was never
	/// established. On a single-tenant host the ambient value is never set, so every timeout was written
	/// into the reserved untenanted partition while every other store in the framework writes the default
	/// tenant identity. Nothing threw, and the timeout fired against a partition the saga does not live in.
	/// </remarks>
	[Fact]
	public async Task ScheduleTimeoutAsync_OnASingleTenantHost_StampsTheDefaultTenantNotTheUntenantedSentinel()
	{
		var store = new InMemorySagaTimeoutStore(new TestTenantContext());

		await store.ScheduleTimeoutAsync(CreateTimeout("timeout-tenancy", "saga-tenancy"), CancellationToken.None);
		var due = await store.GetDueTimeoutsAsync(DateTimeOffset.UtcNow, CancellationToken.None);

		var stamped = due.ShouldHaveSingleItem();
		stamped.TenantId.ShouldBe(TenantDefaults.DefaultTenantId);
		stamped.TenantId.ShouldNotBe(TenantScope.UntenantedSentinel);
	}

	/// <summary>
	/// A host that requires a tenant and has established none must be refused, not quietly given a
	/// partition.
	/// </summary>
	/// <remarks>
	/// This is the loud half of the same correction. The previous behaviour answered the question anyway —
	/// with the untenanted sentinel — so the timeout was scheduled successfully, delivered under a tenant
	/// the saga was never saved under, and the saga simply never completed. There was no exception and no
	/// log line to find afterwards. Refusing at the point the partition cannot be decided keeps the failure
	/// where it can be diagnosed.
	/// </remarks>
	[Fact]
	public async Task ScheduleTimeoutAsync_WhenTheContextResolvesNoTenant_RefusesRatherThanStampingSilently()
	{
		var store = new InMemorySagaTimeoutStore(new TestTenantContext(tenantId: null));

		_ = await Should.ThrowAsync<TenantRequiredException>(async () =>
			await store.ScheduleTimeoutAsync(CreateTimeout("timeout-none", "saga-none"), CancellationToken.None));
	}

	/// <summary>
	/// The tenant context is a required dependency: a store that could be built without one would resolve a
	/// different partition depending on whether a context happened to be registered.
	/// </summary>
	[Fact]
	public void Constructor_WithNullTenantContext_ThrowsArgumentNullException()
	{
		_ = Should.Throw<ArgumentNullException>(() => new InMemorySagaTimeoutStore(tenantContext: null!));
	}

	#endregion Tenant Partition Tests

	private static SagaTimeout CreateTimeout(string timeoutId, string sagaId, DateTime? dueAt = null)
	{
		return new SagaTimeout(
			TimeoutId: timeoutId,
			SagaId: sagaId,
			SagaType: "TestSaga",
			TimeoutType: "TestTimeout",
			TimeoutData: null,
			DueAt: dueAt ?? DateTime.UtcNow.AddMinutes(-1),
			ScheduledAt: DateTime.UtcNow);
	}
}
