// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Resilience.Polly;

namespace Excalibur.Dispatch.Tests.Resilience;

/// <summary>
/// Unit tests for <see cref="GracefulDegradationOptions"/> and <see cref="DegradationLevelConfig"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Resilience")]
public sealed class GracefulDegradationOptionsShould : UnitTestBase
{
	[Fact]
	public void DefaultValues_AreCorrect()
	{
		// Arrange & Act
		var options = new GracefulDegradationOptions();

		// Assert
		options.EnableAutoAdjustment.ShouldBeTrue();
		options.HealthCheckInterval.ShouldBe(TimeSpan.FromSeconds(30));
		options.MinimumLevelDuration.ShouldBe(TimeSpan.FromMinutes(1));
	}

	[Fact]
	public void DefaultLevels_ContainsFiveEntries()
	{
		// Arrange & Act
		var options = new GracefulDegradationOptions();

		// Assert
		options.Levels.Count.ShouldBe(5);
	}

	[Fact]
	public void DefaultLevels_ContainsExpectedNames()
	{
		// Arrange & Act
		var options = new GracefulDegradationOptions();
		var names = options.Levels.Select(l => l.Name).ToList();

		// Assert
		names.ShouldContain("Minor");
		names.ShouldContain("Moderate");
		names.ShouldContain("Major");
		names.ShouldContain("Severe");
		names.ShouldContain("Emergency");
	}

	[Fact]
	public void DefaultLevels_MinorHasExpectedThresholds()
	{
		// Arrange & Act
		var options = new GracefulDegradationOptions();
		var minor = options.Levels.First(l => l.Name == "Minor");

		// Assert
		minor.PriorityThreshold.ShouldBe(10);
		minor.ErrorRateThreshold.ShouldBe(0.01);
		minor.CpuThreshold.ShouldBe(60);
		minor.MemoryThreshold.ShouldBe(60);
	}

	[Fact]
	public void DefaultLevels_EmergencyHasExpectedThresholds()
	{
		// Arrange & Act
		var options = new GracefulDegradationOptions();
		var emergency = options.Levels.First(l => l.Name == "Emergency");

		// Assert
		emergency.PriorityThreshold.ShouldBe(100);
		emergency.ErrorRateThreshold.ShouldBe(0.50);
		emergency.CpuThreshold.ShouldBe(95);
		emergency.MemoryThreshold.ShouldBe(95);
	}

	[Fact]
	public void DefaultLevels_MajorHasExpectedThresholds()
	{
		// Arrange & Act
		var options = new GracefulDegradationOptions();
		var major = options.Levels.First(l => l.Name == "Major");

		// Assert
		major.PriorityThreshold.ShouldBe(50);
		major.ErrorRateThreshold.ShouldBe(0.10);
		major.CpuThreshold.ShouldBe(80);
		major.MemoryThreshold.ShouldBe(80);
	}

	[Fact]
	public void DefaultLevels_SevereHasExpectedThresholds()
	{
		// Arrange & Act
		var options = new GracefulDegradationOptions();
		var severe = options.Levels.First(l => l.Name == "Severe");

		// Assert
		severe.PriorityThreshold.ShouldBe(70);
		severe.ErrorRateThreshold.ShouldBe(0.25);
		severe.CpuThreshold.ShouldBe(90);
		severe.MemoryThreshold.ShouldBe(90);
	}

	[Fact]
	public void EnableAutoAdjustment_CanBeSetToFalse()
	{
		// Arrange & Act
		var options = new GracefulDegradationOptions { EnableAutoAdjustment = false };

		// Assert
		options.EnableAutoAdjustment.ShouldBeFalse();
	}

	[Fact]
	public void HealthCheckInterval_CanBeCustomized()
	{
		// Arrange & Act
		var options = new GracefulDegradationOptions
		{
			HealthCheckInterval = TimeSpan.FromSeconds(10),
		};

		// Assert
		options.HealthCheckInterval.ShouldBe(TimeSpan.FromSeconds(10));
	}

	[Fact]
	public void MinimumLevelDuration_CanBeCustomized()
	{
		// Arrange & Act
		var options = new GracefulDegradationOptions
		{
			MinimumLevelDuration = TimeSpan.FromSeconds(30),
		};

		// Assert
		options.MinimumLevelDuration.ShouldBe(TimeSpan.FromSeconds(30));
	}

	[Fact]
	public void DegradationLevelConfig_RecordEquality_Works()
	{
		// Arrange
		var config1 = new DegradationLevelConfig("Minor", 10, 0.01, 60, 60);
		var config2 = new DegradationLevelConfig("Minor", 10, 0.01, 60, 60);

		// Assert
		config1.ShouldBe(config2);
	}

	[Fact]
	public void DegradationLevelConfig_RecordInequality_Works()
	{
		// Arrange
		var config1 = new DegradationLevelConfig("Minor", 10, 0.01, 60, 60);
		var config2 = new DegradationLevelConfig("Major", 50, 0.10, 80, 80);

		// Assert
		config1.ShouldNotBe(config2);
	}

	[Fact]
	public void CustomLevels_CanBeSet()
	{
		// Arrange
		var options = new GracefulDegradationOptions
		{
			Levels =
			[
				new DegradationLevelConfig("Custom1", 25, 0.03, 50, 50),
				new DegradationLevelConfig("Custom2", 75, 0.20, 85, 85),
			],
		};

		// Assert
		options.Levels.Count.ShouldBe(2);
		options.Levels[0].Name.ShouldBe("Custom1");
	}

	[Fact]
	public void DegradationLevelConfig_Properties_AreAccessible()
	{
		// Arrange & Act
		var config = new DegradationLevelConfig("TestLevel", 42, 0.15, 75.5, 80.0);

		// Assert
		config.Name.ShouldBe("TestLevel");
		config.PriorityThreshold.ShouldBe(42);
		config.ErrorRateThreshold.ShouldBe(0.15);
		config.CpuThreshold.ShouldBe(75.5);
		config.MemoryThreshold.ShouldBe(80.0);
	}

	[Fact]
	public void DefaultLevels_AreOrderedFromLeastToMostSevere()
	{
		// Arrange & Act
		var options = new GracefulDegradationOptions();

		// Assert - thresholds should increase from first to last
		for (var i = 0; i < options.Levels.Count - 1; i++)
		{
			options.Levels[i].PriorityThreshold.ShouldBeLessThan(options.Levels[i + 1].PriorityThreshold);
		}
	}

	[Fact]
	public void DegradationLevelConfig_ToString_ContainsName()
	{
		// Arrange
		var config = new DegradationLevelConfig("Emergency", 100, 0.50, 95, 95);

		// Act
		var str = config.ToString();

		// Assert
		str.ShouldContain("Emergency");
	}

	[Fact]
	public void DegradationLevelConfig_GetHashCode_DiffersForDifferentValues()
	{
		// Arrange
		var config1 = new DegradationLevelConfig("Minor", 10, 0.01, 60, 60);
		var config2 = new DegradationLevelConfig("Major", 50, 0.10, 80, 80);

		// Assert
		config1.GetHashCode().ShouldNotBe(config2.GetHashCode());
	}
}
