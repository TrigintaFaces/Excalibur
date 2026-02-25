// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

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
		_store = new InMemorySagaTimeoutStore();
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

		// Act
		await _store.MarkDeliveredAsync("timeout-1", CancellationToken.None);

		// Assert
		_store.GetPendingCount().ShouldBe(0);
	}

	[Fact]
	public async Task MarkDeliveredAsync_ThrowsArgumentException_WhenTimeoutIdIsNull()
	{
		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentException>(() =>
			_store.MarkDeliveredAsync(null!, CancellationToken.None));
	}

	[Fact]
	public async Task MarkDeliveredAsync_ThrowsArgumentException_WhenTimeoutIdIsEmpty()
	{
		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentException>(() =>
			_store.MarkDeliveredAsync("", CancellationToken.None));
	}

	[Fact]
	public async Task MarkDeliveredAsync_IsIdempotent_WhenTimeoutDoesNotExist()
	{
		// Act & Assert - Should not throw
		await Should.NotThrowAsync(() =>
			_store.MarkDeliveredAsync("nonexistent", CancellationToken.None));
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
