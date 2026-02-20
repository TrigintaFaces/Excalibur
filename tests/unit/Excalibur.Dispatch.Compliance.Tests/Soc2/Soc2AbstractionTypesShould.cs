using Excalibur.Dispatch.Compliance;

namespace Excalibur.Dispatch.Compliance.Tests.Soc2;

[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class Soc2AbstractionTypesShould
{
	[Fact]
	public void Create_soc2_options_with_defaults()
	{
		var options = new Soc2Options();

		options.EnabledCategories.ShouldNotBeNull();
		options.MinimumTypeIIPeriodDays.ShouldBeGreaterThan(0);
		options.DefaultTestSampleSize.ShouldBeGreaterThan(0);
		options.SystemDescription.ShouldBeNull();
	}

	[Fact]
	public void Create_soc2_options_with_custom_values()
	{
		var options = new Soc2Options
		{
			EnabledCategories = [TrustServicesCategory.Security, TrustServicesCategory.Availability],
			MinimumTypeIIPeriodDays = 180,
			DefaultTestSampleSize = 50,
			SystemDescription = new SystemDescription
			{
				Name = "Test",
				Description = "Desc",
				Services = ["Svc"],
				Infrastructure = ["Infra"],
				DataTypes = ["Data"]
			}
		};

		options.EnabledCategories.Length.ShouldBe(2);
		options.MinimumTypeIIPeriodDays.ShouldBe(180);
		options.DefaultTestSampleSize.ShouldBe(50);
		options.SystemDescription.ShouldNotBeNull();
		options.SystemDescription.Name.ShouldBe("Test");
	}

	[Fact]
	public void Create_report_filter_with_defaults()
	{
		var filter = new ReportFilter();

		filter.ReportType.ShouldBeNull();
		filter.TenantId.ShouldBeNull();
		filter.GeneratedAfter.ShouldBeNull();
		filter.GeneratedBefore.ShouldBeNull();
		filter.PeriodStartAfter.ShouldBeNull();
		filter.PeriodEndBefore.ShouldBeNull();
		filter.Opinion.ShouldBeNull();
		filter.MaxResults.ShouldBeNull();
		filter.Skip.ShouldBeNull();
		filter.SortOrder.ShouldBe(ReportSortOrder.GeneratedAtDescending);
	}

	[Fact]
	public void Create_report_filter_with_all_properties()
	{
		var now = DateTimeOffset.UtcNow;
		var filter = new ReportFilter
		{
			ReportType = Soc2ReportType.TypeII,
			TenantId = "tenant-1",
			GeneratedAfter = now.AddDays(-30),
			GeneratedBefore = now,
			PeriodStartAfter = now.AddDays(-365),
			PeriodEndBefore = now,
			Opinion = AuditorOpinion.Unqualified,
			MaxResults = 10,
			Skip = 5,
			SortOrder = ReportSortOrder.PeriodEndAscending
		};

		filter.ReportType.ShouldBe(Soc2ReportType.TypeII);
		filter.TenantId.ShouldBe("tenant-1");
		filter.GeneratedAfter.ShouldNotBeNull();
		filter.GeneratedBefore.ShouldNotBeNull();
		filter.MaxResults.ShouldBe(10);
		filter.Skip.ShouldBe(5);
		filter.SortOrder.ShouldBe(ReportSortOrder.PeriodEndAscending);
	}

	[Fact]
	public void Create_report_summary_with_required_properties()
	{
		var summary = new ReportSummary
		{
			ReportId = Guid.NewGuid(),
			ReportType = Soc2ReportType.TypeI,
			Title = "Test Report",
			PeriodStart = DateTimeOffset.UtcNow.AddDays(-30),
			PeriodEnd = DateTimeOffset.UtcNow,
			GeneratedAt = DateTimeOffset.UtcNow,
			Opinion = AuditorOpinion.Unqualified,
			ExceptionCount = 0,
			CategoriesIncluded = [TrustServicesCategory.Security],
			TenantId = "tenant-1"
		};

		summary.ReportType.ShouldBe(Soc2ReportType.TypeI);
		summary.Title.ShouldBe("Test Report");
		summary.ExceptionCount.ShouldBe(0);
		summary.TenantId.ShouldBe("tenant-1");
	}

	[Fact]
	public void Create_compliance_status_with_all_properties()
	{
		var status = new ComplianceStatus
		{
			OverallLevel = ComplianceLevel.FullyCompliant,
			CategoryStatuses = new Dictionary<TrustServicesCategory, CategoryStatus>
			{
				[TrustServicesCategory.Security] = new CategoryStatus
				{
					Category = TrustServicesCategory.Security,
					Level = ComplianceLevel.FullyCompliant,
					CompliancePercentage = 100,
					ActiveControls = 5,
					ControlsWithIssues = 0
				}
			},
			CriterionStatuses = new Dictionary<TrustServicesCriterion, CriterionStatus>
			{
				[TrustServicesCriterion.CC1_ControlEnvironment] = new CriterionStatus
				{
					Criterion = TrustServicesCriterion.CC1_ControlEnvironment,
					IsMet = true,
					EffectivenessScore = 95,
					LastValidated = DateTimeOffset.UtcNow,
					EvidenceCount = 10,
					Gaps = []
				}
			},
			ActiveGaps = [],
			TenantId = "tenant-1"
		};

		status.OverallLevel.ShouldBe(ComplianceLevel.FullyCompliant);
		status.CategoryStatuses.Count.ShouldBe(1);
		status.CriterionStatuses.Count.ShouldBe(1);
		status.ActiveGaps.ShouldBeEmpty();
		status.TenantId.ShouldBe("tenant-1");
		status.EvaluatedAt.ShouldNotBe(default);
	}

	[Fact]
	public void Create_compliance_gap_with_all_properties()
	{
		var gap = new ComplianceGap
		{
			GapId = "GAP-001",
			Criterion = TrustServicesCriterion.CC6_LogicalAccess,
			Description = "Missing encryption",
			Severity = GapSeverity.High,
			Remediation = "Enable encryption at rest",
			IdentifiedAt = DateTimeOffset.UtcNow,
			TargetRemediationDate = DateTimeOffset.UtcNow.AddDays(30)
		};

		gap.GapId.ShouldBe("GAP-001");
		gap.Criterion.ShouldBe(TrustServicesCriterion.CC6_LogicalAccess);
		gap.Severity.ShouldBe(GapSeverity.High);
		gap.TargetRemediationDate.ShouldNotBeNull();
	}

	[Fact]
	public void Create_control_validation_result_with_defaults()
	{
		var result = new ControlValidationResult
		{
			ControlId = "SEC-001",
			IsConfigured = true,
			IsEffective = true,
			EffectivenessScore = 95
		};

		result.ConfigurationIssues.ShouldBeEmpty();
		result.Evidence.ShouldBeEmpty();
		result.ValidatedAt.ShouldNotBe(default);
	}

	[Fact]
	public void Create_control_test_parameters_with_defaults()
	{
		var parameters = new ControlTestParameters();

		parameters.SampleSize.ShouldBe(25);
		parameters.IncludeDetailedEvidence.ShouldBeTrue();
	}

	[Fact]
	public void Create_control_test_result_with_all_properties()
	{
		var result = new ControlTestResult
		{
			ControlId = "SEC-001",
			Parameters = new ControlTestParameters { SampleSize = 50 },
			ItemsTested = 50,
			ExceptionsFound = 2,
			Outcome = TestOutcome.MinorExceptions,
			Exceptions =
			[
				new TestException
				{
					ItemId = "item-1",
					Description = "Test failure",
					Severity = GapSeverity.Low,
					OccurredAt = DateTimeOffset.UtcNow
				}
			]
		};

		result.ControlId.ShouldBe("SEC-001");
		result.ItemsTested.ShouldBe(50);
		result.ExceptionsFound.ShouldBe(2);
		result.Outcome.ShouldBe(TestOutcome.MinorExceptions);
		result.Exceptions.ShouldHaveSingleItem();
		result.Evidence.ShouldBeEmpty();
	}

	[Fact]
	public void Create_report_options_with_defaults()
	{
		var options = new ReportOptions();

		options.Categories.ShouldBeNull();
		options.IncludeDetailedEvidence.ShouldBeTrue();
		options.IncludeTestResults.ShouldBeTrue();
		options.TenantId.ShouldBeNull();
		options.CustomTitle.ShouldBeNull();
		options.IncludeManagementAssertion.ShouldBeTrue();
		options.IncludeSystemDescription.ShouldBeTrue();
		options.MaxEvidenceItemsPerCriterion.ShouldBeNull();
	}

	[Fact]
	public void Create_system_description_with_defaults()
	{
		var desc = new SystemDescription
		{
			Name = "Test",
			Description = "Desc",
			Services = ["Svc"],
			Infrastructure = ["Infra"],
			DataTypes = ["Data"]
		};

		desc.ThirdParties.ShouldBeEmpty();
	}

	[Fact]
	public void Create_control_section_with_test_results()
	{
		var section = new ControlSection
		{
			Criterion = TrustServicesCriterion.CC1_ControlEnvironment,
			Description = "Control Environment",
			Controls = [],
			TestResults =
			[
				new TestResult
				{
					ControlId = "ctrl-1",
					TestProcedure = "Test proc",
					SampleSize = 25,
					ExceptionsFound = 0,
					Outcome = TestOutcome.NoExceptions,
					Notes = "All passed"
				}
			],
			IsMet = true
		};

		section.TestResults.ShouldNotBeNull();
		section.TestResults.ShouldHaveSingleItem();
		section.TestResults[0].Notes.ShouldBe("All passed");
	}

	[Fact]
	public void Create_control_description_with_all_properties()
	{
		var desc = new ControlDescription
		{
			ControlId = "SEC-001",
			Name = "Encryption at Rest",
			Description = "AES-256-GCM encryption",
			Implementation = "Using Azure Key Vault",
			Type = ControlType.Preventive,
			Frequency = ControlFrequency.Continuous
		};

		desc.ControlId.ShouldBe("SEC-001");
		desc.Type.ShouldBe(ControlType.Preventive);
		desc.Frequency.ShouldBe(ControlFrequency.Continuous);
	}

	[Fact]
	public void Create_report_exception_with_all_properties()
	{
		var exception = new ReportException
		{
			ExceptionId = "EXC-001",
			Criterion = TrustServicesCriterion.CC6_LogicalAccess,
			ControlId = "SEC-001",
			Description = "Encryption key rotation overdue",
			ManagementResponse = "Will remediate",
			RemediationPlan = "Rotate keys within 30 days"
		};

		exception.ExceptionId.ShouldBe("EXC-001");
		exception.ManagementResponse.ShouldBe("Will remediate");
		exception.RemediationPlan.ShouldBe("Rotate keys within 30 days");
	}

	[Fact]
	public void Enumerate_all_soc2_report_types()
	{
		var types = Enum.GetValues<Soc2ReportType>();

		types.Length.ShouldBe(2);
		types.ShouldContain(Soc2ReportType.TypeI);
		types.ShouldContain(Soc2ReportType.TypeII);
	}

	[Fact]
	public void Enumerate_all_control_types()
	{
		var types = Enum.GetValues<ControlType>();

		types.Length.ShouldBe(3);
		types.ShouldContain(ControlType.Preventive);
		types.ShouldContain(ControlType.Detective);
		types.ShouldContain(ControlType.Corrective);
	}

	[Fact]
	public void Enumerate_all_control_frequencies()
	{
		var frequencies = Enum.GetValues<ControlFrequency>();

		frequencies.Length.ShouldBe(8);
	}

	[Fact]
	public void Enumerate_all_test_outcomes()
	{
		var outcomes = Enum.GetValues<TestOutcome>();

		outcomes.Length.ShouldBe(4);
	}

	[Fact]
	public void Enumerate_all_auditor_opinions()
	{
		var opinions = Enum.GetValues<AuditorOpinion>();

		opinions.Length.ShouldBe(4);
	}

	[Fact]
	public void Enumerate_all_gap_severities()
	{
		var severities = Enum.GetValues<GapSeverity>();

		severities.Length.ShouldBe(4);
	}

	[Fact]
	public void Enumerate_all_compliance_levels()
	{
		var levels = Enum.GetValues<ComplianceLevel>();

		levels.Length.ShouldBe(5);
	}

	[Fact]
	public void Enumerate_all_report_sort_orders()
	{
		var orders = Enum.GetValues<ReportSortOrder>();

		orders.Length.ShouldBe(4);
	}
}
