// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.ElasticSearch.Monitoring;

namespace Excalibur.Data.Tests.ElasticSearch.Monitoring;

/// <summary>
/// Unit tests for the <see cref="MetricsOptions"/> class.
/// </summary>
/// <remarks>
/// Sprint 513 (S513.2): Elasticsearch Phase 2 unit tests.
/// Tests verify default values and configuration of metrics collection settings.
/// </remarks>
[Trait("Category", TestCategories.Unit)]
[Trait("Component", "Elasticsearch")]
[Trait("Feature", "Monitoring")]
public sealed class MetricsOptionsShould
{
	#region Default Value Tests

	[Fact]
	public void DefaultEnabled_ToTrue()
	{
		// Arrange & Act
		var settings = new MetricsOptions();

		// Assert
		settings.Enabled.ShouldBeTrue();
	}

	[Fact]
	public void DefaultIncludeDuration_ToTrue()
	{
		// Arrange & Act
		var settings = new MetricsOptions();

		// Assert
		settings.IncludeDuration.ShouldBeTrue();
	}

	[Fact]
	public void DefaultIncludeSuccessFailureRates_ToTrue()
	{
		// Arrange & Act
		var settings = new MetricsOptions();

		// Assert
		settings.IncludeSuccessFailureRates.ShouldBeTrue();
	}

	[Fact]
	public void DefaultIncludeCircuitBreakerState_ToTrue()
	{
		// Arrange & Act
		var settings = new MetricsOptions();

		// Assert
		settings.IncludeCircuitBreakerState.ShouldBeTrue();
	}

	[Fact]
	public void DefaultIncludeRetryAttempts_ToTrue()
	{
		// Arrange & Act
		var settings = new MetricsOptions();

		// Assert
		settings.IncludeRetryAttempts.ShouldBeTrue();
	}

	[Fact]
	public void DefaultIncludeDocumentCounts_ToFalse()
	{
		// Arrange & Act
		var settings = new MetricsOptions();

		// Assert
		settings.IncludeDocumentCounts.ShouldBeFalse();
	}

	[Fact]
	public void DefaultDurationHistogramBuckets_ToExpectedValues()
	{
		// Arrange & Act
		var settings = new MetricsOptions();

		// Assert
		settings.DurationHistogramBuckets.ShouldNotBeNull();
		settings.DurationHistogramBuckets.Length.ShouldBe(12);
		settings.DurationHistogramBuckets[0].ShouldBe(1);
		settings.DurationHistogramBuckets[^1].ShouldBe(10000);
	}

	[Fact]
	public void DurationHistogramBuckets_ContainExpectedBoundaries()
	{
		// Arrange & Act
		var settings = new MetricsOptions();
		var expected = new double[] { 1, 5, 10, 25, 50, 100, 250, 500, 1000, 2500, 5000, 10000 };

		// Assert
		settings.DurationHistogramBuckets.ShouldBe(expected);
	}

	#endregion

	#region Property Configuration Tests

	[Fact]
	public void AllowEnabled_ToBeSetToFalse()
	{
		// Arrange & Act
		var settings = new MetricsOptions { Enabled = false };

		// Assert
		settings.Enabled.ShouldBeFalse();
	}

	[Fact]
	public void AllowIncludeDuration_ToBeSetToFalse()
	{
		// Arrange & Act
		var settings = new MetricsOptions { IncludeDuration = false };

		// Assert
		settings.IncludeDuration.ShouldBeFalse();
	}

	[Fact]
	public void AllowIncludeSuccessFailureRates_ToBeSetToFalse()
	{
		// Arrange & Act
		var settings = new MetricsOptions { IncludeSuccessFailureRates = false };

		// Assert
		settings.IncludeSuccessFailureRates.ShouldBeFalse();
	}

	[Fact]
	public void AllowIncludeCircuitBreakerState_ToBeSetToFalse()
	{
		// Arrange & Act
		var settings = new MetricsOptions { IncludeCircuitBreakerState = false };

		// Assert
		settings.IncludeCircuitBreakerState.ShouldBeFalse();
	}

	[Fact]
	public void AllowIncludeRetryAttempts_ToBeSetToFalse()
	{
		// Arrange & Act
		var settings = new MetricsOptions { IncludeRetryAttempts = false };

		// Assert
		settings.IncludeRetryAttempts.ShouldBeFalse();
	}

	[Fact]
	public void AllowIncludeDocumentCounts_ToBeSetToTrue()
	{
		// Arrange & Act
		var settings = new MetricsOptions { IncludeDocumentCounts = true };

		// Assert
		settings.IncludeDocumentCounts.ShouldBeTrue();
	}

	[Fact]
	public void AllowCustomDurationHistogramBuckets()
	{
		// Arrange
		var customBuckets = new double[] { 10, 100, 1000 };

		// Act
		var settings = new MetricsOptions { DurationHistogramBuckets = customBuckets };

		// Assert
		settings.DurationHistogramBuckets.ShouldBe(customBuckets);
	}

	#endregion

	#region Instance Creation Tests

	[Fact]
	public void CreateNewInstance_WithDefaultConstructor()
	{
		// Act
		var settings = new MetricsOptions();

		// Assert
		settings.ShouldNotBeNull();
	}

	[Fact]
	public void CreateNewInstance_WithAllPropertiesConfigured()
	{
		// Arrange & Act
		var settings = new MetricsOptions
		{
			Enabled = false,
			IncludeDuration = false,
			IncludeSuccessFailureRates = false,
			IncludeCircuitBreakerState = false,
			IncludeRetryAttempts = false,
			IncludeDocumentCounts = true,
			DurationHistogramBuckets = [5, 50, 500]
		};

		// Assert
		settings.Enabled.ShouldBeFalse();
		settings.IncludeDuration.ShouldBeFalse();
		settings.IncludeSuccessFailureRates.ShouldBeFalse();
		settings.IncludeCircuitBreakerState.ShouldBeFalse();
		settings.IncludeRetryAttempts.ShouldBeFalse();
		settings.IncludeDocumentCounts.ShouldBeTrue();
		settings.DurationHistogramBuckets.Length.ShouldBe(3);
	}

	#endregion
}
