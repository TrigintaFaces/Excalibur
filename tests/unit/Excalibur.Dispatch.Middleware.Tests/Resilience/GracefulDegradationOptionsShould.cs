// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Resilience.Polly;

namespace Excalibur.Dispatch.Middleware.Tests.Resilience;

/// <summary>
/// Unit tests for <see cref="GracefulDegradationOptions"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Resilience")]
public sealed class GracefulDegradationOptionsShould : UnitTestBase
{
	[Fact]
	public void DefaultValues_AreCorrect()
	{
		// Act
		var options = new GracefulDegradationOptions();

		// Assert - Default levels configured
		options.Levels.Count.ShouldBe(5);

		// Assert - Priority thresholds via Levels
		options.GetPriorityThreshold(DegradationLevel.Minor).ShouldBe(10);
		options.GetPriorityThreshold(DegradationLevel.Moderate).ShouldBe(30);
		options.GetPriorityThreshold(DegradationLevel.Major).ShouldBe(50);
		options.GetPriorityThreshold(DegradationLevel.Severe).ShouldBe(70);

		// Assert - Error rate thresholds via Levels
		options.GetErrorRateThreshold(DegradationLevel.Minor).ShouldBe(0.01);
		options.GetErrorRateThreshold(DegradationLevel.Moderate).ShouldBe(0.05);
		options.GetErrorRateThreshold(DegradationLevel.Major).ShouldBe(0.10);
		options.GetErrorRateThreshold(DegradationLevel.Severe).ShouldBe(0.25);
		options.GetErrorRateThreshold(DegradationLevel.Emergency).ShouldBe(0.50);

		// Assert - CPU thresholds via Levels
		options.GetCpuThreshold(DegradationLevel.Minor).ShouldBe(60);
		options.GetCpuThreshold(DegradationLevel.Moderate).ShouldBe(70);
		options.GetCpuThreshold(DegradationLevel.Major).ShouldBe(80);
		options.GetCpuThreshold(DegradationLevel.Severe).ShouldBe(90);
		options.GetCpuThreshold(DegradationLevel.Emergency).ShouldBe(95);

		// Assert - Memory thresholds via Levels
		options.GetMemoryThreshold(DegradationLevel.Minor).ShouldBe(60);
		options.GetMemoryThreshold(DegradationLevel.Moderate).ShouldBe(70);
		options.GetMemoryThreshold(DegradationLevel.Major).ShouldBe(80);
		options.GetMemoryThreshold(DegradationLevel.Severe).ShouldBe(90);
		options.GetMemoryThreshold(DegradationLevel.Emergency).ShouldBe(95);

		// Assert - Other settings
		options.EnableAutoAdjustment.ShouldBeTrue();
		options.HealthCheckInterval.ShouldBe(TimeSpan.FromSeconds(30));
		options.MinimumLevelDuration.ShouldBe(TimeSpan.FromMinutes(1));
	}

	[Fact]
	public void Levels_CanBeCustomized()
	{
		// Arrange & Act
		var options = new GracefulDegradationOptions
		{
			Levels =
			[
				new("Minor", 20, 0.02, 50, 50),
				new("Moderate", 40, 0.10, 60, 65),
				new("Major", 60, 0.20, 70, 75),
				new("Severe", 80, 0.35, 85, 85),
				new("Emergency", 100, 0.60, 98, 97),
			],
		};

		// Assert - Priority thresholds
		options.GetPriorityThreshold(DegradationLevel.Minor).ShouldBe(20);
		options.GetPriorityThreshold(DegradationLevel.Moderate).ShouldBe(40);
		options.GetPriorityThreshold(DegradationLevel.Major).ShouldBe(60);
		options.GetPriorityThreshold(DegradationLevel.Severe).ShouldBe(80);

		// Assert - Error rate thresholds
		options.GetErrorRateThreshold(DegradationLevel.Minor).ShouldBe(0.02);
		options.GetErrorRateThreshold(DegradationLevel.Moderate).ShouldBe(0.10);
		options.GetErrorRateThreshold(DegradationLevel.Major).ShouldBe(0.20);
		options.GetErrorRateThreshold(DegradationLevel.Severe).ShouldBe(0.35);
		options.GetErrorRateThreshold(DegradationLevel.Emergency).ShouldBe(0.60);

		// Assert - CPU thresholds
		options.GetCpuThreshold(DegradationLevel.Minor).ShouldBe(50);
		options.GetCpuThreshold(DegradationLevel.Moderate).ShouldBe(60);
		options.GetCpuThreshold(DegradationLevel.Major).ShouldBe(70);
		options.GetCpuThreshold(DegradationLevel.Severe).ShouldBe(85);
		options.GetCpuThreshold(DegradationLevel.Emergency).ShouldBe(98);

		// Assert - Memory thresholds
		options.GetMemoryThreshold(DegradationLevel.Minor).ShouldBe(50);
		options.GetMemoryThreshold(DegradationLevel.Moderate).ShouldBe(65);
		options.GetMemoryThreshold(DegradationLevel.Major).ShouldBe(75);
		options.GetMemoryThreshold(DegradationLevel.Severe).ShouldBe(85);
		options.GetMemoryThreshold(DegradationLevel.Emergency).ShouldBe(97);
	}

	[Fact]
	public void GetThreshold_ReturnsDefault_ForUnconfiguredLevel()
	{
		// Arrange - Empty levels
		var options = new GracefulDegradationOptions { Levels = [] };

		// Assert - Returns safe defaults (never triggers)
		options.GetPriorityThreshold(DegradationLevel.Minor).ShouldBe(0);
		options.GetErrorRateThreshold(DegradationLevel.Minor).ShouldBe(1.0);
		options.GetCpuThreshold(DegradationLevel.Minor).ShouldBe(100.0);
		options.GetMemoryThreshold(DegradationLevel.Minor).ShouldBe(100.0);
	}

	[Fact]
	public void EnableAutoAdjustment_CanBeSet()
	{
		// Arrange & Act
		var options = new GracefulDegradationOptions { EnableAutoAdjustment = false };

		// Assert
		options.EnableAutoAdjustment.ShouldBeFalse();
	}

	[Fact]
	public void HealthCheckInterval_CanBeSet()
	{
		// Arrange & Act
		var options = new GracefulDegradationOptions { HealthCheckInterval = TimeSpan.FromSeconds(15) };

		// Assert
		options.HealthCheckInterval.ShouldBe(TimeSpan.FromSeconds(15));
	}

	[Fact]
	public void MinimumLevelDuration_CanBeSet()
	{
		// Arrange & Act
		var options = new GracefulDegradationOptions { MinimumLevelDuration = TimeSpan.FromMinutes(5) };

		// Assert
		options.MinimumLevelDuration.ShouldBe(TimeSpan.FromMinutes(5));
	}
}
