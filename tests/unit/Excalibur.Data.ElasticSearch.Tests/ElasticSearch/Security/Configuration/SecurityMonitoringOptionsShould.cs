// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.ElasticSearch.Security;

namespace Excalibur.Data.Tests.ElasticSearch.Security.Configuration;

/// <summary>
/// Unit tests for the <see cref="SecurityMonitoringOptions"/> class.
/// </summary>
/// <remarks>
/// Sprint 512 (S512.2): Elasticsearch compliance unit tests.
/// Tests verify security monitoring configuration for threat detection and alerting.
/// </remarks>
[Trait("Category", TestCategories.Unit)]
[Trait("Component", "Elasticsearch")]
[Trait("Feature", "Security")]
public sealed class SecurityMonitoringOptionsShould
{
	#region Default Values Tests

	[Fact]
	public void HaveEnabledAsTrue_ByDefault()
	{
		// Act
		var settings = new SecurityMonitoringOptions();

		// Assert
		settings.Enabled.ShouldBeTrue();
	}

	[Fact]
	public void HaveDetectAnomaliesAsTrue_ByDefault()
	{
		// Act
		var settings = new SecurityMonitoringOptions();

		// Assert
		settings.DetectAnomalies.ShouldBeTrue();
	}

	[Fact]
	public void HaveMonitorAuthenticationAttacksAsTrue_ByDefault()
	{
		// Act
		var settings = new SecurityMonitoringOptions();

		// Assert
		settings.MonitorAuthenticationAttacks.ShouldBeTrue();
	}

	[Fact]
	public void HaveDetectDataExfiltrationAsTrue_ByDefault()
	{
		// Act
		var settings = new SecurityMonitoringOptions();

		// Assert
		settings.DetectDataExfiltration.ShouldBeTrue();
	}

	[Fact]
	public void HaveAutomatedResponseEnabledAsFalse_ByDefault()
	{
		// Act
		var settings = new SecurityMonitoringOptions();

		// Assert
		settings.AutomatedResponseEnabled.ShouldBeFalse();
	}

	[Fact]
	public void HaveStoreAlertsInElasticsearchAsTrue_ByDefault()
	{
		// Act
		var settings = new SecurityMonitoringOptions();

		// Assert
		settings.StoreAlertsInElasticsearch.ShouldBeTrue();
	}

	[Fact]
	public void HaveFiveMinuteMonitoringInterval_ByDefault()
	{
		// Act
		var settings = new SecurityMonitoringOptions();

		// Assert
		settings.MonitoringInterval.ShouldBe(TimeSpan.FromMinutes(5));
	}

	[Fact]
	public void HaveFailedLoginThresholdOfFive_ByDefault()
	{
		// Act
		var settings = new SecurityMonitoringOptions();

		// Assert
		settings.FailedLoginThreshold.ShouldBe(5);
	}

	[Fact]
	public void HaveDefaultAlertingSettings_ByDefault()
	{
		// Act
		var settings = new SecurityMonitoringOptions();

		// Assert
		_ = settings.Alerting.ShouldNotBeNull();
	}

	[Fact]
	public void HaveDefaultThreatIntelligenceSettings_ByDefault()
	{
		// Act
		var settings = new SecurityMonitoringOptions();

		// Assert
		_ = settings.ThreatIntelligence.ShouldNotBeNull();
	}

	#endregion

	#region Init Property Tests

	[Fact]
	public void AllowSettingEnabled_ViaInitializer()
	{
		// Act
		var settings = new SecurityMonitoringOptions { Enabled = false };

		// Assert
		settings.Enabled.ShouldBeFalse();
	}

	[Fact]
	public void AllowSettingDetectAnomalies_ViaInitializer()
	{
		// Act
		var settings = new SecurityMonitoringOptions { DetectAnomalies = false };

		// Assert
		settings.DetectAnomalies.ShouldBeFalse();
	}

	[Fact]
	public void AllowSettingMonitorAuthenticationAttacks_ViaInitializer()
	{
		// Act
		var settings = new SecurityMonitoringOptions { MonitorAuthenticationAttacks = false };

		// Assert
		settings.MonitorAuthenticationAttacks.ShouldBeFalse();
	}

	[Fact]
	public void AllowSettingDetectDataExfiltration_ViaInitializer()
	{
		// Act
		var settings = new SecurityMonitoringOptions { DetectDataExfiltration = false };

		// Assert
		settings.DetectDataExfiltration.ShouldBeFalse();
	}

	[Fact]
	public void AllowSettingAutomatedResponseEnabled_ViaInitializer()
	{
		// Act
		var settings = new SecurityMonitoringOptions { AutomatedResponseEnabled = true };

		// Assert
		settings.AutomatedResponseEnabled.ShouldBeTrue();
	}

	[Fact]
	public void AllowSettingStoreAlertsInElasticsearch_ViaInitializer()
	{
		// Act
		var settings = new SecurityMonitoringOptions { StoreAlertsInElasticsearch = false };

		// Assert
		settings.StoreAlertsInElasticsearch.ShouldBeFalse();
	}

	[Fact]
	public void AllowSettingMonitoringInterval_ViaInitializer()
	{
		// Act
		var settings = new SecurityMonitoringOptions { MonitoringInterval = TimeSpan.FromMinutes(1) };

		// Assert
		settings.MonitoringInterval.ShouldBe(TimeSpan.FromMinutes(1));
	}

	[Fact]
	public void AllowSettingFailedLoginThreshold_ViaInitializer()
	{
		// Act
		var settings = new SecurityMonitoringOptions { FailedLoginThreshold = 3 };

		// Assert
		settings.FailedLoginThreshold.ShouldBe(3);
	}

	#endregion

	#region Monitoring Interval Tests

	[Fact]
	public void AllowOneMinuteMonitoringInterval_ForHighSecurityEnvironments()
	{
		// Act
		var settings = new SecurityMonitoringOptions { MonitoringInterval = TimeSpan.FromMinutes(1) };

		// Assert
		settings.MonitoringInterval.ShouldBe(TimeSpan.FromMinutes(1));
	}

	[Fact]
	public void AllowOneHourMonitoringInterval_ForLowTrafficEnvironments()
	{
		// Act
		var settings = new SecurityMonitoringOptions { MonitoringInterval = TimeSpan.FromHours(1) };

		// Assert
		settings.MonitoringInterval.ShouldBe(TimeSpan.FromHours(1));
	}

	[Fact]
	public void AllowThirtySecondMonitoringInterval_ForRealTimeMonitoring()
	{
		// Act
		var settings = new SecurityMonitoringOptions { MonitoringInterval = TimeSpan.FromSeconds(30) };

		// Assert
		settings.MonitoringInterval.ShouldBe(TimeSpan.FromSeconds(30));
	}

	#endregion

	#region Failed Login Threshold Tests

	[Fact]
	public void AllowLowFailedLoginThreshold_ForHighSecurityEnvironments()
	{
		// Act - Lock after 3 failed attempts
		var settings = new SecurityMonitoringOptions { FailedLoginThreshold = 3 };

		// Assert
		settings.FailedLoginThreshold.ShouldBe(3);
	}

	[Fact]
	public void AllowHighFailedLoginThreshold_ForUserFriendlyEnvironments()
	{
		// Act - Lock after 10 failed attempts
		var settings = new SecurityMonitoringOptions { FailedLoginThreshold = 10 };

		// Assert
		settings.FailedLoginThreshold.ShouldBe(10);
	}

	[Fact]
	public void AllowZeroFailedLoginThreshold_ForNoLockout()
	{
		// Act - No lockout (disabled)
		var settings = new SecurityMonitoringOptions { FailedLoginThreshold = 0 };

		// Assert
		settings.FailedLoginThreshold.ShouldBe(0);
	}

	#endregion

	#region Complete Configuration Tests

	[Fact]
	public void AllowFullSecurityConfiguration()
	{
		// Act
		var settings = new SecurityMonitoringOptions
		{
			Enabled = true,
			DetectAnomalies = true,
			MonitorAuthenticationAttacks = true,
			DetectDataExfiltration = true,
			AutomatedResponseEnabled = true,
			StoreAlertsInElasticsearch = true,
			MonitoringInterval = TimeSpan.FromMinutes(1),
			FailedLoginThreshold = 3
		};

		// Assert
		settings.Enabled.ShouldBeTrue();
		settings.DetectAnomalies.ShouldBeTrue();
		settings.MonitorAuthenticationAttacks.ShouldBeTrue();
		settings.DetectDataExfiltration.ShouldBeTrue();
		settings.AutomatedResponseEnabled.ShouldBeTrue();
		settings.StoreAlertsInElasticsearch.ShouldBeTrue();
		settings.MonitoringInterval.ShouldBe(TimeSpan.FromMinutes(1));
		settings.FailedLoginThreshold.ShouldBe(3);
	}

	[Fact]
	public void AllowMinimalDisabledConfiguration()
	{
		// Act
		var settings = new SecurityMonitoringOptions
		{
			Enabled = false,
			DetectAnomalies = false,
			MonitorAuthenticationAttacks = false,
			DetectDataExfiltration = false,
			AutomatedResponseEnabled = false,
			StoreAlertsInElasticsearch = false
		};

		// Assert
		settings.Enabled.ShouldBeFalse();
		settings.DetectAnomalies.ShouldBeFalse();
		settings.MonitorAuthenticationAttacks.ShouldBeFalse();
		settings.DetectDataExfiltration.ShouldBeFalse();
		settings.AutomatedResponseEnabled.ShouldBeFalse();
		settings.StoreAlertsInElasticsearch.ShouldBeFalse();
	}

	#endregion

	#region Threat Detection Configuration Tests

	[Fact]
	public void AllowEnablingOnlyAnomalyDetection()
	{
		// Act
		var settings = new SecurityMonitoringOptions
		{
			DetectAnomalies = true,
			MonitorAuthenticationAttacks = false,
			DetectDataExfiltration = false
		};

		// Assert
		settings.DetectAnomalies.ShouldBeTrue();
		settings.MonitorAuthenticationAttacks.ShouldBeFalse();
		settings.DetectDataExfiltration.ShouldBeFalse();
	}

	[Fact]
	public void AllowEnablingOnlyAuthenticationMonitoring()
	{
		// Act
		var settings = new SecurityMonitoringOptions
		{
			DetectAnomalies = false,
			MonitorAuthenticationAttacks = true,
			DetectDataExfiltration = false
		};

		// Assert
		settings.DetectAnomalies.ShouldBeFalse();
		settings.MonitorAuthenticationAttacks.ShouldBeTrue();
		settings.DetectDataExfiltration.ShouldBeFalse();
	}

	[Fact]
	public void AllowEnablingOnlyDataExfiltrationDetection()
	{
		// Act
		var settings = new SecurityMonitoringOptions
		{
			DetectAnomalies = false,
			MonitorAuthenticationAttacks = false,
			DetectDataExfiltration = true
		};

		// Assert
		settings.DetectAnomalies.ShouldBeFalse();
		settings.MonitorAuthenticationAttacks.ShouldBeFalse();
		settings.DetectDataExfiltration.ShouldBeTrue();
	}

	#endregion
}
