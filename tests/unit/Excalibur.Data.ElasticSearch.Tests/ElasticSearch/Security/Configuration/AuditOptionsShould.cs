// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.ElasticSearch.Security;

namespace Excalibur.Data.Tests.ElasticSearch.Security.Configuration;

/// <summary>
/// Unit tests for the <see cref="AuditOptions"/> class.
/// </summary>
/// <remarks>
/// Sprint 512 (S512.2): Elasticsearch compliance unit tests.
/// Tests verify default values and configuration for GDPR/HIPAA/PCI-DSS compliance.
/// </remarks>
[Trait("Category", TestCategories.Unit)]
[Trait("Component", "Elasticsearch")]
[Trait("Feature", "Security")]
public sealed class AuditOptionsShould
{
	#region Default Values Tests

	[Fact]
	public void HaveEnabledAsTrue_ByDefault()
	{
		// Act
		var settings = new AuditOptions();

		// Assert
		settings.Enabled.ShouldBeTrue();
	}

	[Fact]
	public void HaveAuditAuthenticationAsTrue_ByDefault()
	{
		// Act
		var settings = new AuditOptions();

		// Assert
		settings.AuditAuthentication.ShouldBeTrue();
	}

	[Fact]
	public void HaveAuditDataAccessAsTrue_ByDefault()
	{
		// Act
		var settings = new AuditOptions();

		// Assert
		settings.AuditDataAccess.ShouldBeTrue();
	}

	[Fact]
	public void HaveAuditConfigurationChangesAsTrue_ByDefault()
	{
		// Act
		var settings = new AuditOptions();

		// Assert
		settings.AuditConfigurationChanges.ShouldBeTrue();
	}

	[Fact]
	public void HaveEnsureLogIntegrityAsTrue_ByDefault()
	{
		// Act
		var settings = new AuditOptions();

		// Assert
		settings.EnsureLogIntegrity.ShouldBeTrue();
	}

	[Fact]
	public void HaveEmptyComplianceFrameworks_ByDefault()
	{
		// Act
		var settings = new AuditOptions();

		// Assert
		settings.ComplianceFrameworks.ShouldBeEmpty();
	}

	#endregion

	#region Init Property Tests

	[Fact]
	public void AllowSettingEnabled_ViaInitializer()
	{
		// Act
		var settings = new AuditOptions { Enabled = false };

		// Assert
		settings.Enabled.ShouldBeFalse();
	}

	[Fact]
	public void AllowSettingAuditAuthentication_ViaInitializer()
	{
		// Act
		var settings = new AuditOptions { AuditAuthentication = false };

		// Assert
		settings.AuditAuthentication.ShouldBeFalse();
	}

	[Fact]
	public void AllowSettingAuditDataAccess_ViaInitializer()
	{
		// Act
		var settings = new AuditOptions { AuditDataAccess = false };

		// Assert
		settings.AuditDataAccess.ShouldBeFalse();
	}

	[Fact]
	public void AllowSettingAuditConfigurationChanges_ViaInitializer()
	{
		// Act
		var settings = new AuditOptions { AuditConfigurationChanges = false };

		// Assert
		settings.AuditConfigurationChanges.ShouldBeFalse();
	}

	[Fact]
	public void AllowSettingEnsureLogIntegrity_ViaInitializer()
	{
		// Act
		var settings = new AuditOptions { EnsureLogIntegrity = false };

		// Assert
		settings.EnsureLogIntegrity.ShouldBeFalse();
	}

	#endregion

	#region Compliance Framework Configuration Tests

	[Fact]
	public void AllowAddingSingleComplianceFramework()
	{
		// Act
		var settings = new AuditOptions
		{
			ComplianceFrameworks = [ComplianceFramework.Gdpr]
		};

		// Assert
		settings.ComplianceFrameworks.Count.ShouldBe(1);
		settings.ComplianceFrameworks.ShouldContain(ComplianceFramework.Gdpr);
	}

	[Fact]
	public void AllowAddingMultipleComplianceFrameworks()
	{
		// Act
		var settings = new AuditOptions
		{
			ComplianceFrameworks =
			[
				ComplianceFramework.Gdpr,
				ComplianceFramework.Hipaa,
				ComplianceFramework.PciDss,
				ComplianceFramework.Sox,
				ComplianceFramework.Iso27001,
				ComplianceFramework.NistCsf,
				ComplianceFramework.Fisma
			]
		};

		// Assert
		settings.ComplianceFrameworks.Count.ShouldBe(7);
	}

	[Fact]
	public void SupportGdprFramework_ForEuDataProtection()
	{
		// Act
		var settings = new AuditOptions
		{
			ComplianceFrameworks = [ComplianceFramework.Gdpr]
		};

		// Assert
		settings.ComplianceFrameworks.ShouldContain(ComplianceFramework.Gdpr);
	}

	[Fact]
	public void SupportHipaaFramework_ForHealthcareCompliance()
	{
		// Act
		var settings = new AuditOptions
		{
			ComplianceFrameworks = [ComplianceFramework.Hipaa]
		};

		// Assert
		settings.ComplianceFrameworks.ShouldContain(ComplianceFramework.Hipaa);
	}

	[Fact]
	public void SupportPciDssFramework_ForPaymentCardCompliance()
	{
		// Act
		var settings = new AuditOptions
		{
			ComplianceFrameworks = [ComplianceFramework.PciDss]
		};

		// Assert
		settings.ComplianceFrameworks.ShouldContain(ComplianceFramework.PciDss);
	}

	[Fact]
	public void SupportSoxFramework_ForFinancialCompliance()
	{
		// Act
		var settings = new AuditOptions
		{
			ComplianceFrameworks = [ComplianceFramework.Sox]
		};

		// Assert
		settings.ComplianceFrameworks.ShouldContain(ComplianceFramework.Sox);
	}

	[Fact]
	public void SupportIso27001Framework_ForInformationSecurityManagement()
	{
		// Act
		var settings = new AuditOptions
		{
			ComplianceFrameworks = [ComplianceFramework.Iso27001]
		};

		// Assert
		settings.ComplianceFrameworks.ShouldContain(ComplianceFramework.Iso27001);
	}

	[Fact]
	public void SupportNistCsfFramework_ForCybersecurityFramework()
	{
		// Act
		var settings = new AuditOptions
		{
			ComplianceFrameworks = [ComplianceFramework.NistCsf]
		};

		// Assert
		settings.ComplianceFrameworks.ShouldContain(ComplianceFramework.NistCsf);
	}

	[Fact]
	public void SupportFismaFramework_ForFederalCompliance()
	{
		// Act
		var settings = new AuditOptions
		{
			ComplianceFrameworks = [ComplianceFramework.Fisma]
		};

		// Assert
		settings.ComplianceFrameworks.ShouldContain(ComplianceFramework.Fisma);
	}

	#endregion

	#region Complete Configuration Tests

	[Fact]
	public void AllowFullConfigurationOverride()
	{
		// Act
		var settings = new AuditOptions
		{
			Enabled = true,
			AuditAuthentication = true,
			AuditDataAccess = true,
			AuditConfigurationChanges = true,
			EnsureLogIntegrity = true,
			ComplianceFrameworks = [ComplianceFramework.Gdpr, ComplianceFramework.Hipaa]
		};

		// Assert
		settings.Enabled.ShouldBeTrue();
		settings.AuditAuthentication.ShouldBeTrue();
		settings.AuditDataAccess.ShouldBeTrue();
		settings.AuditConfigurationChanges.ShouldBeTrue();
		settings.EnsureLogIntegrity.ShouldBeTrue();
		settings.ComplianceFrameworks.Count.ShouldBe(2);
	}

	[Fact]
	public void AllowMinimalDisabledConfiguration()
	{
		// Act
		var settings = new AuditOptions
		{
			Enabled = false,
			AuditAuthentication = false,
			AuditDataAccess = false,
			AuditConfigurationChanges = false,
			EnsureLogIntegrity = false
		};

		// Assert
		settings.Enabled.ShouldBeFalse();
		settings.AuditAuthentication.ShouldBeFalse();
		settings.AuditDataAccess.ShouldBeFalse();
		settings.AuditConfigurationChanges.ShouldBeFalse();
		settings.EnsureLogIntegrity.ShouldBeFalse();
	}

	#endregion
}
