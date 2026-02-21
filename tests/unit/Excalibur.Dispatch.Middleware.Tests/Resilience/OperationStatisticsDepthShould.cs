// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Resilience.Polly;

namespace Excalibur.Dispatch.Middleware.Tests.Resilience;

/// <summary>
/// Depth coverage tests for <see cref="OperationStatistics"/> including thread-safety and edge cases.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Resilience")]
public sealed class OperationStatisticsDepthShould
{
	[Fact]
	public void MaintainConsistencyUnderConcurrentRecordAttempts()
	{
		// Arrange
		var stats = new OperationStatistics();
		const int threadCount = 10;
		const int opsPerThread = 1000;

		// Act
		Parallel.For(0, threadCount, _ =>
		{
			for (var i = 0; i < opsPerThread; i++)
			{
				stats.RecordAttempt();
			}
		});

		// Assert
		stats.TotalAttempts.ShouldBe(threadCount * opsPerThread);
	}

	[Fact]
	public void MaintainConsistencyUnderConcurrentRecordSuccess()
	{
		// Arrange
		var stats = new OperationStatistics();
		const int threadCount = 10;
		const int opsPerThread = 1000;

		// Act
		Parallel.For(0, threadCount, _ =>
		{
			for (var i = 0; i < opsPerThread; i++)
			{
				stats.RecordSuccess();
			}
		});

		// Assert
		stats.Successes.ShouldBe(threadCount * opsPerThread);
	}

	[Fact]
	public void MaintainConsistencyUnderConcurrentRecordFailure()
	{
		// Arrange
		var stats = new OperationStatistics();
		const int threadCount = 10;
		const int opsPerThread = 1000;

		// Act
		Parallel.For(0, threadCount, _ =>
		{
			for (var i = 0; i < opsPerThread; i++)
			{
				stats.RecordFailure();
			}
		});

		// Assert
		stats.Failures.ShouldBe(threadCount * opsPerThread);
	}

	[Fact]
	public void MaintainConsistencyUnderConcurrentRecordFallback()
	{
		// Arrange
		var stats = new OperationStatistics();
		const int threadCount = 10;
		const int opsPerThread = 1000;

		// Act
		Parallel.For(0, threadCount, _ =>
		{
			for (var i = 0; i < opsPerThread; i++)
			{
				stats.RecordFallback();
			}
		});

		// Assert
		stats.FallbackExecutions.ShouldBe(threadCount * opsPerThread);
	}

	[Fact]
	public async Task MaintainConsistencyUnderMixedConcurrentOperations()
	{
		// Arrange
		var stats = new OperationStatistics();
		const int opsPerType = 500;

		// Act - mix all operation types concurrently
		var tasks = new List<Task>
		{
			Task.Run(() => { for (var i = 0; i < opsPerType; i++) stats.RecordAttempt(); }),
			Task.Run(() => { for (var i = 0; i < opsPerType; i++) stats.RecordSuccess(); }),
			Task.Run(() => { for (var i = 0; i < opsPerType; i++) stats.RecordFailure(); }),
			Task.Run(() => { for (var i = 0; i < opsPerType; i++) stats.RecordFallback(); }),
		};

		await Task.WhenAll(tasks);

		// Assert
		stats.TotalAttempts.ShouldBe(opsPerType);
		stats.Successes.ShouldBe(opsPerType);
		stats.Failures.ShouldBe(opsPerType);
		stats.FallbackExecutions.ShouldBe(opsPerType);
	}

	[Fact]
	public void CloneSnapshotIsConsistentAtPointInTime()
	{
		// Arrange
		var stats = new OperationStatistics();
		stats.RecordAttempt();
		stats.RecordAttempt();
		stats.RecordSuccess();
		stats.RecordFailure();
		stats.RecordFallback();

		// Act
		var clone = stats.Clone();

		// Then mutate original
		stats.RecordAttempt();
		stats.RecordSuccess();

		// Assert - clone is a snapshot, unaffected by later mutations
		clone.TotalAttempts.ShouldBe(2);
		clone.Successes.ShouldBe(1);
		clone.Failures.ShouldBe(1);
		clone.FallbackExecutions.ShouldBe(1);

		// Original continues to track
		stats.TotalAttempts.ShouldBe(3);
		stats.Successes.ShouldBe(2);
	}

	[Fact]
	public void CloneOfFreshInstanceIsZero()
	{
		// Arrange
		var stats = new OperationStatistics();

		// Act
		var clone = stats.Clone();

		// Assert
		clone.TotalAttempts.ShouldBe(0);
		clone.Successes.ShouldBe(0);
		clone.Failures.ShouldBe(0);
		clone.FallbackExecutions.ShouldBe(0);
	}

	[Fact]
	public void RecordMultipleAttemptsThenClone()
	{
		// Arrange
		var stats = new OperationStatistics();
		for (var i = 0; i < 50; i++) stats.RecordAttempt();
		for (var i = 0; i < 30; i++) stats.RecordSuccess();
		for (var i = 0; i < 10; i++) stats.RecordFailure();
		for (var i = 0; i < 10; i++) stats.RecordFallback();

		// Act
		var clone = stats.Clone();

		// Assert
		clone.TotalAttempts.ShouldBe(50);
		clone.Successes.ShouldBe(30);
		clone.Failures.ShouldBe(10);
		clone.FallbackExecutions.ShouldBe(10);
	}

	[Fact]
	public void InitialStateReadableWithoutException()
	{
		// Arrange
		var stats = new OperationStatistics();

		// Act & Assert - all reads should work on fresh instance without errors
		stats.TotalAttempts.ShouldBe(0L);
		stats.Successes.ShouldBe(0L);
		stats.Failures.ShouldBe(0L);
		stats.FallbackExecutions.ShouldBe(0L);
	}

	[Fact]
	public void IndependentCounterTracking()
	{
		// Arrange
		var stats = new OperationStatistics();

		// Act - only record specific counters
		stats.RecordAttempt();
		stats.RecordFailure();

		// Assert - only those counters should have values
		stats.TotalAttempts.ShouldBe(1);
		stats.Successes.ShouldBe(0);
		stats.Failures.ShouldBe(1);
		stats.FallbackExecutions.ShouldBe(0);
	}
}
