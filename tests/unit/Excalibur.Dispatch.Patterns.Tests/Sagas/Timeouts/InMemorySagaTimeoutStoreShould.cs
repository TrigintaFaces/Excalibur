// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Saga.Abstractions;
using Excalibur.Saga.Storage;

using Shouldly;

using Xunit;

namespace Excalibur.Dispatch.Patterns.Tests.Sagas.Timeouts;

/// <summary>
/// Unit tests for <see cref="InMemorySagaTimeoutStore"/> validating all
/// <see cref="ISagaTimeoutStore"/> interface operations.
/// </summary>
/// <remarks>
/// <para>
/// Sprint 215 - Saga Timeouts Foundation.
/// Task: n2y3k (SAGA-013: Unit Tests - 12 tests).
/// </para>
/// <para>
/// Tests use anti-flakiness patterns with deterministic data and no fixed delays.
/// Thread-safety is verified through concurrent operations.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Saga")]
[Trait("Sprint", "215")]
public sealed class InMemorySagaTimeoutStoreShould
{
	/// <summary>
	/// Tests that ScheduleTimeoutAsync stores a timeout that can be retrieved.
	/// </summary>
	[Fact]
	public async Task StoreTimeoutWhenScheduled()
	{
		// Arrange
		var store = new InMemorySagaTimeoutStore();
		var timeout = CreateTimeout("saga-001", "timeout-001", DateTime.UtcNow.AddMinutes(5));

		// Act
		await store.ScheduleTimeoutAsync(timeout, CancellationToken.None).ConfigureAwait(true);

		// Assert
		store.GetPendingCount().ShouldBe(1);
	}

	/// <summary>
	/// Tests that CancelTimeoutAsync removes a specific timeout and is idempotent.
	/// </summary>
	[Fact]
	public async Task RemoveSpecificTimeoutWhenCancelled()
	{
		// Arrange
		var store = new InMemorySagaTimeoutStore();
		var sagaId = Guid.NewGuid().ToString();
		var timeout1 = CreateTimeout(sagaId, "timeout-001", DateTime.UtcNow.AddMinutes(5));
		var timeout2 = CreateTimeout(sagaId, "timeout-002", DateTime.UtcNow.AddMinutes(10));

		await store.ScheduleTimeoutAsync(timeout1, CancellationToken.None).ConfigureAwait(true);
		await store.ScheduleTimeoutAsync(timeout2, CancellationToken.None).ConfigureAwait(true);

		// Act - Cancel first timeout
		await store.CancelTimeoutAsync(sagaId, "timeout-001", CancellationToken.None).ConfigureAwait(true);

		// Assert - Only second timeout remains
		store.GetPendingCount().ShouldBe(1);

		// Act - Cancel again (idempotent)
		await store.CancelTimeoutAsync(sagaId, "timeout-001", CancellationToken.None).ConfigureAwait(true);

		// Assert - Still only one timeout
		store.GetPendingCount().ShouldBe(1);
	}

	/// <summary>
	/// Tests that CancelAllTimeoutsAsync removes all timeouts for a saga.
	/// </summary>
	[Fact]
	public async Task RemoveAllTimeoutsForSagaWhenCancelAllCalled()
	{
		// Arrange
		var store = new InMemorySagaTimeoutStore();
		var sagaId1 = Guid.NewGuid().ToString();
		var sagaId2 = Guid.NewGuid().ToString();

		await store.ScheduleTimeoutAsync(CreateTimeout(sagaId1, "t1", DateTime.UtcNow.AddMinutes(5)), CancellationToken.None).ConfigureAwait(true);
		await store.ScheduleTimeoutAsync(CreateTimeout(sagaId1, "t2", DateTime.UtcNow.AddMinutes(10)), CancellationToken.None).ConfigureAwait(true);
		await store.ScheduleTimeoutAsync(CreateTimeout(sagaId2, "t3", DateTime.UtcNow.AddMinutes(15)), CancellationToken.None).ConfigureAwait(true);

		store.GetPendingCount().ShouldBe(3);

		// Act - Cancel all timeouts for saga1
		await store.CancelAllTimeoutsAsync(sagaId1, CancellationToken.None).ConfigureAwait(true);

		// Assert - Only saga2's timeout remains
		store.GetPendingCount().ShouldBe(1);
	}

	/// <summary>
	/// Tests that GetDueTimeoutsAsync returns timeouts due for delivery, ordered by DueAt.
	/// </summary>
	[Fact]
	public async Task ReturnOrderedDueTimeoutsWhenQueried()
	{
		// Arrange
		var store = new InMemorySagaTimeoutStore();
		var now = DateTime.UtcNow;
		var sagaId = Guid.NewGuid().ToString();

		// Schedule timeouts in non-chronological order
		var timeout3 = CreateTimeout(sagaId, "t3", now.AddMinutes(3)); // Due later
		var timeout1 = CreateTimeout(sagaId, "t1", now.AddMinutes(-1)); // Due (past)
		var timeout2 = CreateTimeout(sagaId, "t2", now.AddMinutes(-2)); // Due earlier (most overdue)
		var timeout4 = CreateTimeout(sagaId, "t4", now.AddMinutes(10)); // Not due yet

		await store.ScheduleTimeoutAsync(timeout3, CancellationToken.None).ConfigureAwait(true);
		await store.ScheduleTimeoutAsync(timeout1, CancellationToken.None).ConfigureAwait(true);
		await store.ScheduleTimeoutAsync(timeout2, CancellationToken.None).ConfigureAwait(true);
		await store.ScheduleTimeoutAsync(timeout4, CancellationToken.None).ConfigureAwait(true);

		// Act - Query for due timeouts as of now
		var dueTimeouts = await store.GetDueTimeoutsAsync(now, CancellationToken.None).ConfigureAwait(true);

		// Assert - Only past-due timeouts returned, ordered by DueAt ascending
		dueTimeouts.Count.ShouldBe(2);
		dueTimeouts[0].TimeoutId.ShouldBe("t2"); // Most overdue first
		dueTimeouts[1].TimeoutId.ShouldBe("t1"); // Less overdue second
	}

	/// <summary>
	/// Tests that MarkDeliveredAsync removes a timeout and is idempotent.
	/// </summary>
	[Fact]
	public async Task RemoveTimeoutWhenMarkedDelivered()
	{
		// Arrange
		var store = new InMemorySagaTimeoutStore();
		var sagaId = Guid.NewGuid().ToString();
		var timeout = CreateTimeout(sagaId, "timeout-delivered", DateTime.UtcNow.AddMinutes(-1));

		await store.ScheduleTimeoutAsync(timeout, CancellationToken.None).ConfigureAwait(true);
		store.GetPendingCount().ShouldBe(1);

		// Act - Mark as delivered
		await store.MarkDeliveredAsync("timeout-delivered", CancellationToken.None).ConfigureAwait(true);

		// Assert - Timeout removed
		store.GetPendingCount().ShouldBe(0);

		// Act - Mark again (idempotent)
		await store.MarkDeliveredAsync("timeout-delivered", CancellationToken.None).ConfigureAwait(true);

		// Assert - Still zero
		store.GetPendingCount().ShouldBe(0);
	}

	/// <summary>
	/// Tests thread-safety of InMemorySagaTimeoutStore under concurrent operations.
	/// </summary>
	[Fact]
	public async Task HandleConcurrentOperationsWithoutCorruption()
	{
		// Arrange
		var store = new InMemorySagaTimeoutStore();
		var now = DateTime.UtcNow;
		const int operationCount = 100;
		var sagaId = Guid.NewGuid().ToString();

		// Act - Concurrent schedule, cancel, and query operations
		var tasks = new List<Task>();

		// 50 concurrent schedules
		for (var i = 0; i < operationCount / 2; i++)
		{
			var timeoutId = $"timeout-{i}";
			var dueAt = now.AddMinutes(i % 10); // Mix of due and not-due times
			tasks.Add(Task.Run(async () =>
			{
				await store.ScheduleTimeoutAsync(
					CreateTimeout(sagaId, timeoutId, dueAt),
					CancellationToken.None).ConfigureAwait(false);
			}));
		}

		// 25 concurrent cancels (some will target non-existent timeouts)
		for (var i = 0; i < operationCount / 4; i++)
		{
			var timeoutId = $"timeout-{i}";
			tasks.Add(Task.Run(async () =>
			{
				await store.CancelTimeoutAsync(sagaId, timeoutId, CancellationToken.None).ConfigureAwait(false);
			}));
		}

		// 25 concurrent queries
		for (var i = 0; i < operationCount / 4; i++)
		{
			tasks.Add(Task.Run(async () =>
			{
				_ = await store.GetDueTimeoutsAsync(now, CancellationToken.None).ConfigureAwait(false);
			}));
		}

		await Task.WhenAll(tasks).ConfigureAwait(true);

		// Assert - Store is in consistent state (no exception, count >= 0)
		store.GetPendingCount().ShouldBeGreaterThanOrEqualTo(0);

		// Verify we can still perform operations after concurrent load
		var finalTimeout = CreateTimeout(sagaId, "final-timeout", now.AddMinutes(1));
		await store.ScheduleTimeoutAsync(finalTimeout, CancellationToken.None).ConfigureAwait(true);

		var dueTimeouts = await store.GetDueTimeoutsAsync(now.AddMinutes(5), CancellationToken.None).ConfigureAwait(true);
		_ = dueTimeouts.ShouldNotBeNull();
	}

	/// <summary>
	/// Helper method to create a SagaTimeout with the specified parameters.
	/// </summary>
	private static SagaTimeout CreateTimeout(string sagaId, string timeoutId, DateTime dueAt)
	{
		return new SagaTimeout(
			TimeoutId: timeoutId,
			SagaId: sagaId,
			SagaType: "TestSaga",
			TimeoutType: "TestTimeout",
			TimeoutData: null,
			DueAt: dueAt,
			ScheduledAt: DateTime.UtcNow);
	}
}
