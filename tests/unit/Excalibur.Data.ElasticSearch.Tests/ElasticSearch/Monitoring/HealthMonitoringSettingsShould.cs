// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.ElasticSearch.Monitoring;

namespace Excalibur.Data.Tests.ElasticSearch.Monitoring;

/// <summary>
/// Unit tests for the <see cref="HealthMonitoringOptions"/> class.
/// </summary>
/// <remarks>
/// Sprint 513 (S513.2): Elasticsearch Phase 2 unit tests.
/// Tests verify default values and configuration of health monitoring settings.
/// </remarks>
[Trait("Category", TestCategories.Unit)]
[Trait("Component", "Elasticsearch")]
[Trait("Feature", "Monitoring")]
public sealed class HealthMonitoringOptionsShould
{
	#region Default Value Tests

	[Fact]
	public void DefaultEnabled_ToTrue()
	{
		// Arrange & Act
		var settings = new HealthMonitoringOptions();

		// Assert
		settings.Enabled.ShouldBeTrue();
	}

	[Fact]
	public void DefaultHealthCheckInterval_To30Seconds()
	{
		// Arrange & Act
		var settings = new HealthMonitoringOptions();

		// Assert
		settings.HealthCheckInterval.ShouldBe(TimeSpan.FromSeconds(30));
	}

	[Fact]
	public void DefaultMonitorNodeHealth_ToFalse()
	{
		// Arrange & Act
		var settings = new HealthMonitoringOptions();

		// Assert
		settings.MonitorNodeHealth.ShouldBeFalse();
	}

	[Fact]
	public void DefaultMonitorClusterStats_ToFalse()
	{
		// Arrange & Act
		var settings = new HealthMonitoringOptions();

		// Assert
		settings.MonitorClusterStats.ShouldBeFalse();
	}

	[Fact]
	public void DefaultHealthCheckTimeout_To10Seconds()
	{
		// Arrange & Act
		var settings = new HealthMonitoringOptions();

		// Assert
		settings.HealthCheckTimeout.ShouldBe(TimeSpan.FromSeconds(10));
	}

	#endregion

	#region Property Configuration Tests

	[Fact]
	public void AllowEnabled_ToBeSetToFalse()
	{
		// Arrange & Act
		var settings = new HealthMonitoringOptions { Enabled = false };

		// Assert
		settings.Enabled.ShouldBeFalse();
	}

	[Fact]
	public void AllowCustomHealthCheckInterval()
	{
		// Arrange
		var customInterval = TimeSpan.FromMinutes(1);

		// Act
		var settings = new HealthMonitoringOptions { HealthCheckInterval = customInterval };

		// Assert
		settings.HealthCheckInterval.ShouldBe(customInterval);
	}

	[Fact]
	public void AllowMonitorNodeHealth_ToBeSetToTrue()
	{
		// Arrange & Act
		var settings = new HealthMonitoringOptions { MonitorNodeHealth = true };

		// Assert
		settings.MonitorNodeHealth.ShouldBeTrue();
	}

	[Fact]
	public void AllowMonitorClusterStats_ToBeSetToTrue()
	{
		// Arrange & Act
		var settings = new HealthMonitoringOptions { MonitorClusterStats = true };

		// Assert
		settings.MonitorClusterStats.ShouldBeTrue();
	}

	[Fact]
	public void AllowCustomHealthCheckTimeout()
	{
		// Arrange
		var customTimeout = TimeSpan.FromSeconds(30);

		// Act
		var settings = new HealthMonitoringOptions { HealthCheckTimeout = customTimeout };

		// Assert
		settings.HealthCheckTimeout.ShouldBe(customTimeout);
	}

	#endregion

	#region Instance Creation Tests

	[Fact]
	public void CreateNewInstance_WithDefaultConstructor()
	{
		// Act
		var settings = new HealthMonitoringOptions();

		// Assert
		settings.ShouldNotBeNull();
	}

	[Fact]
	public void CreateNewInstance_WithAllPropertiesConfigured()
	{
		// Arrange & Act
		var settings = new HealthMonitoringOptions
		{
			Enabled = false,
			HealthCheckInterval = TimeSpan.FromMinutes(5),
			MonitorNodeHealth = true,
			MonitorClusterStats = true,
			HealthCheckTimeout = TimeSpan.FromSeconds(60)
		};

		// Assert
		settings.Enabled.ShouldBeFalse();
		settings.HealthCheckInterval.ShouldBe(TimeSpan.FromMinutes(5));
		settings.MonitorNodeHealth.ShouldBeTrue();
		settings.MonitorClusterStats.ShouldBeTrue();
		settings.HealthCheckTimeout.ShouldBe(TimeSpan.FromSeconds(60));
	}

	#endregion

	#region TimeSpan Edge Cases

	[Fact]
	public void AllowZeroHealthCheckInterval()
	{
		// Arrange & Act
		var settings = new HealthMonitoringOptions { HealthCheckInterval = TimeSpan.Zero };

		// Assert
		settings.HealthCheckInterval.ShouldBe(TimeSpan.Zero);
	}

	[Fact]
	public void AllowLargeHealthCheckInterval()
	{
		// Arrange
		var largeInterval = TimeSpan.FromHours(24);

		// Act
		var settings = new HealthMonitoringOptions { HealthCheckInterval = largeInterval };

		// Assert
		settings.HealthCheckInterval.ShouldBe(largeInterval);
	}

	[Fact]
	public void AllowZeroHealthCheckTimeout()
	{
		// Arrange & Act
		var settings = new HealthMonitoringOptions { HealthCheckTimeout = TimeSpan.Zero };

		// Assert
		settings.HealthCheckTimeout.ShouldBe(TimeSpan.Zero);
	}

	#endregion
}
