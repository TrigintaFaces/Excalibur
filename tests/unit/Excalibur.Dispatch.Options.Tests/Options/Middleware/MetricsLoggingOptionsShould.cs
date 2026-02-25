// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Options.Middleware;

namespace Excalibur.Dispatch.Tests.Options.Middleware;

/// <summary>
/// Unit tests for <see cref="MetricsLoggingOptions"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Options")]
[Trait("Priority", "0")]
public sealed class MetricsLoggingOptionsShould
{
	#region Default Value Tests

	[Fact]
	public void Default_Enabled_IsTrue()
	{
		// Arrange & Act
		var options = new MetricsLoggingOptions();

		// Assert
		options.Enabled.ShouldBeTrue();
	}

	[Fact]
	public void Default_RecordOpenTelemetryMetrics_IsTrue()
	{
		// Arrange & Act
		var options = new MetricsLoggingOptions();

		// Assert
		options.RecordOpenTelemetryMetrics.ShouldBeTrue();
	}

	[Fact]
	public void Default_RecordCustomMetrics_IsTrue()
	{
		// Arrange & Act
		var options = new MetricsLoggingOptions();

		// Assert
		options.RecordCustomMetrics.ShouldBeTrue();
	}

	[Fact]
	public void Default_LogProcessingDetails_IsTrue()
	{
		// Arrange & Act
		var options = new MetricsLoggingOptions();

		// Assert
		options.LogProcessingDetails.ShouldBeTrue();
	}

	[Fact]
	public void Default_SlowOperationThreshold_IsOneSecond()
	{
		// Arrange & Act
		var options = new MetricsLoggingOptions();

		// Assert
		options.SlowOperationThreshold.ShouldBe(TimeSpan.FromSeconds(1));
	}

	[Fact]
	public void Default_BypassMetricsForTypes_IsNull()
	{
		// Arrange & Act
		var options = new MetricsLoggingOptions();

		// Assert
		options.BypassMetricsForTypes.ShouldBeNull();
	}

	[Fact]
	public void Default_IncludeMessageSizes_IsTrue()
	{
		// Arrange & Act
		var options = new MetricsLoggingOptions();

		// Assert
		options.IncludeMessageSizes.ShouldBeTrue();
	}

	[Fact]
	public void Default_SampleRate_IsOne()
	{
		// Arrange & Act
		var options = new MetricsLoggingOptions();

		// Assert
		options.SampleRate.ShouldBe(1.0);
	}

	#endregion

	#region Property Setter Tests

	[Fact]
	public void Enabled_CanBeSet()
	{
		// Arrange
		var options = new MetricsLoggingOptions();

		// Act
		options.Enabled = false;

		// Assert
		options.Enabled.ShouldBeFalse();
	}

	[Fact]
	public void RecordOpenTelemetryMetrics_CanBeSet()
	{
		// Arrange
		var options = new MetricsLoggingOptions();

		// Act
		options.RecordOpenTelemetryMetrics = false;

		// Assert
		options.RecordOpenTelemetryMetrics.ShouldBeFalse();
	}

	[Fact]
	public void SlowOperationThreshold_CanBeSet()
	{
		// Arrange
		var options = new MetricsLoggingOptions();

		// Act
		options.SlowOperationThreshold = TimeSpan.FromSeconds(5);

		// Assert
		options.SlowOperationThreshold.ShouldBe(TimeSpan.FromSeconds(5));
	}

	[Fact]
	public void BypassMetricsForTypes_CanBeSet()
	{
		// Arrange
		var options = new MetricsLoggingOptions();

		// Act
		options.BypassMetricsForTypes = ["HealthCheck", "Ping"];

		// Assert
		_ = options.BypassMetricsForTypes.ShouldNotBeNull();
		options.BypassMetricsForTypes.Length.ShouldBe(2);
	}

	[Fact]
	public void SampleRate_CanBeSet()
	{
		// Arrange
		var options = new MetricsLoggingOptions();

		// Act
		options.SampleRate = 0.5;

		// Assert
		options.SampleRate.ShouldBe(0.5);
	}

	#endregion

	#region Object Initializer Tests

	[Fact]
	public void ObjectInitializer_SetsAllProperties()
	{
		// Act
		var options = new MetricsLoggingOptions
		{
			Enabled = false,
			RecordOpenTelemetryMetrics = false,
			RecordCustomMetrics = false,
			LogProcessingDetails = false,
			SlowOperationThreshold = TimeSpan.FromSeconds(2),
			BypassMetricsForTypes = ["Test"],
			IncludeMessageSizes = false,
			SampleRate = 0.1,
		};

		// Assert
		options.Enabled.ShouldBeFalse();
		options.RecordOpenTelemetryMetrics.ShouldBeFalse();
		options.RecordCustomMetrics.ShouldBeFalse();
		options.LogProcessingDetails.ShouldBeFalse();
		options.SlowOperationThreshold.ShouldBe(TimeSpan.FromSeconds(2));
		_ = options.BypassMetricsForTypes.ShouldNotBeNull();
		options.IncludeMessageSizes.ShouldBeFalse();
		options.SampleRate.ShouldBe(0.1);
	}

	#endregion

	#region Real-World Scenario Tests

	[Fact]
	public void Options_ForProduction_HasFullMetrics()
	{
		// Act
		var options = new MetricsLoggingOptions
		{
			Enabled = true,
			RecordOpenTelemetryMetrics = true,
			IncludeMessageSizes = true,
			SampleRate = 1.0,
		};

		// Assert
		options.Enabled.ShouldBeTrue();
		options.SampleRate.ShouldBe(1.0);
	}

	[Fact]
	public void Options_ForSampling_HasReducedRate()
	{
		// Act
		var options = new MetricsLoggingOptions
		{
			Enabled = true,
			SampleRate = 0.1, // 10% sampling
			IncludeMessageSizes = false,
		};

		// Assert
		options.SampleRate.ShouldBeLessThan(1.0);
		options.IncludeMessageSizes.ShouldBeFalse();
	}

	[Fact]
	public void Options_ForDebugMode_HasLongThreshold()
	{
		// Act
		var options = new MetricsLoggingOptions
		{
			SlowOperationThreshold = TimeSpan.FromSeconds(10),
			LogProcessingDetails = true,
		};

		// Assert
		options.SlowOperationThreshold.ShouldBeGreaterThan(TimeSpan.FromSeconds(1));
	}

	#endregion
}
