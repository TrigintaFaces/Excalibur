// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.ElasticSearch.Monitoring;

namespace Excalibur.Data.Tests.ElasticSearch.Monitoring;

/// <summary>
/// Unit tests for the <see cref="ElasticsearchMonitoringOptions"/> class.
/// </summary>
/// <remarks>
/// Sprint 513 (S513.2): Elasticsearch Phase 2 unit tests.
/// Tests verify default values and configuration of the main monitoring settings container.
/// </remarks>
[Trait("Category", TestCategories.Unit)]
[Trait("Component", "Elasticsearch")]
[Trait("Feature", "Monitoring")]
public sealed class ElasticsearchMonitoringOptionsShould
{
	#region Default Value Tests

	[Fact]
	public void DefaultEnabled_ToTrue()
	{
		// Arrange & Act
		var settings = new ElasticsearchMonitoringOptions();

		// Assert
		settings.Enabled.ShouldBeTrue();
	}

	[Fact]
	public void DefaultLevel_ToStandard()
	{
		// Arrange & Act
		var settings = new ElasticsearchMonitoringOptions();

		// Assert
		settings.Level.ShouldBe(MonitoringLevel.Standard);
	}

	[Fact]
	public void DefaultMetrics_ToNotNull()
	{
		// Arrange & Act
		var settings = new ElasticsearchMonitoringOptions();

		// Assert
		settings.Metrics.ShouldNotBeNull();
	}

	[Fact]
	public void DefaultRequestLogging_ToNotNull()
	{
		// Arrange & Act
		var settings = new ElasticsearchMonitoringOptions();

		// Assert
		settings.RequestLogging.ShouldNotBeNull();
	}

	[Fact]
	public void DefaultPerformance_ToNotNull()
	{
		// Arrange & Act
		var settings = new ElasticsearchMonitoringOptions();

		// Assert
		settings.Performance.ShouldNotBeNull();
	}

	[Fact]
	public void DefaultHealth_ToNotNull()
	{
		// Arrange & Act
		var settings = new ElasticsearchMonitoringOptions();

		// Assert
		settings.Health.ShouldNotBeNull();
	}

	[Fact]
	public void DefaultTracing_ToNotNull()
	{
		// Arrange & Act
		var settings = new ElasticsearchMonitoringOptions();

		// Assert
		settings.Tracing.ShouldNotBeNull();
	}

	#endregion

	#region Nested Settings Default Value Tests

	[Fact]
	public void DefaultMetrics_HasExpectedDefaults()
	{
		// Arrange & Act
		var settings = new ElasticsearchMonitoringOptions();

		// Assert
		settings.Metrics.Enabled.ShouldBeTrue();
		settings.Metrics.IncludeDuration.ShouldBeTrue();
	}

	[Fact]
	public void DefaultRequestLogging_HasExpectedDefaults()
	{
		// Arrange & Act
		var settings = new ElasticsearchMonitoringOptions();

		// Assert
		settings.RequestLogging.Enabled.ShouldBeFalse();
		settings.RequestLogging.LogFailuresOnly.ShouldBeTrue();
	}

	[Fact]
	public void DefaultPerformance_HasExpectedDefaults()
	{
		// Arrange & Act
		var settings = new ElasticsearchMonitoringOptions();

		// Assert
		settings.Performance.Enabled.ShouldBeTrue();
		settings.Performance.SlowOperationThreshold.ShouldBe(TimeSpan.FromSeconds(5));
	}

	[Fact]
	public void DefaultHealth_HasExpectedDefaults()
	{
		// Arrange & Act
		var settings = new ElasticsearchMonitoringOptions();

		// Assert
		settings.Health.Enabled.ShouldBeTrue();
		settings.Health.HealthCheckInterval.ShouldBe(TimeSpan.FromSeconds(30));
	}

	[Fact]
	public void DefaultTracing_HasExpectedDefaults()
	{
		// Arrange & Act
		var settings = new ElasticsearchMonitoringOptions();

		// Assert
		settings.Tracing.Enabled.ShouldBeTrue();
		settings.Tracing.ActivitySourceName.ShouldBe("Excalibur.Data.ElasticSearch");
	}

	#endregion

	#region Property Configuration Tests

	[Fact]
	public void AllowEnabled_ToBeSetToFalse()
	{
		// Arrange & Act
		var settings = new ElasticsearchMonitoringOptions { Enabled = false };

		// Assert
		settings.Enabled.ShouldBeFalse();
	}

	[Theory]
	[InlineData(MonitoringLevel.Minimal)]
	[InlineData(MonitoringLevel.Standard)]
	[InlineData(MonitoringLevel.Verbose)]
	public void AllowLevel_ToBeSet(MonitoringLevel level)
	{
		// Arrange & Act
		var settings = new ElasticsearchMonitoringOptions { Level = level };

		// Assert
		settings.Level.ShouldBe(level);
	}

	[Fact]
	public void AllowCustomMetricsOptions()
	{
		// Arrange
		var customMetrics = new MetricsOptions { Enabled = false };

		// Act
		var settings = new ElasticsearchMonitoringOptions { Metrics = customMetrics };

		// Assert
		settings.Metrics.Enabled.ShouldBeFalse();
	}

	[Fact]
	public void AllowCustomRequestLoggingOptions()
	{
		// Arrange
		var customLogging = new RequestLoggingOptions { Enabled = true };

		// Act
		var settings = new ElasticsearchMonitoringOptions { RequestLogging = customLogging };

		// Assert
		settings.RequestLogging.Enabled.ShouldBeTrue();
	}

	[Fact]
	public void AllowCustomPerformanceSettings()
	{
		// Arrange
		var customPerformance = new PerformanceDiagnosticsOptions { SlowOperationThreshold = TimeSpan.FromSeconds(10) };

		// Act
		var settings = new ElasticsearchMonitoringOptions { Performance = customPerformance };

		// Assert
		settings.Performance.SlowOperationThreshold.ShouldBe(TimeSpan.FromSeconds(10));
	}

	[Fact]
	public void AllowCustomHealthSettings()
	{
		// Arrange
		var customHealth = new HealthMonitoringOptions { HealthCheckInterval = TimeSpan.FromMinutes(1) };

		// Act
		var settings = new ElasticsearchMonitoringOptions { Health = customHealth };

		// Assert
		settings.Health.HealthCheckInterval.ShouldBe(TimeSpan.FromMinutes(1));
	}

	[Fact]
	public void AllowCustomTracingOptions()
	{
		// Arrange
		var customTracing = new TracingOptions { ActivitySourceName = "Custom.Source" };

		// Act
		var settings = new ElasticsearchMonitoringOptions { Tracing = customTracing };

		// Assert
		settings.Tracing.ActivitySourceName.ShouldBe("Custom.Source");
	}

	#endregion

	#region Instance Creation Tests

	[Fact]
	public void CreateNewInstance_WithDefaultConstructor()
	{
		// Act
		var settings = new ElasticsearchMonitoringOptions();

		// Assert
		settings.ShouldNotBeNull();
	}

	[Fact]
	public void CreateNewInstance_WithAllPropertiesConfigured()
	{
		// Arrange & Act
		var settings = new ElasticsearchMonitoringOptions
		{
			Enabled = false,
			Level = MonitoringLevel.Verbose,
			Metrics = new MetricsOptions { Enabled = false },
			RequestLogging = new RequestLoggingOptions { Enabled = true },
			Performance = new PerformanceDiagnosticsOptions { SamplingRate = 0.5 },
			Health = new HealthMonitoringOptions { MonitorNodeHealth = true },
			Tracing = new TracingOptions { RecordRequestResponse = true }
		};

		// Assert
		settings.Enabled.ShouldBeFalse();
		settings.Level.ShouldBe(MonitoringLevel.Verbose);
		settings.Metrics.Enabled.ShouldBeFalse();
		settings.RequestLogging.Enabled.ShouldBeTrue();
		settings.Performance.SamplingRate.ShouldBe(0.5);
		settings.Health.MonitorNodeHealth.ShouldBeTrue();
		settings.Tracing.RecordRequestResponse.ShouldBeTrue();
	}

	#endregion

	#region Composition Tests

	[Fact]
	public void NestedSettings_AreIndependentInstances()
	{
		// Arrange
		var settings1 = new ElasticsearchMonitoringOptions();
		var settings2 = new ElasticsearchMonitoringOptions();

		// Assert - Each instance should have its own nested settings
		settings1.Metrics.ShouldNotBeSameAs(settings2.Metrics);
		settings1.RequestLogging.ShouldNotBeSameAs(settings2.RequestLogging);
		settings1.Performance.ShouldNotBeSameAs(settings2.Performance);
		settings1.Health.ShouldNotBeSameAs(settings2.Health);
		settings1.Tracing.ShouldNotBeSameAs(settings2.Tracing);
	}

	#endregion
}
