using Excalibur.Dispatch.Compliance;

namespace Excalibur.Dispatch.Compliance.Tests.Soc2;

[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class TrustServicesCriteriaExtensionsShould
{
	[Theory]
	[InlineData(TrustServicesCriterion.CC1_ControlEnvironment, TrustServicesCategory.Security)]
	[InlineData(TrustServicesCriterion.CC5_ControlActivities, TrustServicesCategory.Security)]
	[InlineData(TrustServicesCriterion.CC9_RiskMitigation, TrustServicesCategory.Security)]
	[InlineData(TrustServicesCriterion.A1_InfrastructureManagement, TrustServicesCategory.Availability)]
	[InlineData(TrustServicesCriterion.A3_BackupRecovery, TrustServicesCategory.Availability)]
	[InlineData(TrustServicesCriterion.PI1_InputValidation, TrustServicesCategory.ProcessingIntegrity)]
	[InlineData(TrustServicesCriterion.PI3_OutputCompleteness, TrustServicesCategory.ProcessingIntegrity)]
	[InlineData(TrustServicesCriterion.C1_DataClassification, TrustServicesCategory.Confidentiality)]
	[InlineData(TrustServicesCriterion.C3_DataDisposal, TrustServicesCategory.Confidentiality)]
	[InlineData(TrustServicesCriterion.P1_Notice, TrustServicesCategory.Privacy)]
	[InlineData(TrustServicesCriterion.P8_MonitoringEnforcement, TrustServicesCategory.Privacy)]
	public void Map_criterion_to_correct_category(TrustServicesCriterion criterion, TrustServicesCategory expected)
	{
		criterion.GetCategory().ShouldBe(expected);
	}

	[Fact]
	public void Throw_for_invalid_criterion_in_get_category()
	{
		var invalidCriterion = (TrustServicesCriterion)999;

		Should.Throw<ArgumentOutOfRangeException>(
			() => invalidCriterion.GetCategory());
	}

	[Theory]
	[InlineData(TrustServicesCriterion.CC1_ControlEnvironment, "CC1 - Control Environment")]
	[InlineData(TrustServicesCriterion.CC2_Communication, "CC2 - Communication and Information")]
	[InlineData(TrustServicesCriterion.CC3_RiskAssessment, "CC3 - Risk Assessment")]
	[InlineData(TrustServicesCriterion.CC4_Monitoring, "CC4 - Monitoring Activities")]
	[InlineData(TrustServicesCriterion.CC5_ControlActivities, "CC5 - Control Activities")]
	[InlineData(TrustServicesCriterion.CC6_LogicalAccess, "CC6 - Logical and Physical Access")]
	[InlineData(TrustServicesCriterion.CC7_SystemOperations, "CC7 - System Operations")]
	[InlineData(TrustServicesCriterion.CC8_ChangeManagement, "CC8 - Change Management")]
	[InlineData(TrustServicesCriterion.CC9_RiskMitigation, "CC9 - Risk Mitigation")]
	[InlineData(TrustServicesCriterion.A1_InfrastructureManagement, "A1 - Infrastructure Management")]
	[InlineData(TrustServicesCriterion.A2_CapacityManagement, "A2 - Capacity Management")]
	[InlineData(TrustServicesCriterion.A3_BackupRecovery, "A3 - Backup and Recovery")]
	[InlineData(TrustServicesCriterion.PI1_InputValidation, "PI1 - Input Validation")]
	[InlineData(TrustServicesCriterion.PI2_ProcessingAccuracy, "PI2 - Processing Accuracy")]
	[InlineData(TrustServicesCriterion.PI3_OutputCompleteness, "PI3 - Output Completeness")]
	[InlineData(TrustServicesCriterion.C1_DataClassification, "C1 - Data Classification")]
	[InlineData(TrustServicesCriterion.C2_DataProtection, "C2 - Data Protection")]
	[InlineData(TrustServicesCriterion.C3_DataDisposal, "C3 - Data Disposal")]
	[InlineData(TrustServicesCriterion.P1_Notice, "P1 - Notice")]
	[InlineData(TrustServicesCriterion.P2_ChoiceConsent, "P2 - Choice and Consent")]
	[InlineData(TrustServicesCriterion.P3_Collection, "P3 - Collection")]
	[InlineData(TrustServicesCriterion.P4_UseRetention, "P4 - Use and Retention")]
	[InlineData(TrustServicesCriterion.P5_Access, "P5 - Access")]
	[InlineData(TrustServicesCriterion.P6_Disclosure, "P6 - Disclosure")]
	[InlineData(TrustServicesCriterion.P7_Quality, "P7 - Quality")]
	[InlineData(TrustServicesCriterion.P8_MonitoringEnforcement, "P8 - Monitoring and Enforcement")]
	public void Return_correct_display_name(TrustServicesCriterion criterion, string expectedName)
	{
		criterion.GetDisplayName().ShouldBe(expectedName);
	}

	[Fact]
	public void Return_enum_name_for_unknown_criterion_display_name()
	{
		var invalidCriterion = (TrustServicesCriterion)999;

		invalidCriterion.GetDisplayName().ShouldBe("999");
	}

	[Fact]
	public void Security_category_has_nine_criteria()
	{
		var criteria = TrustServicesCategory.Security.GetCriteria().ToList();

		criteria.Count.ShouldBe(9);
		criteria.ShouldContain(TrustServicesCriterion.CC1_ControlEnvironment);
		criteria.ShouldContain(TrustServicesCriterion.CC9_RiskMitigation);
	}

	[Fact]
	public void Availability_category_has_three_criteria()
	{
		var criteria = TrustServicesCategory.Availability.GetCriteria().ToList();

		criteria.Count.ShouldBe(3);
		criteria.ShouldContain(TrustServicesCriterion.A1_InfrastructureManagement);
		criteria.ShouldContain(TrustServicesCriterion.A3_BackupRecovery);
	}

	[Fact]
	public void Processing_integrity_category_has_three_criteria()
	{
		var criteria = TrustServicesCategory.ProcessingIntegrity.GetCriteria().ToList();

		criteria.Count.ShouldBe(3);
		criteria.ShouldContain(TrustServicesCriterion.PI1_InputValidation);
		criteria.ShouldContain(TrustServicesCriterion.PI3_OutputCompleteness);
	}

	[Fact]
	public void Confidentiality_category_has_three_criteria()
	{
		var criteria = TrustServicesCategory.Confidentiality.GetCriteria().ToList();

		criteria.Count.ShouldBe(3);
		criteria.ShouldContain(TrustServicesCriterion.C1_DataClassification);
		criteria.ShouldContain(TrustServicesCriterion.C3_DataDisposal);
	}

	[Fact]
	public void Privacy_category_has_eight_criteria()
	{
		var criteria = TrustServicesCategory.Privacy.GetCriteria().ToList();

		criteria.Count.ShouldBe(8);
		criteria.ShouldContain(TrustServicesCriterion.P1_Notice);
		criteria.ShouldContain(TrustServicesCriterion.P8_MonitoringEnforcement);
	}

	[Fact]
	public void Throw_for_invalid_category_in_get_criteria()
	{
		var invalidCategory = (TrustServicesCategory)999;

		Should.Throw<ArgumentOutOfRangeException>(
			() => invalidCategory.GetCriteria().ToList());
	}

	[Fact]
	public void All_criteria_map_to_a_valid_category()
	{
		foreach (var criterion in Enum.GetValues<TrustServicesCriterion>())
		{
			var category = criterion.GetCategory();
			Enum.IsDefined(category).ShouldBeTrue($"Criterion {criterion} mapped to undefined category {category}");
		}
	}

	[Fact]
	public void All_categories_return_criteria()
	{
		foreach (var category in Enum.GetValues<TrustServicesCategory>())
		{
			var criteria = category.GetCriteria().ToList();
			criteria.ShouldNotBeEmpty($"Category {category} should have at least one criterion");

			// All returned criteria should belong to this category
			foreach (var criterion in criteria)
			{
				criterion.GetCategory().ShouldBe(category);
			}
		}
	}

	[Fact]
	public void Total_criteria_count_is_twenty_six()
	{
		var totalCount = Enum.GetValues<TrustServicesCategory>()
			.SelectMany(c => c.GetCriteria())
			.Distinct()
			.Count();

		totalCount.ShouldBe(26);
	}
}
