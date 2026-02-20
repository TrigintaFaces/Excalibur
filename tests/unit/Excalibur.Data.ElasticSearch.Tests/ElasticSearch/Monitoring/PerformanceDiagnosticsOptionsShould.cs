// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.ElasticSearch.Monitoring;

namespace Excalibur.Data.Tests.ElasticSearch.Monitoring;

/// <summary>
/// Unit tests for the <see cref="PerformanceDiagnosticsOptions"/> class.
/// </summary>
/// <remarks>
/// Sprint 513 (S513.2): Elasticsearch Phase 2 unit tests.
/// Tests verify default values and configuration of performance diagnostics settings.
/// </remarks>
[Trait("Category", TestCategories.Unit)]
[Trait("Component", "Elasticsearch")]
[Trait("Feature", "Monitoring")]
public sealed class PerformanceDiagnosticsOptionsShould
{
	#region Default Value Tests

	[Fact]
	public void DefaultEnabled_ToTrue()
	{
		// Arrange & Act
		var settings = new PerformanceDiagnosticsOptions();

		// Assert
		settings.Enabled.ShouldBeTrue();
	}

	[Fact]
	public void DefaultSlowOperationThreshold_To5Seconds()
	{
		// Arrange & Act
		var settings = new PerformanceDiagnosticsOptions();

		// Assert
		settings.SlowOperationThreshold.ShouldBe(TimeSpan.FromSeconds(5));
	}

	[Fact]
	public void DefaultTrackMemoryUsage_ToFalse()
	{
		// Arrange & Act
		var settings = new PerformanceDiagnosticsOptions();

		// Assert
		settings.TrackMemoryUsage.ShouldBeFalse();
	}

	[Fact]
	public void DefaultAnalyzeQueryPerformance_ToFalse()
	{
		// Arrange & Act
		var settings = new PerformanceDiagnosticsOptions();

		// Assert
		settings.AnalyzeQueryPerformance.ShouldBeFalse();
	}

	[Fact]
	public void DefaultSamplingRate_To0Point01()
	{
		// Arrange & Act
		var settings = new PerformanceDiagnosticsOptions();

		// Assert
		settings.SamplingRate.ShouldBe(0.01);
	}

	#endregion

	#region Property Configuration Tests

	[Fact]
	public void AllowEnabled_ToBeSetToFalse()
	{
		// Arrange & Act
		var settings = new PerformanceDiagnosticsOptions { Enabled = false };

		// Assert
		settings.Enabled.ShouldBeFalse();
	}

	[Fact]
	public void AllowCustomSlowOperationThreshold()
	{
		// Arrange
		var customThreshold = TimeSpan.FromSeconds(10);

		// Act
		var settings = new PerformanceDiagnosticsOptions { SlowOperationThreshold = customThreshold };

		// Assert
		settings.SlowOperationThreshold.ShouldBe(customThreshold);
	}

	[Fact]
	public void AllowTrackMemoryUsage_ToBeSetToTrue()
	{
		// Arrange & Act
		var settings = new PerformanceDiagnosticsOptions { TrackMemoryUsage = true };

		// Assert
		settings.TrackMemoryUsage.ShouldBeTrue();
	}

	[Fact]
	public void AllowAnalyzeQueryPerformance_ToBeSetToTrue()
	{
		// Arrange & Act
		var settings = new PerformanceDiagnosticsOptions { AnalyzeQueryPerformance = true };

		// Assert
		settings.AnalyzeQueryPerformance.ShouldBeTrue();
	}

	[Fact]
	public void AllowCustomSamplingRate()
	{
		// Arrange & Act
		var settings = new PerformanceDiagnosticsOptions { SamplingRate = 0.5 };

		// Assert
		settings.SamplingRate.ShouldBe(0.5);
	}

	#endregion

	#region Instance Creation Tests

	[Fact]
	public void CreateNewInstance_WithDefaultConstructor()
	{
		// Act
		var settings = new PerformanceDiagnosticsOptions();

		// Assert
		settings.ShouldNotBeNull();
	}

	[Fact]
	public void CreateNewInstance_WithAllPropertiesConfigured()
	{
		// Arrange & Act
		var settings = new PerformanceDiagnosticsOptions
		{
			Enabled = false,
			SlowOperationThreshold = TimeSpan.FromSeconds(30),
			TrackMemoryUsage = true,
			AnalyzeQueryPerformance = true,
			SamplingRate = 1.0
		};

		// Assert
		settings.Enabled.ShouldBeFalse();
		settings.SlowOperationThreshold.ShouldBe(TimeSpan.FromSeconds(30));
		settings.TrackMemoryUsage.ShouldBeTrue();
		settings.AnalyzeQueryPerformance.ShouldBeTrue();
		settings.SamplingRate.ShouldBe(1.0);
	}

	#endregion

	#region SamplingRate Boundary Tests

	[Fact]
	public void AllowZeroSamplingRate()
	{
		// Arrange & Act
		var settings = new PerformanceDiagnosticsOptions { SamplingRate = 0.0 };

		// Assert
		settings.SamplingRate.ShouldBe(0.0);
	}

	[Fact]
	public void AllowFullSamplingRate()
	{
		// Arrange & Act
		var settings = new PerformanceDiagnosticsOptions { SamplingRate = 1.0 };

		// Assert
		settings.SamplingRate.ShouldBe(1.0);
	}

	[Fact]
	public void AllowSamplingRateAboveOne()
	{
		// Note: The property allows any double; validation should be done at configuration level
		var settings = new PerformanceDiagnosticsOptions { SamplingRate = 2.0 };

		// Assert
		settings.SamplingRate.ShouldBe(2.0);
	}

	[Fact]
	public void AllowNegativeSamplingRate()
	{
		// Note: The property allows any double; validation should be done at configuration level
		var settings = new PerformanceDiagnosticsOptions { SamplingRate = -0.5 };

		// Assert
		settings.SamplingRate.ShouldBe(-0.5);
	}

	#endregion

	#region TimeSpan Edge Cases

	[Fact]
	public void AllowZeroSlowOperationThreshold()
	{
		// Arrange & Act
		var settings = new PerformanceDiagnosticsOptions { SlowOperationThreshold = TimeSpan.Zero };

		// Assert
		settings.SlowOperationThreshold.ShouldBe(TimeSpan.Zero);
	}

	[Fact]
	public void AllowLargeSlowOperationThreshold()
	{
		// Arrange
		var largeThreshold = TimeSpan.FromMinutes(10);

		// Act
		var settings = new PerformanceDiagnosticsOptions { SlowOperationThreshold = largeThreshold };

		// Assert
		settings.SlowOperationThreshold.ShouldBe(largeThreshold);
	}

	#endregion
}
