// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.CloudNative;

namespace Excalibur.Dispatch.Tests.Messaging.CloudNative;

/// <summary>
/// Unit tests for <see cref="PatternMetrics"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class PatternMetricsShould
{
	[Fact]
	public void HaveDefaultZeroValues()
	{
		// Arrange & Act
		var metrics = new PatternMetrics();

		// Assert
		metrics.TotalOperations.ShouldBe(0);
		metrics.SuccessfulOperations.ShouldBe(0);
		metrics.FailedOperations.ShouldBe(0);
		metrics.AverageOperationTime.ShouldBe(TimeSpan.Zero);
	}

	[Fact]
	public void HaveDefaultLastUpdatedTimestamp()
	{
		// Arrange
		var before = DateTimeOffset.UtcNow;

		// Act
		var metrics = new PatternMetrics();
		var after = DateTimeOffset.UtcNow;

		// Assert
		metrics.LastUpdated.ShouldBeGreaterThanOrEqualTo(before);
		metrics.LastUpdated.ShouldBeLessThanOrEqualTo(after);
	}

	[Fact]
	public void HaveEmptyCustomMetricsByDefault()
	{
		// Arrange & Act
		var metrics = new PatternMetrics();

		// Assert
		metrics.CustomMetrics.ShouldNotBeNull();
		metrics.CustomMetrics.ShouldBeEmpty();
	}

	[Fact]
	public void CalculateSuccessRateAsZeroWhenNoOperations()
	{
		// Arrange & Act
		var metrics = new PatternMetrics();

		// Assert
		metrics.SuccessRate.ShouldBe(0);
	}

	[Fact]
	public void CalculateSuccessRateCorrectly()
	{
		// Arrange
		var metrics = new PatternMetrics
		{
			TotalOperations = 100,
			SuccessfulOperations = 85,
		};

		// Act
		var rate = metrics.SuccessRate;

		// Assert
		rate.ShouldBe(0.85);
	}

	[Fact]
	public void CalculateFullSuccessRate()
	{
		// Arrange
		var metrics = new PatternMetrics
		{
			TotalOperations = 50,
			SuccessfulOperations = 50,
		};

		// Act
		var rate = metrics.SuccessRate;

		// Assert
		rate.ShouldBe(1.0);
	}

	[Fact]
	public void CalculateZeroSuccessRate()
	{
		// Arrange
		var metrics = new PatternMetrics
		{
			TotalOperations = 50,
			SuccessfulOperations = 0,
		};

		// Act
		var rate = metrics.SuccessRate;

		// Assert
		rate.ShouldBe(0.0);
	}

	[Fact]
	public void AllowSettingTotalOperations()
	{
		// Arrange
		var metrics = new PatternMetrics();

		// Act
		metrics.TotalOperations = 1000;

		// Assert
		metrics.TotalOperations.ShouldBe(1000);
	}

	[Fact]
	public void AllowSettingSuccessfulOperations()
	{
		// Arrange
		var metrics = new PatternMetrics();

		// Act
		metrics.SuccessfulOperations = 900;

		// Assert
		metrics.SuccessfulOperations.ShouldBe(900);
	}

	[Fact]
	public void AllowSettingFailedOperations()
	{
		// Arrange
		var metrics = new PatternMetrics();

		// Act
		metrics.FailedOperations = 100;

		// Assert
		metrics.FailedOperations.ShouldBe(100);
	}

	[Fact]
	public void AllowSettingAverageOperationTime()
	{
		// Arrange
		var metrics = new PatternMetrics();
		var duration = TimeSpan.FromMilliseconds(50);

		// Act
		metrics.AverageOperationTime = duration;

		// Assert
		metrics.AverageOperationTime.ShouldBe(duration);
	}

	[Fact]
	public void AllowSettingLastUpdated()
	{
		// Arrange
		var metrics = new PatternMetrics();
		var customTime = new DateTimeOffset(2026, 1, 15, 12, 0, 0, TimeSpan.Zero);

		// Act
		metrics.LastUpdated = customTime;

		// Assert
		metrics.LastUpdated.ShouldBe(customTime);
	}

	[Fact]
	public void AllowAddingCustomMetrics()
	{
		// Arrange
		var metrics = new PatternMetrics();

		// Act
		metrics.CustomMetrics["retries"] = 5;
		metrics.CustomMetrics["maxLatency"] = TimeSpan.FromSeconds(2);

		// Assert
		metrics.CustomMetrics.Count.ShouldBe(2);
		metrics.CustomMetrics["retries"].ShouldBe(5);
		metrics.CustomMetrics["maxLatency"].ShouldBe(TimeSpan.FromSeconds(2));
	}

	[Fact]
	public void SupportObjectInitializer()
	{
		// Arrange & Act
		var metrics = new PatternMetrics
		{
			TotalOperations = 500,
			SuccessfulOperations = 450,
			FailedOperations = 50,
			AverageOperationTime = TimeSpan.FromMilliseconds(100),
			LastUpdated = new DateTimeOffset(2026, 6, 1, 10, 0, 0, TimeSpan.Zero),
			CustomMetrics = new Dictionary<string, object>
			{
				["peakLoad"] = 100,
				["region"] = "us-east",
			},
		};

		// Assert
		metrics.TotalOperations.ShouldBe(500);
		metrics.SuccessfulOperations.ShouldBe(450);
		metrics.FailedOperations.ShouldBe(50);
		metrics.AverageOperationTime.ShouldBe(TimeSpan.FromMilliseconds(100));
		metrics.CustomMetrics.Count.ShouldBe(2);
	}

	[Theory]
	[InlineData(0)]
	[InlineData(100)]
	[InlineData(long.MaxValue)]
	public void AcceptVariousTotalOperationsValues(long value)
	{
		// Arrange
		var metrics = new PatternMetrics();

		// Act
		metrics.TotalOperations = value;

		// Assert
		metrics.TotalOperations.ShouldBe(value);
	}

	[Fact]
	public void TrackTypicalUsageScenario()
	{
		// Arrange & Act - Simulate real usage metrics
		var metrics = new PatternMetrics
		{
			TotalOperations = 1000,
			SuccessfulOperations = 950,
			FailedOperations = 50,
			AverageOperationTime = TimeSpan.FromMilliseconds(25),
		};

		// Assert
		metrics.SuccessRate.ShouldBe(0.95);
		(metrics.SuccessfulOperations + metrics.FailedOperations).ShouldBe(metrics.TotalOperations);
	}
}
