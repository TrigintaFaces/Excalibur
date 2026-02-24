// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Outbox.Health;

namespace Excalibur.Outbox.Tests.Health;

/// <summary>
/// Unit tests for <see cref="BackgroundServiceHealthState"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Outbox")]
public sealed class BackgroundServiceHealthStateShould : UnitTestBase
{
	#region Initial State Tests

	[Fact]
	public void HaveZeroTotalProcessedByDefault()
	{
		// Arrange & Act
		var state = new BackgroundServiceHealthState();

		// Assert
		state.TotalProcessed.ShouldBe(0);
	}

	[Fact]
	public void HaveZeroTotalFailedByDefault()
	{
		// Arrange & Act
		var state = new BackgroundServiceHealthState();

		// Assert
		state.TotalFailed.ShouldBe(0);
	}

	[Fact]
	public void HaveZeroTotalCyclesByDefault()
	{
		// Arrange & Act
		var state = new BackgroundServiceHealthState();

		// Assert
		state.TotalCycles.ShouldBe(0);
	}

	[Fact]
	public void HaveNullLastActivityTimeByDefault()
	{
		// Arrange & Act
		var state = new BackgroundServiceHealthState();

		// Assert
		state.LastActivityTime.ShouldBeNull();
	}

	[Fact]
	public void NotBeRunningByDefault()
	{
		// Arrange & Act
		var state = new BackgroundServiceHealthState();

		// Assert
		state.IsRunning.ShouldBeFalse();
	}

	#endregion Initial State Tests

	#region MarkStarted Tests

	[Fact]
	public void SetIsRunningTrueWhenStarted()
	{
		// Arrange
		var state = new BackgroundServiceHealthState();

		// Act
		state.MarkStarted();

		// Assert
		state.IsRunning.ShouldBeTrue();
	}

	[Fact]
	public void SetLastActivityTimeWhenStarted()
	{
		// Arrange
		var state = new BackgroundServiceHealthState();
		var beforeStart = DateTimeOffset.UtcNow;

		// Act
		state.MarkStarted();

		var afterStart = DateTimeOffset.UtcNow;

		// Assert
		state.LastActivityTime.ShouldNotBeNull();
		state.LastActivityTime.Value.ShouldBeGreaterThanOrEqualTo(beforeStart);
		state.LastActivityTime.Value.ShouldBeLessThanOrEqualTo(afterStart);
	}

	[Fact]
	public void AllowMultipleStartCalls()
	{
		// Arrange
		var state = new BackgroundServiceHealthState();

		// Act
		state.MarkStarted();
		state.MarkStarted();
		state.MarkStarted();

		// Assert
		state.IsRunning.ShouldBeTrue();
	}

	#endregion MarkStarted Tests

	#region MarkStopped Tests

	[Fact]
	public void SetIsRunningFalseWhenStopped()
	{
		// Arrange
		var state = new BackgroundServiceHealthState();
		state.MarkStarted();

		// Act
		state.MarkStopped();

		// Assert
		state.IsRunning.ShouldBeFalse();
	}

	[Fact]
	public void AllowStopWithoutStart()
	{
		// Arrange
		var state = new BackgroundServiceHealthState();

		// Act
		state.MarkStopped();

		// Assert
		state.IsRunning.ShouldBeFalse();
	}

	[Fact]
	public void AllowMultipleStopCalls()
	{
		// Arrange
		var state = new BackgroundServiceHealthState();
		state.MarkStarted();

		// Act
		state.MarkStopped();
		state.MarkStopped();
		state.MarkStopped();

		// Assert
		state.IsRunning.ShouldBeFalse();
	}

	[Fact]
	public void AllowRestartAfterStop()
	{
		// Arrange
		var state = new BackgroundServiceHealthState();

		// Act
		state.MarkStarted();
		state.MarkStopped();
		state.MarkStarted();

		// Assert
		state.IsRunning.ShouldBeTrue();
	}

	#endregion MarkStopped Tests

	#region RecordCycle Tests

	[Fact]
	public void IncrementTotalProcessedOnRecordCycle()
	{
		// Arrange
		var state = new BackgroundServiceHealthState();

		// Act
		state.RecordCycle(processedCount: 10, failedCount: 0);

		// Assert
		state.TotalProcessed.ShouldBe(10);
	}

	[Fact]
	public void IncrementTotalFailedOnRecordCycle()
	{
		// Arrange
		var state = new BackgroundServiceHealthState();

		// Act
		state.RecordCycle(processedCount: 0, failedCount: 5);

		// Assert
		state.TotalFailed.ShouldBe(5);
	}

	[Fact]
	public void IncrementTotalCyclesOnRecordCycle()
	{
		// Arrange
		var state = new BackgroundServiceHealthState();

		// Act
		state.RecordCycle(processedCount: 0, failedCount: 0);

		// Assert
		state.TotalCycles.ShouldBe(1);
	}

	[Fact]
	public void UpdateLastActivityTimeOnRecordCycle()
	{
		// Arrange
		var state = new BackgroundServiceHealthState();
		var beforeRecord = DateTimeOffset.UtcNow;

		// Act
		state.RecordCycle(processedCount: 1, failedCount: 0);

		var afterRecord = DateTimeOffset.UtcNow;

		// Assert
		state.LastActivityTime.ShouldNotBeNull();
		state.LastActivityTime.Value.ShouldBeGreaterThanOrEqualTo(beforeRecord);
		state.LastActivityTime.Value.ShouldBeLessThanOrEqualTo(afterRecord);
	}

	[Fact]
	public void AccumulateProcessedCountsAcrossCycles()
	{
		// Arrange
		var state = new BackgroundServiceHealthState();

		// Act
		state.RecordCycle(processedCount: 10, failedCount: 0);
		state.RecordCycle(processedCount: 20, failedCount: 0);
		state.RecordCycle(processedCount: 30, failedCount: 0);

		// Assert
		state.TotalProcessed.ShouldBe(60);
		state.TotalCycles.ShouldBe(3);
	}

	[Fact]
	public void AccumulateFailedCountsAcrossCycles()
	{
		// Arrange
		var state = new BackgroundServiceHealthState();

		// Act
		state.RecordCycle(processedCount: 0, failedCount: 1);
		state.RecordCycle(processedCount: 0, failedCount: 2);
		state.RecordCycle(processedCount: 0, failedCount: 3);

		// Assert
		state.TotalFailed.ShouldBe(6);
		state.TotalCycles.ShouldBe(3);
	}

	[Fact]
	public void HandleMixedProcessedAndFailedCounts()
	{
		// Arrange
		var state = new BackgroundServiceHealthState();

		// Act
		state.RecordCycle(processedCount: 100, failedCount: 5);
		state.RecordCycle(processedCount: 200, failedCount: 10);

		// Assert
		state.TotalProcessed.ShouldBe(300);
		state.TotalFailed.ShouldBe(15);
		state.TotalCycles.ShouldBe(2);
	}

	[Fact]
	public void NotIncrementProcessedForZeroCount()
	{
		// Arrange
		var state = new BackgroundServiceHealthState();

		// Act
		state.RecordCycle(processedCount: 0, failedCount: 5);

		// Assert
		state.TotalProcessed.ShouldBe(0);
		state.TotalFailed.ShouldBe(5);
	}

	[Fact]
	public void NotIncrementFailedForZeroCount()
	{
		// Arrange
		var state = new BackgroundServiceHealthState();

		// Act
		state.RecordCycle(processedCount: 10, failedCount: 0);

		// Assert
		state.TotalProcessed.ShouldBe(10);
		state.TotalFailed.ShouldBe(0);
	}

	[Fact]
	public void IncrementCycleEvenWithZeroCounts()
	{
		// Arrange
		var state = new BackgroundServiceHealthState();

		// Act
		state.RecordCycle(processedCount: 0, failedCount: 0);

		// Assert
		state.TotalProcessed.ShouldBe(0);
		state.TotalFailed.ShouldBe(0);
		state.TotalCycles.ShouldBe(1);
	}

	#endregion RecordCycle Tests

	#region Thread Safety Scenario Tests

	[Fact]
	public void MaintainConsistentStateAcrossOperations()
	{
		// Arrange
		var state = new BackgroundServiceHealthState();

		// Act - Simulate a typical service lifecycle
		state.MarkStarted();
		state.RecordCycle(processedCount: 100, failedCount: 2);
		state.RecordCycle(processedCount: 150, failedCount: 3);
		state.RecordCycle(processedCount: 50, failedCount: 1);
		state.MarkStopped();

		// Assert
		state.IsRunning.ShouldBeFalse();
		state.TotalProcessed.ShouldBe(300);
		state.TotalFailed.ShouldBe(6);
		state.TotalCycles.ShouldBe(3);
		state.LastActivityTime.ShouldNotBeNull();
	}

	[Fact]
	public void TrackLargeProcessedCounts()
	{
		// Arrange
		var state = new BackgroundServiceHealthState();

		// Act
		state.RecordCycle(processedCount: 1_000_000, failedCount: 0);
		state.RecordCycle(processedCount: 2_000_000, failedCount: 0);

		// Assert
		state.TotalProcessed.ShouldBe(3_000_000);
	}

	[Fact]
	public void TrackLargeFailedCounts()
	{
		// Arrange
		var state = new BackgroundServiceHealthState();

		// Act
		state.RecordCycle(processedCount: 0, failedCount: 500_000);
		state.RecordCycle(processedCount: 0, failedCount: 500_000);

		// Assert
		state.TotalFailed.ShouldBe(1_000_000);
	}

	[Fact]
	public void TrackManyCycles()
	{
		// Arrange
		var state = new BackgroundServiceHealthState();

		// Act
		for (var i = 0; i < 1000; i++)
		{
			state.RecordCycle(processedCount: 1, failedCount: 0);
		}

		// Assert
		state.TotalCycles.ShouldBe(1000);
		state.TotalProcessed.ShouldBe(1000);
	}

	#endregion Thread Safety Scenario Tests

	#region LastActivityTime Tests

	[Fact]
	public void UpdateLastActivityTimeOnEachCycle()
	{
		// Arrange
		var state = new BackgroundServiceHealthState();

		// Act
		state.RecordCycle(processedCount: 1, failedCount: 0);
		var firstActivityTime = state.LastActivityTime;

		// Small delay to ensure time progresses
		global::Tests.Shared.Infrastructure.TestTiming.Sleep(1);

		state.RecordCycle(processedCount: 1, failedCount: 0);
		var secondActivityTime = state.LastActivityTime;

		// Assert
		firstActivityTime.ShouldNotBeNull();
		secondActivityTime.ShouldNotBeNull();
		secondActivityTime.Value.ShouldBeGreaterThanOrEqualTo(firstActivityTime.Value);
	}

	[Fact]
	public void PreserveLastActivityTimeAfterStop()
	{
		// Arrange
		var state = new BackgroundServiceHealthState();
		state.MarkStarted();
		state.RecordCycle(processedCount: 1, failedCount: 0);
		var activityTimeBeforeStop = state.LastActivityTime;

		// Act
		state.MarkStopped();

		// Assert
		state.LastActivityTime.ShouldBe(activityTimeBeforeStop);
	}

	#endregion LastActivityTime Tests
}
