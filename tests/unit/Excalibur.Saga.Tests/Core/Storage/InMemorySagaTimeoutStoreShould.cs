// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Saga.Abstractions;
using Excalibur.Saga.Storage;

namespace Excalibur.Saga.Tests.Core.Storage;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class InMemorySagaTimeoutStoreShould
{
	private readonly InMemorySagaTimeoutStore _sut = new();

	[Fact]
	public async Task ScheduleTimeoutAsync_StoreTimeout()
	{
		// Arrange
		var timeout = CreateTimeout("saga-1", "timeout-1", DateTimeOffset.UtcNow.AddMinutes(5));

		// Act
		await _sut.ScheduleTimeoutAsync(timeout, CancellationToken.None);

		// Assert
		_sut.GetPendingCount().ShouldBe(1);
	}

	[Fact]
	public async Task ScheduleTimeoutAsync_ThrowOnNull()
	{
		await Should.ThrowAsync<ArgumentNullException>(
			() => _sut.ScheduleTimeoutAsync(null!, CancellationToken.None));
	}

	[Fact]
	public async Task ScheduleTimeoutAsync_OverwriteExistingTimeout()
	{
		// Arrange — same TimeoutId means it's keyed by TimeoutId
		var timeout1 = CreateTimeout("saga-1", "timeout-1", DateTimeOffset.UtcNow.AddMinutes(5));
		var timeout2 = CreateTimeout("saga-1", "timeout-1", DateTimeOffset.UtcNow.AddMinutes(10));

		// Act
		await _sut.ScheduleTimeoutAsync(timeout1, CancellationToken.None);
		await _sut.ScheduleTimeoutAsync(timeout2, CancellationToken.None);

		// Assert
		_sut.GetPendingCount().ShouldBe(1);
	}

	[Fact]
	public async Task CancelTimeoutAsync_RemoveTimeout()
	{
		// Arrange
		var timeout = CreateTimeout("saga-1", "timeout-1", DateTimeOffset.UtcNow.AddMinutes(5));
		await _sut.ScheduleTimeoutAsync(timeout, CancellationToken.None);

		// Act
		await _sut.CancelTimeoutAsync("saga-1", "timeout-1", CancellationToken.None);

		// Assert
		_sut.GetPendingCount().ShouldBe(0);
	}

	[Fact]
	public async Task CancelTimeoutAsync_BeIdempotent_WhenNotFound()
	{
		// Act & Assert - should not throw
		await _sut.CancelTimeoutAsync("saga-1", "nonexistent", CancellationToken.None);
		_sut.GetPendingCount().ShouldBe(0);
	}

	[Fact]
	public async Task CancelTimeoutAsync_ThrowOnNullTimeoutId()
	{
		await Should.ThrowAsync<ArgumentException>(
			() => _sut.CancelTimeoutAsync("saga-1", null!, CancellationToken.None));
	}

	[Fact]
	public async Task CancelAllTimeoutsAsync_RemoveAllForSaga()
	{
		// Arrange
		await _sut.ScheduleTimeoutAsync(CreateTimeout("saga-1", "t1", DateTimeOffset.UtcNow.AddMinutes(5)), CancellationToken.None);
		await _sut.ScheduleTimeoutAsync(CreateTimeout("saga-1", "t2", DateTimeOffset.UtcNow.AddMinutes(10)), CancellationToken.None);
		await _sut.ScheduleTimeoutAsync(CreateTimeout("saga-2", "t3", DateTimeOffset.UtcNow.AddMinutes(5)), CancellationToken.None);

		// Act
		await _sut.CancelAllTimeoutsAsync("saga-1", CancellationToken.None);

		// Assert
		_sut.GetPendingCount().ShouldBe(1); // Only saga-2's timeout remains
	}

	[Fact]
	public async Task CancelAllTimeoutsAsync_ThrowOnNullSagaId()
	{
		await Should.ThrowAsync<ArgumentException>(
			() => _sut.CancelAllTimeoutsAsync(null!, CancellationToken.None));
	}

	[Fact]
	public async Task GetDueTimeoutsAsync_ReturnOnlyDueTimeouts()
	{
		// Arrange
		var now = DateTimeOffset.UtcNow;
		await _sut.ScheduleTimeoutAsync(CreateTimeout("saga-1", "t1", now.AddMinutes(-5)), CancellationToken.None);
		await _sut.ScheduleTimeoutAsync(CreateTimeout("saga-2", "t2", now.AddMinutes(-1)), CancellationToken.None);
		await _sut.ScheduleTimeoutAsync(CreateTimeout("saga-3", "t3", now.AddMinutes(5)), CancellationToken.None);

		// Act
		var due = await _sut.GetDueTimeoutsAsync(now, CancellationToken.None);

		// Assert
		due.Count.ShouldBe(2);
	}

	[Fact]
	public async Task GetDueTimeoutsAsync_ReturnOrderedByDueAt()
	{
		// Arrange
		var now = DateTimeOffset.UtcNow;
		await _sut.ScheduleTimeoutAsync(CreateTimeout("saga-1", "t1", now.AddMinutes(-1)), CancellationToken.None);
		await _sut.ScheduleTimeoutAsync(CreateTimeout("saga-2", "t2", now.AddMinutes(-10)), CancellationToken.None);
		await _sut.ScheduleTimeoutAsync(CreateTimeout("saga-3", "t3", now.AddMinutes(-5)), CancellationToken.None);

		// Act
		var due = await _sut.GetDueTimeoutsAsync(now, CancellationToken.None);

		// Assert
		due.Count.ShouldBe(3);
		due[0].TimeoutId.ShouldBe("t2"); // Earliest
		due[1].TimeoutId.ShouldBe("t3");
		due[2].TimeoutId.ShouldBe("t1"); // Latest
	}

	[Fact]
	public async Task GetDueTimeoutsAsync_ReturnEmpty_WhenNoTimeoutsDue()
	{
		// Arrange
		var now = DateTimeOffset.UtcNow;
		await _sut.ScheduleTimeoutAsync(CreateTimeout("saga-1", "t1", now.AddMinutes(5)), CancellationToken.None);

		// Act
		var due = await _sut.GetDueTimeoutsAsync(now, CancellationToken.None);

		// Assert
		due.ShouldBeEmpty();
	}

	[Fact]
	public async Task MarkDeliveredAsync_RemoveTimeout()
	{
		// Arrange
		await _sut.ScheduleTimeoutAsync(CreateTimeout("saga-1", "t1", DateTimeOffset.UtcNow), CancellationToken.None);

		// Act
		await _sut.MarkDeliveredAsync("t1", CancellationToken.None);

		// Assert
		_sut.GetPendingCount().ShouldBe(0);
	}

	[Fact]
	public async Task MarkDeliveredAsync_BeIdempotent()
	{
		// Arrange
		await _sut.ScheduleTimeoutAsync(CreateTimeout("saga-1", "t1", DateTimeOffset.UtcNow), CancellationToken.None);
		await _sut.MarkDeliveredAsync("t1", CancellationToken.None);

		// Act & Assert - second call should not throw
		await _sut.MarkDeliveredAsync("t1", CancellationToken.None);
		_sut.GetPendingCount().ShouldBe(0);
	}

	[Fact]
	public async Task MarkDeliveredAsync_ThrowOnNullTimeoutId()
	{
		await Should.ThrowAsync<ArgumentException>(
			() => _sut.MarkDeliveredAsync(null!, CancellationToken.None));
	}

	[Fact]
	public async Task Clear_RemoveAllTimeouts()
	{
		// Arrange
		await _sut.ScheduleTimeoutAsync(CreateTimeout("saga-1", "t1", DateTimeOffset.UtcNow), CancellationToken.None);
		await _sut.ScheduleTimeoutAsync(CreateTimeout("saga-2", "t2", DateTimeOffset.UtcNow), CancellationToken.None);

		// Act
		_sut.Clear();

		// Assert
		_sut.GetPendingCount().ShouldBe(0);
	}

	[Fact]
	public void GetPendingCount_ReturnZero_WhenEmpty()
	{
		_sut.GetPendingCount().ShouldBe(0);
	}

	/// <summary>
	/// Creates a SagaTimeout using the positional record constructor:
	/// SagaTimeout(TimeoutId, SagaId, SagaType, TimeoutType, TimeoutData, DueAt, ScheduledAt)
	/// </summary>
	private static SagaTimeout CreateTimeout(string sagaId, string timeoutId, DateTimeOffset dueAt) =>
		new(timeoutId, sagaId, "TestSaga", "test-timeout", null, dueAt, DateTimeOffset.UtcNow);
}
