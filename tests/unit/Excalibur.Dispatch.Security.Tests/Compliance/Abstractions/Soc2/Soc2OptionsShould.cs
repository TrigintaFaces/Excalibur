// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Security.Tests.Compliance.Abstractions.Soc2;

/// <summary>
/// Unit tests for <see cref="Soc2Options"/> and <see cref="ControlDefinition"/> classes.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
[Trait("Feature", "Soc2")]
public sealed class Soc2OptionsShould : UnitTestBase
{
	[Fact]
	public void HaveSensibleDefaults()
	{
		// Act
		var options = new Soc2Options();

		// Assert
		_ = options.EnabledCategories.ShouldNotBeNull();
		options.EnabledCategories.ShouldContain(TrustServicesCategory.Security);
		options.EnabledCategories.Length.ShouldBe(1);
		options.EnableContinuousMonitoring.ShouldBeTrue();
		options.MonitoringInterval.ShouldBe(TimeSpan.FromHours(1));
		options.EnableAlerts.ShouldBeTrue();
		options.AlertThreshold.ShouldBe(GapSeverity.Medium);
		options.EvidenceRetentionPeriod.ShouldBe(TimeSpan.FromDays(365 * 7));
		options.SystemDescription.ShouldBeNull();
		options.CustomControls.ShouldBeEmpty();
		options.DefaultTestSampleSize.ShouldBe(25);
		options.MinimumTypeIIPeriodDays.ShouldBe(90);
		options.IncludeSubServiceOrganizations.ShouldBeFalse();
	}

	[Fact]
	public void AllowConfiguringAllCategories()
	{
		// Act
		var options = new Soc2Options
		{
			EnabledCategories =
			[
				TrustServicesCategory.Security,
				TrustServicesCategory.Availability,
				TrustServicesCategory.ProcessingIntegrity,
				TrustServicesCategory.Confidentiality,
				TrustServicesCategory.Privacy
			]
		};

		// Assert
		options.EnabledCategories.Length.ShouldBe(5);
	}

	[Fact]
	public void AllowDisablingContinuousMonitoring()
	{
		// Act
		var options = new Soc2Options
		{
			EnableContinuousMonitoring = false
		};

		// Assert
		options.EnableContinuousMonitoring.ShouldBeFalse();
	}

	[Fact]
	public void AllowCustomMonitoringInterval()
	{
		// Act
		var options = new Soc2Options
		{
			MonitoringInterval = TimeSpan.FromMinutes(30)
		};

		// Assert
		options.MonitoringInterval.ShouldBe(TimeSpan.FromMinutes(30));
	}

	[Theory]
	[InlineData(GapSeverity.Low)]
	[InlineData(GapSeverity.Medium)]
	[InlineData(GapSeverity.High)]
	[InlineData(GapSeverity.Critical)]
	public void AllowAllAlertThresholds(GapSeverity threshold)
	{
		// Act
		var options = new Soc2Options
		{
			AlertThreshold = threshold
		};

		// Assert
		options.AlertThreshold.ShouldBe(threshold);
	}

	[Fact]
	public void AllowCustomRetentionPeriod()
	{
		// Act
		var options = new Soc2Options
		{
			EvidenceRetentionPeriod = TimeSpan.FromDays(365 * 10) // 10 years
		};

		// Assert
		options.EvidenceRetentionPeriod.ShouldBe(TimeSpan.FromDays(3650));
	}

	[Fact]
	public void AllowAddingCustomControls()
	{
		// Act
		var options = new Soc2Options
		{
			CustomControls =
			[
				new ControlDefinition
				{
					ControlId = "CC6.1-CUSTOM",
					Criterion = TrustServicesCriterion.CC6_LogicalAccess,
					Name = "Custom Access Control",
					Description = "Additional access control measure",
					Implementation = "RBAC with MFA"
				}
			]
		};

		// Assert
		options.CustomControls.Count.ShouldBe(1);
		options.CustomControls[0].ControlId.ShouldBe("CC6.1-CUSTOM");
	}

	[Fact]
	public void CreateValidControlDefinition()
	{
		// Act
		var control = new ControlDefinition
		{
			ControlId = "CC7.1",
			Criterion = TrustServicesCriterion.CC7_SystemOperations,
			Name = "System Operations Monitoring",
			Description = "Monitor system operations for anomalies",
			Implementation = "Azure Monitor + Datadog",
			Type = ControlType.Detective,
			Frequency = ControlFrequency.Continuous,
			ValidatorTypeName = "SystemOpsValidator"
		};

		// Assert
		control.ControlId.ShouldBe("CC7.1");
		control.Criterion.ShouldBe(TrustServicesCriterion.CC7_SystemOperations);
		control.Name.ShouldBe("System Operations Monitoring");
		control.Type.ShouldBe(ControlType.Detective);
		control.Frequency.ShouldBe(ControlFrequency.Continuous);
		control.ValidatorTypeName.ShouldBe("SystemOpsValidator");
	}

	[Fact]
	public void HaveDefaultControlTypeAndFrequency()
	{
		// Act
		var control = new ControlDefinition
		{
			ControlId = "TEST-001",
			Criterion = TrustServicesCriterion.CC1_ControlEnvironment,
			Name = "Test Control",
			Description = "Test",
			Implementation = "Test impl"
		};

		// Assert
		control.Type.ShouldBe(ControlType.Preventive);
		control.Frequency.ShouldBe(ControlFrequency.Continuous);
	}

	[Fact]
	public void AllowConfiguringSampleSize()
	{
		// Act
		var options = new Soc2Options
		{
			DefaultTestSampleSize = 50
		};

		// Assert
		options.DefaultTestSampleSize.ShouldBe(50);
	}

	[Fact]
	public void AllowConfiguringTypeIIPeriod()
	{
		// Act
		var options = new Soc2Options
		{
			MinimumTypeIIPeriodDays = 180 // 6 months
		};

		// Assert
		options.MinimumTypeIIPeriodDays.ShouldBe(180);
	}
}
