// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Resilience.Polly;

namespace Excalibur.Dispatch.Tests.Resilience;

/// <summary>
/// Unit tests for <see cref="OperationStatistics"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Resilience")]
public sealed class OperationStatisticsShould : UnitTestBase
{
	[Fact]
	public void InitializeWithZeroValues()
	{
		// Arrange & Act
		var stats = new OperationStatistics();

		// Assert
		stats.TotalAttempts.ShouldBe(0);
		stats.Successes.ShouldBe(0);
		stats.Failures.ShouldBe(0);
		stats.FallbackExecutions.ShouldBe(0);
	}

	[Fact]
	public void RecordAttempt_IncrementsTotalAttempts()
	{
		// Arrange
		var stats = new OperationStatistics();

		// Act
		stats.RecordAttempt();
		stats.RecordAttempt();
		stats.RecordAttempt();

		// Assert
		stats.TotalAttempts.ShouldBe(3);
	}

	[Fact]
	public void RecordSuccess_IncrementsSuccesses()
	{
		// Arrange
		var stats = new OperationStatistics();

		// Act
		stats.RecordSuccess();
		stats.RecordSuccess();

		// Assert
		stats.Successes.ShouldBe(2);
	}

	[Fact]
	public void RecordFailure_IncrementsFailures()
	{
		// Arrange
		var stats = new OperationStatistics();

		// Act
		stats.RecordFailure();

		// Assert
		stats.Failures.ShouldBe(1);
	}

	[Fact]
	public void RecordFallback_IncrementsFallbackExecutions()
	{
		// Arrange
		var stats = new OperationStatistics();

		// Act
		stats.RecordFallback();
		stats.RecordFallback();

		// Assert
		stats.FallbackExecutions.ShouldBe(2);
	}

	[Fact]
	public void Clone_CreatesIndependentCopy()
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

		// Assert - Clone should have same values
		clone.TotalAttempts.ShouldBe(2);
		clone.Successes.ShouldBe(1);
		clone.Failures.ShouldBe(1);
		clone.FallbackExecutions.ShouldBe(1);

		// Mutating original should NOT affect clone
		stats.RecordAttempt();
		clone.TotalAttempts.ShouldBe(2);
	}

	[Fact]
	public void MixedRecords_MaintainCorrectCounts()
	{
		// Arrange
		var stats = new OperationStatistics();

		// Act
		stats.RecordAttempt();
		stats.RecordSuccess();
		stats.RecordAttempt();
		stats.RecordFailure();
		stats.RecordFallback();
		stats.RecordAttempt();
		stats.RecordSuccess();

		// Assert
		stats.TotalAttempts.ShouldBe(3);
		stats.Successes.ShouldBe(2);
		stats.Failures.ShouldBe(1);
		stats.FallbackExecutions.ShouldBe(1);
	}

	[Fact]
	public void ThreadSafety_ConcurrentRecords_MaintainAccuracy()
	{
		// Arrange
		var stats = new OperationStatistics();
		const int iterations = 1000;

		// Act - concurrent increments
		Parallel.For(0, iterations, _ =>
		{
			stats.RecordAttempt();
			stats.RecordSuccess();
		});

		// Assert
		stats.TotalAttempts.ShouldBe(iterations);
		stats.Successes.ShouldBe(iterations);
	}
}
