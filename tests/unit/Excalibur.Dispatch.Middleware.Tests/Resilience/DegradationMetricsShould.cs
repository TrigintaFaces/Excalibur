// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Resilience.Polly;

namespace Excalibur.Dispatch.Middleware.Tests.Resilience;

/// <summary>
/// Unit tests for <see cref="DegradationMetrics"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Resilience")]
public sealed class DegradationMetricsShould : UnitTestBase
{
	[Fact]
	public void DefaultValues_AreCorrect()
	{
		// Act
		var metrics = new DegradationMetrics();

		// Assert
		metrics.CurrentLevel.ShouldBe(default);
		metrics.LastLevelChange.ShouldBe(default);
		metrics.LastChangeReason.ShouldBe(string.Empty);
		metrics.OperationStatistics.ShouldNotBeNull();
		metrics.OperationStatistics.ShouldBeEmpty();
		metrics.HealthMetrics.ShouldNotBeNull();
		metrics.TotalOperations.ShouldBe(0);
		metrics.TotalFallbacks.ShouldBe(0);
		metrics.SuccessRate.ShouldBe(0);
	}

	[Fact]
	public void CurrentLevel_CanBeSet()
	{
		// Arrange & Act
		var metrics = new DegradationMetrics { CurrentLevel = DegradationLevel.Moderate };

		// Assert
		metrics.CurrentLevel.ShouldBe(DegradationLevel.Moderate);
	}

	[Fact]
	public void LastLevelChange_CanBeSet()
	{
		// Arrange
		var timestamp = DateTime.UtcNow;

		// Act
		var metrics = new DegradationMetrics { LastLevelChange = timestamp };

		// Assert
		metrics.LastLevelChange.ShouldBe(timestamp);
	}

	[Fact]
	public void LastChangeReason_CanBeSet()
	{
		// Arrange & Act
		var metrics = new DegradationMetrics { LastChangeReason = "High CPU usage" };

		// Assert
		metrics.LastChangeReason.ShouldBe("High CPU usage");
	}

	[Fact]
	public void OperationStatistics_CanBeSet()
	{
		// Arrange
		var stats = new Dictionary<string, OperationStatistics>(StringComparer.Ordinal)
		{
			["op1"] = CreateStats(100, 90),
			["op2"] = CreateStats(50, 45)
		};

		// Act
		var metrics = new DegradationMetrics { OperationStatistics = stats };

		// Assert
		metrics.OperationStatistics.Count.ShouldBe(2);
		metrics.OperationStatistics.ShouldContainKey("op1");
		metrics.OperationStatistics.ShouldContainKey("op2");
	}

	[Fact]
	public void HealthMetrics_CanBeSet()
	{
		// Arrange
		var health = new HealthMetrics
		{
			CpuUsagePercent = 75.0,
			MemoryUsagePercent = 80.0
		};

		// Act
		var metrics = new DegradationMetrics { HealthMetrics = health };

		// Assert
		metrics.HealthMetrics.CpuUsagePercent.ShouldBe(75.0);
		metrics.HealthMetrics.MemoryUsagePercent.ShouldBe(80.0);
	}

	[Fact]
	public void TotalOperations_CanBeSet()
	{
		// Arrange & Act
		var metrics = new DegradationMetrics { TotalOperations = 1000 };

		// Assert
		metrics.TotalOperations.ShouldBe(1000);
	}

	[Fact]
	public void TotalFallbacks_CanBeSet()
	{
		// Arrange & Act
		var metrics = new DegradationMetrics { TotalFallbacks = 50 };

		// Assert
		metrics.TotalFallbacks.ShouldBe(50);
	}

	[Fact]
	public void SuccessRate_CanBeSet()
	{
		// Arrange & Act
		var metrics = new DegradationMetrics { SuccessRate = 0.95 };

		// Assert
		metrics.SuccessRate.ShouldBe(0.95);
	}

	[Fact]
	public void AllProperties_CanBeSetTogether()
	{
		// Arrange
		var timestamp = DateTime.UtcNow;
		var stats = new Dictionary<string, OperationStatistics>(StringComparer.Ordinal)
		{
			["test-op"] = CreateStats(100, 0)
		};
		var health = new HealthMetrics { CpuUsagePercent = 50.0 };

		// Act
		var metrics = new DegradationMetrics
		{
			CurrentLevel = DegradationLevel.Minor,
			LastLevelChange = timestamp,
			LastChangeReason = "Auto-adjusted",
			OperationStatistics = stats,
			HealthMetrics = health,
			TotalOperations = 500,
			TotalFallbacks = 10,
			SuccessRate = 0.98
		};

		// Assert
		metrics.CurrentLevel.ShouldBe(DegradationLevel.Minor);
		metrics.LastLevelChange.ShouldBe(timestamp);
		metrics.LastChangeReason.ShouldBe("Auto-adjusted");
		metrics.OperationStatistics.Count.ShouldBe(1);
		metrics.HealthMetrics.CpuUsagePercent.ShouldBe(50.0);
		metrics.TotalOperations.ShouldBe(500);
		metrics.TotalFallbacks.ShouldBe(10);
		metrics.SuccessRate.ShouldBe(0.98);
	}

	private static OperationStatistics CreateStats(int attempts, int successes)
	{
		var stats = new OperationStatistics();
		for (var i = 0; i < attempts; i++) stats.RecordAttempt();
		for (var i = 0; i < successes; i++) stats.RecordSuccess();
		return stats;
	}
}
