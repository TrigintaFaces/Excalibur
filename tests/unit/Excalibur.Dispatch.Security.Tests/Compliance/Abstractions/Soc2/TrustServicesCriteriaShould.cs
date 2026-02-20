// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Security.Tests.Compliance.Abstractions.Soc2;

/// <summary>
/// Unit tests for <see cref="TrustServicesCriterion"/>, <see cref="TrustServicesCategory"/>,
/// and <see cref="TrustServicesCriteriaExtensions"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
[Trait("Feature", "Soc2")]
public sealed class TrustServicesCriteriaShould : UnitTestBase
{
	[Fact]
	public void HaveAllSecurityCriteria()
	{
		// Act
		var securityCriteria = TrustServicesCategory.Security.GetCriteria().ToList();

		// Assert
		securityCriteria.Count.ShouldBe(9); // CC1-CC9
		securityCriteria.ShouldContain(TrustServicesCriterion.CC1_ControlEnvironment);
		securityCriteria.ShouldContain(TrustServicesCriterion.CC2_Communication);
		securityCriteria.ShouldContain(TrustServicesCriterion.CC3_RiskAssessment);
		securityCriteria.ShouldContain(TrustServicesCriterion.CC4_Monitoring);
		securityCriteria.ShouldContain(TrustServicesCriterion.CC5_ControlActivities);
		securityCriteria.ShouldContain(TrustServicesCriterion.CC6_LogicalAccess);
		securityCriteria.ShouldContain(TrustServicesCriterion.CC7_SystemOperations);
		securityCriteria.ShouldContain(TrustServicesCriterion.CC8_ChangeManagement);
		securityCriteria.ShouldContain(TrustServicesCriterion.CC9_RiskMitigation);
	}

	[Fact]
	public void HaveAllAvailabilityCriteria()
	{
		// Act
		var criteria = TrustServicesCategory.Availability.GetCriteria().ToList();

		// Assert
		criteria.Count.ShouldBe(3); // A1-A3
		criteria.ShouldContain(TrustServicesCriterion.A1_InfrastructureManagement);
		criteria.ShouldContain(TrustServicesCriterion.A2_CapacityManagement);
		criteria.ShouldContain(TrustServicesCriterion.A3_BackupRecovery);
	}

	[Fact]
	public void HaveAllProcessingIntegrityCriteria()
	{
		// Act
		var criteria = TrustServicesCategory.ProcessingIntegrity.GetCriteria().ToList();

		// Assert
		criteria.Count.ShouldBe(3); // PI1-PI3
		criteria.ShouldContain(TrustServicesCriterion.PI1_InputValidation);
		criteria.ShouldContain(TrustServicesCriterion.PI2_ProcessingAccuracy);
		criteria.ShouldContain(TrustServicesCriterion.PI3_OutputCompleteness);
	}

	[Fact]
	public void HaveAllConfidentialityCriteria()
	{
		// Act
		var criteria = TrustServicesCategory.Confidentiality.GetCriteria().ToList();

		// Assert
		criteria.Count.ShouldBe(3); // C1-C3
		criteria.ShouldContain(TrustServicesCriterion.C1_DataClassification);
		criteria.ShouldContain(TrustServicesCriterion.C2_DataProtection);
		criteria.ShouldContain(TrustServicesCriterion.C3_DataDisposal);
	}

	[Fact]
	public void HaveAllPrivacyCriteria()
	{
		// Act
		var criteria = TrustServicesCategory.Privacy.GetCriteria().ToList();

		// Assert
		criteria.Count.ShouldBe(8); // P1-P8
		criteria.ShouldContain(TrustServicesCriterion.P1_Notice);
		criteria.ShouldContain(TrustServicesCriterion.P2_ChoiceConsent);
		criteria.ShouldContain(TrustServicesCriterion.P3_Collection);
		criteria.ShouldContain(TrustServicesCriterion.P4_UseRetention);
		criteria.ShouldContain(TrustServicesCriterion.P5_Access);
		criteria.ShouldContain(TrustServicesCriterion.P6_Disclosure);
		criteria.ShouldContain(TrustServicesCriterion.P7_Quality);
		criteria.ShouldContain(TrustServicesCriterion.P8_MonitoringEnforcement);
	}

	[Theory]
	[InlineData(TrustServicesCriterion.CC1_ControlEnvironment, TrustServicesCategory.Security)]
	[InlineData(TrustServicesCriterion.CC6_LogicalAccess, TrustServicesCategory.Security)]
	[InlineData(TrustServicesCriterion.A1_InfrastructureManagement, TrustServicesCategory.Availability)]
	[InlineData(TrustServicesCriterion.PI1_InputValidation, TrustServicesCategory.ProcessingIntegrity)]
	[InlineData(TrustServicesCriterion.C1_DataClassification, TrustServicesCategory.Confidentiality)]
	[InlineData(TrustServicesCriterion.P1_Notice, TrustServicesCategory.Privacy)]
	public void GetCorrectCategoryForCriterion(TrustServicesCriterion criterion, TrustServicesCategory expectedCategory)
	{
		// Act
		var category = criterion.GetCategory();

		// Assert
		category.ShouldBe(expectedCategory);
	}

	[Theory]
	[InlineData(TrustServicesCriterion.CC1_ControlEnvironment, "CC1 - Control Environment")]
	[InlineData(TrustServicesCriterion.CC6_LogicalAccess, "CC6 - Logical and Physical Access")]
	[InlineData(TrustServicesCriterion.A1_InfrastructureManagement, "A1 - Infrastructure Management")]
	[InlineData(TrustServicesCriterion.PI1_InputValidation, "PI1 - Input Validation")]
	[InlineData(TrustServicesCriterion.C1_DataClassification, "C1 - Data Classification")]
	[InlineData(TrustServicesCriterion.P1_Notice, "P1 - Notice")]
	public void GetCorrectDisplayName(TrustServicesCriterion criterion, string expectedName)
	{
		// Act
		var displayName = criterion.GetDisplayName();

		// Assert
		displayName.ShouldBe(expectedName);
	}

	[Fact]
	public void Have26TotalCriteria()
	{
		// Act
		var allCriteria = Enum.GetValues<TrustServicesCriterion>();

		// Assert - 9 Security + 3 Availability + 3 Processing Integrity + 3 Confidentiality + 8 Privacy = 26
		allCriteria.Length.ShouldBe(26);
	}

	[Fact]
	public void Have5Categories()
	{
		// Act
		var allCategories = Enum.GetValues<TrustServicesCategory>();

		// Assert
		allCategories.Length.ShouldBe(5);
	}

	[Theory]
	[InlineData(TrustServicesCategory.Security, 0)]
	[InlineData(TrustServicesCategory.Availability, 1)]
	[InlineData(TrustServicesCategory.ProcessingIntegrity, 2)]
	[InlineData(TrustServicesCategory.Confidentiality, 3)]
	[InlineData(TrustServicesCategory.Privacy, 4)]
	public void HaveCorrectCategoryValues(TrustServicesCategory category, int expectedValue)
	{
		// Assert
		((int)category).ShouldBe(expectedValue);
	}

	[Fact]
	public void GetCriteriaReturnsOnlyBelongingCriteria()
	{
		// Verify each category returns only its own criteria
		foreach (var category in Enum.GetValues<TrustServicesCategory>())
		{
			var criteria = category.GetCriteria();

			foreach (var criterion in criteria)
			{
				criterion.GetCategory().ShouldBe(category,
					$"Criterion {criterion} should belong to {category}");
			}
		}
	}

	[Fact]
	public void AllCriteriaBelongToCategory()
	{
		// Every criterion should map to a valid category
		foreach (var criterion in Enum.GetValues<TrustServicesCriterion>())
		{
			var category = criterion.GetCategory();
			Enum.IsDefined(category).ShouldBeTrue(
				$"Criterion {criterion} maps to undefined category");
		}
	}
}
