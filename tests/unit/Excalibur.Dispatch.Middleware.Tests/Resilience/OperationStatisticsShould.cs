// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Resilience.Polly;

namespace Excalibur.Dispatch.Middleware.Tests.Resilience;

/// <summary>
/// Unit tests for <see cref="OperationStatistics"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Resilience")]
public sealed class OperationStatisticsShould : UnitTestBase
{
	[Fact]
	public void DefaultValues_AreCorrect()
	{
		// Act
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
		stats.RecordFallback();

		// Assert
		stats.FallbackExecutions.ShouldBe(3);
	}

	[Fact]
	public void Clone_CreatesDeepCopy()
	{
		// Arrange
		var original = new OperationStatistics();
		for (var i = 0; i < 100; i++) original.RecordAttempt();
		for (var i = 0; i < 80; i++) original.RecordSuccess();
		for (var i = 0; i < 15; i++) original.RecordFailure();
		for (var i = 0; i < 5; i++) original.RecordFallback();

		// Act
		var clone = original.Clone();

		// Assert
		clone.ShouldNotBeSameAs(original);
		clone.TotalAttempts.ShouldBe(original.TotalAttempts);
		clone.Successes.ShouldBe(original.Successes);
		clone.Failures.ShouldBe(original.Failures);
		clone.FallbackExecutions.ShouldBe(original.FallbackExecutions);
	}

	[Fact]
	public void Clone_IsIndependentOfOriginal()
	{
		// Arrange
		var original = new OperationStatistics();
		for (var i = 0; i < 100; i++) original.RecordAttempt();
		for (var i = 0; i < 80; i++) original.RecordSuccess();

		// Act
		var clone = original.Clone();
		original.RecordAttempt(); // 101
		original.RecordSuccess(); // 81

		// Assert - clone should not be affected
		clone.TotalAttempts.ShouldBe(100);
		clone.Successes.ShouldBe(80);
	}

	[Fact]
	public void AllRecordMethods_TrackCorrectly()
	{
		// Arrange
		var stats = new OperationStatistics();

		// Act
		for (var i = 0; i < 1000; i++) stats.RecordAttempt();
		for (var i = 0; i < 900; i++) stats.RecordSuccess();
		for (var i = 0; i < 75; i++) stats.RecordFailure();
		for (var i = 0; i < 25; i++) stats.RecordFallback();

		// Assert
		stats.TotalAttempts.ShouldBe(1000);
		stats.Successes.ShouldBe(900);
		stats.Failures.ShouldBe(75);
		stats.FallbackExecutions.ShouldBe(25);
	}
}
