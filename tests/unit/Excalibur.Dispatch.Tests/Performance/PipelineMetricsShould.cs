// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Performance;

namespace Excalibur.Dispatch.Tests.Performance;

/// <summary>
/// Unit tests for <see cref="PipelineMetrics"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Performance")]
[Trait("Priority", "0")]
public sealed class PipelineMetricsShould
{
	#region Object Initializer Tests

	[Fact]
	public void Construct_WithRequiredProperties_SetsAllProperties()
	{
		// Arrange
		var totalExecutions = 1000;
		var totalDuration = TimeSpan.FromSeconds(10);
		var averageDuration = TimeSpan.FromMilliseconds(10);
		var averageMiddlewareCount = 5.0;
		var totalMemoryAllocated = 1024L * 1024L; // 1 MB
		var averageMemoryPerExecution = 1024L;

		// Act
		var metrics = new PipelineMetrics
		{
			TotalExecutions = totalExecutions,
			TotalDuration = totalDuration,
			AverageDuration = averageDuration,
			AverageMiddlewareCount = averageMiddlewareCount,
			TotalMemoryAllocated = totalMemoryAllocated,
			AverageMemoryPerExecution = averageMemoryPerExecution,
		};

		// Assert
		metrics.TotalExecutions.ShouldBe(totalExecutions);
		metrics.TotalDuration.ShouldBe(totalDuration);
		metrics.AverageDuration.ShouldBe(averageDuration);
		metrics.AverageMiddlewareCount.ShouldBe(averageMiddlewareCount);
		metrics.TotalMemoryAllocated.ShouldBe(totalMemoryAllocated);
		metrics.AverageMemoryPerExecution.ShouldBe(averageMemoryPerExecution);
	}

	[Fact]
	public void Construct_WithZeroValues_SetsAllPropertiesToZero()
	{
		// Act
		var metrics = new PipelineMetrics
		{
			TotalExecutions = 0,
			TotalDuration = TimeSpan.Zero,
			AverageDuration = TimeSpan.Zero,
			AverageMiddlewareCount = 0.0,
			TotalMemoryAllocated = 0L,
			AverageMemoryPerExecution = 0L,
		};

		// Assert
		metrics.TotalExecutions.ShouldBe(0);
		metrics.TotalDuration.ShouldBe(TimeSpan.Zero);
		metrics.AverageDuration.ShouldBe(TimeSpan.Zero);
		metrics.AverageMiddlewareCount.ShouldBe(0.0);
		metrics.TotalMemoryAllocated.ShouldBe(0L);
		metrics.AverageMemoryPerExecution.ShouldBe(0L);
	}

	#endregion

	#region Equality Tests

	[Fact]
	public void Equals_WithSameValues_ReturnsTrue()
	{
		// Arrange
		var metrics1 = CreateStandardMetrics();
		var metrics2 = CreateStandardMetrics();

		// Act & Assert
		metrics1.ShouldBe(metrics2);
		(metrics1 == metrics2).ShouldBeTrue();
	}

	[Fact]
	public void Equals_WithDifferentTotalExecutions_ReturnsFalse()
	{
		// Arrange
		var metrics1 = CreateStandardMetrics();
		var metrics2 = metrics1 with { TotalExecutions = 5000 };

		// Act & Assert
		metrics1.ShouldNotBe(metrics2);
	}

	[Fact]
	public void Equals_WithDifferentTotalMemoryAllocated_ReturnsFalse()
	{
		// Arrange
		var metrics1 = CreateStandardMetrics();
		var metrics2 = metrics1 with { TotalMemoryAllocated = 2048L * 1024L };

		// Act & Assert
		metrics1.ShouldNotBe(metrics2);
	}

	[Fact]
	public void GetHashCode_WithSameValues_ReturnsSameHashCode()
	{
		// Arrange
		var metrics1 = CreateStandardMetrics();
		var metrics2 = CreateStandardMetrics();

		// Act & Assert
		metrics1.GetHashCode().ShouldBe(metrics2.GetHashCode());
	}

	#endregion

	#region With Expression Tests

	[Fact]
	public void WithExpression_CreatesCopyWithModifiedProperty()
	{
		// Arrange
		var original = CreateStandardMetrics();

		// Act
		var modified = original with { TotalExecutions = 5000 };

		// Assert
		modified.TotalExecutions.ShouldBe(5000);
		modified.TotalDuration.ShouldBe(original.TotalDuration);
		modified.AverageMiddlewareCount.ShouldBe(original.AverageMiddlewareCount);
	}

	[Fact]
	public void WithExpression_PreservesUnmodifiedProperties()
	{
		// Arrange
		var original = CreateStandardMetrics();

		// Act
		var modified = original with { AverageMiddlewareCount = 8.0 };

		// Assert
		modified.AverageMiddlewareCount.ShouldBe(8.0);
		modified.TotalExecutions.ShouldBe(original.TotalExecutions);
		modified.TotalDuration.ShouldBe(original.TotalDuration);
		modified.AverageDuration.ShouldBe(original.AverageDuration);
		modified.TotalMemoryAllocated.ShouldBe(original.TotalMemoryAllocated);
		modified.AverageMemoryPerExecution.ShouldBe(original.AverageMemoryPerExecution);
	}

	#endregion

	#region Real-World Scenario Tests

	[Fact]
	public void Metrics_ForHighThroughputScenario_ReflectsLowAverageDuration()
	{
		// Act
		var metrics = new PipelineMetrics
		{
			TotalExecutions = 100000,
			TotalDuration = TimeSpan.FromSeconds(100),
			AverageDuration = TimeSpan.FromMilliseconds(1),
			AverageMiddlewareCount = 3.0,
			TotalMemoryAllocated = 100L * 1024L * 1024L, // 100 MB
			AverageMemoryPerExecution = 1024L,
		};

		// Assert
		metrics.AverageDuration.TotalMilliseconds.ShouldBeLessThanOrEqualTo(1);
		metrics.TotalExecutions.ShouldBe(100000);
	}

	[Fact]
	public void Metrics_ForComplexPipeline_HasHighMiddlewareCount()
	{
		// Act
		var metrics = new PipelineMetrics
		{
			TotalExecutions = 1000,
			TotalDuration = TimeSpan.FromSeconds(50),
			AverageDuration = TimeSpan.FromMilliseconds(50),
			AverageMiddlewareCount = 12.0,
			TotalMemoryAllocated = 10L * 1024L * 1024L, // 10 MB
			AverageMemoryPerExecution = 10240L,
		};

		// Assert
		metrics.AverageMiddlewareCount.ShouldBeGreaterThan(10.0);
	}

	[Fact]
	public void Metrics_ForZeroAllocationPipeline_HasLowMemoryUsage()
	{
		// Act
		var metrics = new PipelineMetrics
		{
			TotalExecutions = 10000,
			TotalDuration = TimeSpan.FromSeconds(5),
			AverageDuration = TimeSpan.FromMicroseconds(500),
			AverageMiddlewareCount = 5.0,
			TotalMemoryAllocated = 0L,
			AverageMemoryPerExecution = 0L,
		};

		// Assert
		metrics.TotalMemoryAllocated.ShouldBe(0L);
		metrics.AverageMemoryPerExecution.ShouldBe(0L);
	}

	[Fact]
	public void Metrics_ForLongRunningPipeline_ReflectsHighDuration()
	{
		// Act
		var metrics = new PipelineMetrics
		{
			TotalExecutions = 10,
			TotalDuration = TimeSpan.FromMinutes(5),
			AverageDuration = TimeSpan.FromSeconds(30),
			AverageMiddlewareCount = 2.0,
			TotalMemoryAllocated = 50L * 1024L * 1024L, // 50 MB
			AverageMemoryPerExecution = 5L * 1024L * 1024L, // 5 MB
		};

		// Assert
		metrics.AverageDuration.TotalSeconds.ShouldBeGreaterThan(10);
	}

	#endregion

	#region Helper Methods

	private static PipelineMetrics CreateStandardMetrics() =>
		new()
		{
			TotalExecutions = 1000,
			TotalDuration = TimeSpan.FromSeconds(10),
			AverageDuration = TimeSpan.FromMilliseconds(10),
			AverageMiddlewareCount = 5.0,
			TotalMemoryAllocated = 1024L * 1024L,
			AverageMemoryPerExecution = 1024L,
		};

	#endregion
}
