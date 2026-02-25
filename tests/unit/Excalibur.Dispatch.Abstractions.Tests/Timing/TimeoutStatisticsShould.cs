// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;

namespace Excalibur.Dispatch.Abstractions.Tests.Timing;

/// <summary>
/// Unit tests for <see cref="TimeoutStatistics"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Timing")]
[Trait("Priority", "0")]
public sealed class TimeoutStatisticsShould
{
	#region Property Tests

	[Fact]
	public void OperationType_CanBeSet()
	{
		// Act
		var stats = new TimeoutStatistics { OperationType = TimeoutOperationType.Handler };

		// Assert
		stats.OperationType.ShouldBe(TimeoutOperationType.Handler);
	}

	[Fact]
	public void TotalOperations_CanBeSet()
	{
		// Act
		var stats = new TimeoutStatistics { TotalOperations = 1000 };

		// Assert
		stats.TotalOperations.ShouldBe(1000);
	}

	[Fact]
	public void SuccessfulOperations_CanBeSet()
	{
		// Act
		var stats = new TimeoutStatistics { SuccessfulOperations = 950 };

		// Assert
		stats.SuccessfulOperations.ShouldBe(950);
	}

	[Fact]
	public void TimedOutOperations_CanBeSet()
	{
		// Act
		var stats = new TimeoutStatistics { TimedOutOperations = 50 };

		// Assert
		stats.TimedOutOperations.ShouldBe(50);
	}

	[Fact]
	public void AverageDuration_CanBeSet()
	{
		// Arrange
		var duration = TimeSpan.FromMilliseconds(150);

		// Act
		var stats = new TimeoutStatistics { AverageDuration = duration };

		// Assert
		stats.AverageDuration.ShouldBe(duration);
	}

	[Fact]
	public void MedianDuration_CanBeSet()
	{
		// Arrange
		var duration = TimeSpan.FromMilliseconds(100);

		// Act
		var stats = new TimeoutStatistics { MedianDuration = duration };

		// Assert
		stats.MedianDuration.ShouldBe(duration);
	}

	[Fact]
	public void P95Duration_CanBeSet()
	{
		// Arrange
		var duration = TimeSpan.FromMilliseconds(300);

		// Act
		var stats = new TimeoutStatistics { P95Duration = duration };

		// Assert
		stats.P95Duration.ShouldBe(duration);
	}

	[Fact]
	public void P99Duration_CanBeSet()
	{
		// Arrange
		var duration = TimeSpan.FromMilliseconds(500);

		// Act
		var stats = new TimeoutStatistics { P99Duration = duration };

		// Assert
		stats.P99Duration.ShouldBe(duration);
	}

	[Fact]
	public void MinDuration_CanBeSet()
	{
		// Arrange
		var duration = TimeSpan.FromMilliseconds(10);

		// Act
		var stats = new TimeoutStatistics { MinDuration = duration };

		// Assert
		stats.MinDuration.ShouldBe(duration);
	}

	[Fact]
	public void MaxDuration_CanBeSet()
	{
		// Arrange
		var duration = TimeSpan.FromMilliseconds(1000);

		// Act
		var stats = new TimeoutStatistics { MaxDuration = duration };

		// Assert
		stats.MaxDuration.ShouldBe(duration);
	}

	[Fact]
	public void LastUpdated_CanBeSet()
	{
		// Arrange
		var timestamp = new DateTimeOffset(2026, 1, 15, 10, 30, 0, TimeSpan.Zero);

		// Act
		var stats = new TimeoutStatistics { LastUpdated = timestamp };

		// Assert
		stats.LastUpdated.ShouldBe(timestamp);
	}

	[Fact]
	public void LastUpdated_DefaultsToUtcNow()
	{
		// Arrange
		var before = DateTimeOffset.UtcNow;

		// Act
		var stats = new TimeoutStatistics();
		var after = DateTimeOffset.UtcNow;

		// Assert
		stats.LastUpdated.ShouldBeGreaterThanOrEqualTo(before);
		stats.LastUpdated.ShouldBeLessThanOrEqualTo(after);
	}

	#endregion

	#region SuccessRate Tests

	[Fact]
	public void SuccessRate_WithZeroTotalOperations_ReturnsZero()
	{
		// Arrange
		var stats = new TimeoutStatistics
		{
			TotalOperations = 0,
			SuccessfulOperations = 0,
		};

		// Assert
		stats.SuccessRate.ShouldBe(0.0);
	}

	[Fact]
	public void SuccessRate_WithAllSuccessful_ReturnsHundred()
	{
		// Arrange
		var stats = new TimeoutStatistics
		{
			TotalOperations = 100,
			SuccessfulOperations = 100,
		};

		// Assert
		stats.SuccessRate.ShouldBe(100.0);
	}

	[Fact]
	public void SuccessRate_WithPartialSuccess_ReturnsCorrectPercentage()
	{
		// Arrange
		var stats = new TimeoutStatistics
		{
			TotalOperations = 100,
			SuccessfulOperations = 95,
		};

		// Assert
		stats.SuccessRate.ShouldBe(95.0);
	}

	[Fact]
	public void SuccessRate_WithNoSuccess_ReturnsZero()
	{
		// Arrange
		var stats = new TimeoutStatistics
		{
			TotalOperations = 100,
			SuccessfulOperations = 0,
		};

		// Assert
		stats.SuccessRate.ShouldBe(0.0);
	}

	#endregion

	#region TimeoutRate Tests

	[Fact]
	public void TimeoutRate_WithZeroTotalOperations_ReturnsZero()
	{
		// Arrange
		var stats = new TimeoutStatistics
		{
			TotalOperations = 0,
			TimedOutOperations = 0,
		};

		// Assert
		stats.TimeoutRate.ShouldBe(0.0);
	}

	[Fact]
	public void TimeoutRate_WithAllTimedOut_ReturnsHundred()
	{
		// Arrange
		var stats = new TimeoutStatistics
		{
			TotalOperations = 100,
			TimedOutOperations = 100,
		};

		// Assert
		stats.TimeoutRate.ShouldBe(100.0);
	}

	[Fact]
	public void TimeoutRate_WithPartialTimeouts_ReturnsCorrectPercentage()
	{
		// Arrange
		var stats = new TimeoutStatistics
		{
			TotalOperations = 100,
			TimedOutOperations = 5,
		};

		// Assert
		stats.TimeoutRate.ShouldBe(5.0);
	}

	[Fact]
	public void TimeoutRate_WithNoTimeouts_ReturnsZero()
	{
		// Arrange
		var stats = new TimeoutStatistics
		{
			TotalOperations = 100,
			TimedOutOperations = 0,
		};

		// Assert
		stats.TimeoutRate.ShouldBe(0.0);
	}

	#endregion

	#region GetPercentileDuration Tests

	[Theory]
	[InlineData(0)]
	[InlineData(25)]
	[InlineData(50)]
	public void GetPercentileDuration_AtOrBelow50_ReturnsMedian(int percentile)
	{
		// Arrange
		var stats = new TimeoutStatistics
		{
			MedianDuration = TimeSpan.FromMilliseconds(100),
			P95Duration = TimeSpan.FromMilliseconds(300),
			P99Duration = TimeSpan.FromMilliseconds(500),
			MaxDuration = TimeSpan.FromSeconds(1),
		};

		// Act
		var result = stats.GetPercentileDuration(percentile);

		// Assert
		result.ShouldBe(TimeSpan.FromMilliseconds(100));
	}

	[Theory]
	[InlineData(51)]
	[InlineData(75)]
	[InlineData(95)]
	public void GetPercentileDuration_Between51And95_ReturnsP95(int percentile)
	{
		// Arrange
		var stats = new TimeoutStatistics
		{
			MedianDuration = TimeSpan.FromMilliseconds(100),
			P95Duration = TimeSpan.FromMilliseconds(300),
			P99Duration = TimeSpan.FromMilliseconds(500),
			MaxDuration = TimeSpan.FromSeconds(1),
		};

		// Act
		var result = stats.GetPercentileDuration(percentile);

		// Assert
		result.ShouldBe(TimeSpan.FromMilliseconds(300));
	}

	[Theory]
	[InlineData(96)]
	[InlineData(99)]
	public void GetPercentileDuration_Between96And99_ReturnsP99(int percentile)
	{
		// Arrange
		var stats = new TimeoutStatistics
		{
			MedianDuration = TimeSpan.FromMilliseconds(100),
			P95Duration = TimeSpan.FromMilliseconds(300),
			P99Duration = TimeSpan.FromMilliseconds(500),
			MaxDuration = TimeSpan.FromSeconds(1),
		};

		// Act
		var result = stats.GetPercentileDuration(percentile);

		// Assert
		result.ShouldBe(TimeSpan.FromMilliseconds(500));
	}

	[Theory]
	[InlineData(100)]
	[InlineData(150)]
	public void GetPercentileDuration_Above99_ReturnsMax(int percentile)
	{
		// Arrange
		var stats = new TimeoutStatistics
		{
			MedianDuration = TimeSpan.FromMilliseconds(100),
			P95Duration = TimeSpan.FromMilliseconds(300),
			P99Duration = TimeSpan.FromMilliseconds(500),
			MaxDuration = TimeSpan.FromSeconds(1),
		};

		// Act
		var result = stats.GetPercentileDuration(percentile);

		// Assert
		result.ShouldBe(TimeSpan.FromSeconds(1));
	}

	#endregion

	#region HasSufficientData Tests

	[Fact]
	public void HasSufficientData_WithSufficientSamplesAndSuccess_ReturnsTrue()
	{
		// Arrange
		var stats = new TimeoutStatistics
		{
			TotalOperations = 100,
			SuccessfulOperations = 95,
		};

		// Act & Assert
		stats.HasSufficientData().ShouldBeTrue();
	}

	[Fact]
	public void HasSufficientData_WithCustomMinimumSamples_ReturnsTrue()
	{
		// Arrange
		var stats = new TimeoutStatistics
		{
			TotalOperations = 50,
			SuccessfulOperations = 45,
		};

		// Act & Assert
		stats.HasSufficientData(minimumSamples: 50).ShouldBeTrue();
	}

	[Fact]
	public void HasSufficientData_WithInsufficientSamples_ReturnsFalse()
	{
		// Arrange
		var stats = new TimeoutStatistics
		{
			TotalOperations = 99,
			SuccessfulOperations = 99,
		};

		// Act & Assert
		stats.HasSufficientData().ShouldBeFalse();
	}

	[Fact]
	public void HasSufficientData_WithZeroSuccessfulOperations_ReturnsFalse()
	{
		// Arrange
		var stats = new TimeoutStatistics
		{
			TotalOperations = 100,
			SuccessfulOperations = 0,
		};

		// Act & Assert
		stats.HasSufficientData().ShouldBeFalse();
	}

	[Fact]
	public void HasSufficientData_WithZeroTotalOperations_ReturnsFalse()
	{
		// Arrange
		var stats = new TimeoutStatistics
		{
			TotalOperations = 0,
			SuccessfulOperations = 0,
		};

		// Act & Assert
		stats.HasSufficientData().ShouldBeFalse();
	}

	#endregion
}
