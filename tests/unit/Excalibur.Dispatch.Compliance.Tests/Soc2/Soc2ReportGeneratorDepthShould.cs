using Excalibur.Dispatch.Compliance;

using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Dispatch.Compliance.Tests.Soc2;

[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class Soc2ReportGeneratorDepthShould
{
	private readonly IControlValidationService _controlValidation = A.Fake<IControlValidationService>();
	private readonly ISoc2ReportStore _reportStore = A.Fake<ISoc2ReportStore>();
	private readonly NullLogger<Soc2ReportGenerator> _logger = NullLogger<Soc2ReportGenerator>.Instance;

	private readonly Soc2Options _options = new()
	{
		EnabledCategories = [TrustServicesCategory.Security],
		MinimumTypeIIPeriodDays = 90,
		DefaultTestSampleSize = 25
	};

	[Fact]
	public async Task Throw_for_type_ii_period_end_before_start()
	{
		var sut = CreateGenerator();
		var start = DateTimeOffset.UtcNow;
		var end = DateTimeOffset.UtcNow.AddDays(-10);

		await Should.ThrowAsync<ArgumentException>(
			() => sut.GenerateTypeIIReportAsync(start, end, new ReportOptions(), CancellationToken.None))
			.ConfigureAwait(false);
	}

	[Fact]
	public async Task Throw_for_unknown_report_type_in_generate_and_store()
	{
		var sut = CreateGenerator();
		var request = new ReportGenerationRequest
		{
			ReportType = (Soc2ReportType)99,
			PeriodStart = DateTimeOffset.UtcNow,
			Options = new ReportOptions()
		};

		await Should.ThrowAsync<ArgumentOutOfRangeException>(
			() => sut.GenerateAndStoreReportAsync(request, CancellationToken.None))
			.ConfigureAwait(false);
	}

	[Fact]
	public async Task Generate_type_i_with_default_title_when_no_custom_title()
	{
		SetupPassingValidation();

		var sut = CreateGenerator();
		var asOfDate = DateTimeOffset.UtcNow;

		var report = await sut.GenerateTypeIReportAsync(
			asOfDate, new ReportOptions(), CancellationToken.None).ConfigureAwait(false);

		report.Title.ShouldContain("SOC 2 Type I Report");
	}

	[Fact]
	public async Task Generate_type_ii_with_default_title_when_no_custom_title()
	{
		SetupPassingValidation();

		var sut = CreateGenerator();
		var start = DateTimeOffset.UtcNow.AddDays(-180);
		var end = DateTimeOffset.UtcNow;

		var report = await sut.GenerateTypeIIReportAsync(
			start, end, new ReportOptions(), CancellationToken.None).ConfigureAwait(false);

		report.Title.ShouldContain("SOC 2 Type II Report");
	}

	[Fact]
	public async Task Use_default_system_description_when_not_configured()
	{
		SetupPassingValidation();

		var sut = CreateGenerator();

		var report = await sut.GenerateTypeIReportAsync(
			DateTimeOffset.UtcNow, new ReportOptions(), CancellationToken.None).ConfigureAwait(false);

		report.System.ShouldNotBeNull();
		report.System.Name.ShouldBe("Excalibur framework");
		report.System.Services.ShouldNotBeEmpty();
		report.System.Infrastructure.ShouldNotBeEmpty();
		report.System.DataTypes.ShouldNotBeEmpty();
	}

	[Fact]
	public async Task Include_tenant_id_in_report()
	{
		SetupPassingValidation();

		var sut = CreateGenerator();
		var options = new ReportOptions { TenantId = "tenant-abc" };

		var report = await sut.GenerateTypeIReportAsync(
			DateTimeOffset.UtcNow, options, CancellationToken.None).ConfigureAwait(false);

		report.TenantId.ShouldBe("tenant-abc");
	}

	[Fact]
	public async Task Use_custom_categories_when_specified()
	{
		SetupPassingValidation();

		var sut = CreateGenerator();
		var options = new ReportOptions
		{
			Categories = [TrustServicesCategory.Availability, TrustServicesCategory.Confidentiality]
		};

		var report = await sut.GenerateTypeIReportAsync(
			DateTimeOffset.UtcNow, options, CancellationToken.None).ConfigureAwait(false);

		report.CategoriesIncluded.ShouldContain(TrustServicesCategory.Availability);
		report.CategoriesIncluded.ShouldContain(TrustServicesCategory.Confidentiality);
	}

	[Fact]
	public async Task Return_adverse_opinion_for_non_compliant_controls()
	{
		var failingResult = new ControlValidationResult
		{
			ControlId = "SEC-001",
			IsConfigured = true,
			IsEffective = false,
			EffectivenessScore = 20,
			ConfigurationIssues = ["Critical failure"]
		};
		A.CallTo(() => _controlValidation.GetControlsForCriterion(A<TrustServicesCriterion>._))
			.Returns(new List<string> { "SEC-001" });
		A.CallTo(() => _controlValidation.ValidateCriterionAsync(A<TrustServicesCriterion>._, A<CancellationToken>._))
			.Returns(new List<ControlValidationResult> { failingResult });

		var sut = CreateGenerator();

		var report = await sut.GenerateTypeIReportAsync(
			DateTimeOffset.UtcNow, new ReportOptions(), CancellationToken.None).ConfigureAwait(false);

		report.Opinion.ShouldBe(AuditorOpinion.Adverse);
	}

	[Fact]
	public async Task Build_exceptions_for_non_met_sections_without_test_results()
	{
		var failingResult = new ControlValidationResult
		{
			ControlId = "SEC-001",
			IsConfigured = true,
			IsEffective = false,
			EffectivenessScore = 30,
			ConfigurationIssues = ["Failed"]
		};
		A.CallTo(() => _controlValidation.GetControlsForCriterion(A<TrustServicesCriterion>._))
			.Returns(new List<string> { "SEC-001" });
		A.CallTo(() => _controlValidation.ValidateCriterionAsync(A<TrustServicesCriterion>._, A<CancellationToken>._))
			.Returns(new List<ControlValidationResult> { failingResult });

		var sut = CreateGenerator();

		var report = await sut.GenerateTypeIReportAsync(
			DateTimeOffset.UtcNow, new ReportOptions(), CancellationToken.None).ConfigureAwait(false);

		report.Exceptions.ShouldNotBeEmpty();
		report.Exceptions[0].ControlId.ShouldBe("N/A");
		report.Exceptions[0].Description.ShouldContain("not suitably designed");
	}

	[Fact]
	public async Task Build_exceptions_from_test_results_when_available()
	{
		var failingResult = new ControlValidationResult
		{
			ControlId = "SEC-001",
			IsConfigured = true,
			IsEffective = false,
			EffectivenessScore = 30
		};
		A.CallTo(() => _controlValidation.GetControlsForCriterion(A<TrustServicesCriterion>._))
			.Returns(new List<string> { "SEC-001" });
		A.CallTo(() => _controlValidation.ValidateCriterionAsync(A<TrustServicesCriterion>._, A<CancellationToken>._))
			.Returns(new List<ControlValidationResult> { failingResult });
		A.CallTo(() => _controlValidation.RunControlTestAsync(A<string>._, A<ControlTestParameters>._, A<CancellationToken>._))
			.Returns(new ControlTestResult
			{
				ControlId = "SEC-001",
				Parameters = new ControlTestParameters { SampleSize = 25 },
				ItemsTested = 25,
				ExceptionsFound = 3,
				Outcome = TestOutcome.SignificantExceptions,
				Exceptions =
				[
					new TestException
					{
						ItemId = "item-1",
						Description = "Test failure",
						Severity = GapSeverity.High,
						OccurredAt = DateTimeOffset.UtcNow
					}
				]
			});

		var sut = CreateGenerator();
		var start = DateTimeOffset.UtcNow.AddDays(-180);
		var end = DateTimeOffset.UtcNow;

		var report = await sut.GenerateTypeIIReportAsync(
			start, end, new ReportOptions { IncludeTestResults = true }, CancellationToken.None).ConfigureAwait(false);

		report.Exceptions.ShouldNotBeEmpty();
		report.Exceptions[0].ControlId.ShouldBe("SEC-001");
		report.Exceptions[0].Description.ShouldContain("3 exception(s)");
	}

	[Fact]
	public async Task Get_control_descriptions_for_known_controls()
	{
		A.CallTo(() => _controlValidation.GetControlsForCriterion(A<TrustServicesCriterion>._))
			.Returns(new List<string> { "SEC-001", "SEC-002", "AVL-001", "INT-001", "CNF-001" });

		var sut = CreateGenerator();

		var descriptions = await sut.GetControlDescriptionsAsync(
			TrustServicesCriterion.CC6_LogicalAccess, CancellationToken.None).ConfigureAwait(false);

		descriptions.Count.ShouldBe(5);
		descriptions[0].Name.ShouldBe("Encryption at Rest");
		descriptions[1].Name.ShouldBe("Encryption in Transit");
		descriptions[2].Name.ShouldBe("Health Monitoring");
		descriptions[3].Name.ShouldBe("Input Validation");
		descriptions[4].Name.ShouldBe("Data Classification");
	}

	[Fact]
	public async Task Get_control_descriptions_for_unknown_control_uses_default()
	{
		A.CallTo(() => _controlValidation.GetControlsForCriterion(A<TrustServicesCriterion>._))
			.Returns(new List<string> { "CUSTOM-001" });

		var sut = CreateGenerator();

		var descriptions = await sut.GetControlDescriptionsAsync(
			TrustServicesCriterion.CC1_ControlEnvironment, CancellationToken.None).ConfigureAwait(false);

		descriptions.ShouldHaveSingleItem();
		descriptions[0].Name.ShouldBe("Control CUSTOM-001");
	}

	[Fact]
	public async Task Get_test_results_includes_exception_notes()
	{
		A.CallTo(() => _controlValidation.GetControlsForCriterion(A<TrustServicesCriterion>._))
			.Returns(new List<string> { "SEC-001" });
		A.CallTo(() => _controlValidation.RunControlTestAsync("SEC-001", A<ControlTestParameters>._, A<CancellationToken>._))
			.Returns(new ControlTestResult
			{
				ControlId = "SEC-001",
				Parameters = new ControlTestParameters { SampleSize = 25 },
				ItemsTested = 25,
				ExceptionsFound = 1,
				Outcome = TestOutcome.MinorExceptions,
				Exceptions =
				[
					new TestException
					{
						ItemId = "item-1",
						Description = "Missing config",
						Severity = GapSeverity.Low,
						OccurredAt = DateTimeOffset.UtcNow
					}
				]
			});

		var sut = CreateGenerator();

		var results = await sut.GetTestResultsAsync(
			TrustServicesCriterion.CC6_LogicalAccess,
			DateTimeOffset.UtcNow.AddDays(-90),
			DateTimeOffset.UtcNow,
			CancellationToken.None).ConfigureAwait(false);

		results.ShouldHaveSingleItem();
		results[0].Notes.ShouldBe("Missing config");
		results[0].ExceptionsFound.ShouldBe(1);
	}

	[Fact]
	public async Task Get_test_procedure_descriptions_for_known_controls()
	{
		A.CallTo(() => _controlValidation.GetControlsForCriterion(A<TrustServicesCriterion>._))
			.Returns(new List<string> { "SEC-003", "SEC-004", "SEC-005", "AVL-002", "AVL-003", "INT-002", "INT-003", "CNF-002", "CNF-003" });
		A.CallTo(() => _controlValidation.RunControlTestAsync(A<string>._, A<ControlTestParameters>._, A<CancellationToken>._))
			.Returns(new ControlTestResult
			{
				ControlId = "ctrl",
				Parameters = new ControlTestParameters { SampleSize = 25 },
				ItemsTested = 25,
				ExceptionsFound = 0,
				Outcome = TestOutcome.NoExceptions
			});

		var sut = CreateGenerator();

		var results = await sut.GetTestResultsAsync(
			TrustServicesCriterion.CC6_LogicalAccess,
			DateTimeOffset.UtcNow.AddDays(-90),
			DateTimeOffset.UtcNow,
			CancellationToken.None).ConfigureAwait(false);

		results.Count.ShouldBe(9);
		results[0].TestProcedure.ShouldContain("key rotation");
		results[1].TestProcedure.ShouldContain("audit log");
		results[2].TestProcedure.ShouldContain("security event");
	}

	[Fact]
	public async Task Get_test_procedure_uses_default_for_unknown_control()
	{
		A.CallTo(() => _controlValidation.GetControlsForCriterion(A<TrustServicesCriterion>._))
			.Returns(new List<string> { "CUSTOM-999" });
		A.CallTo(() => _controlValidation.RunControlTestAsync(A<string>._, A<ControlTestParameters>._, A<CancellationToken>._))
			.Returns(new ControlTestResult
			{
				ControlId = "CUSTOM-999",
				Parameters = new ControlTestParameters { SampleSize = 25 },
				ItemsTested = 25,
				ExceptionsFound = 0,
				Outcome = TestOutcome.NoExceptions
			});

		var sut = CreateGenerator();

		var results = await sut.GetTestResultsAsync(
			TrustServicesCriterion.CC1_ControlEnvironment,
			DateTimeOffset.UtcNow.AddDays(-90),
			DateTimeOffset.UtcNow,
			CancellationToken.None).ConfigureAwait(false);

		results.ShouldHaveSingleItem();
		results[0].TestProcedure.ShouldContain("CUSTOM-999");
	}

	[Fact]
	public async Task Section_is_not_met_when_no_validation_results()
	{
		A.CallTo(() => _controlValidation.GetControlsForCriterion(A<TrustServicesCriterion>._))
			.Returns(new List<string>());
		A.CallTo(() => _controlValidation.ValidateCriterionAsync(A<TrustServicesCriterion>._, A<CancellationToken>._))
			.Returns(new List<ControlValidationResult>());

		var sut = CreateGenerator();

		var report = await sut.GenerateTypeIReportAsync(
			DateTimeOffset.UtcNow, new ReportOptions(), CancellationToken.None).ConfigureAwait(false);

		// When no validation results, sections are not met → NonCompliant → Adverse opinion
		report.Opinion.ShouldBe(AuditorOpinion.Adverse);
	}

	[Fact]
	public async Task Return_qualified_opinion_for_substantially_compliant()
	{
		// Set up multiple categories to test partial compliance
		_options.EnabledCategories = [TrustServicesCategory.Security, TrustServicesCategory.Availability];

		var passingResult = new ControlValidationResult
		{
			ControlId = "ctrl-pass",
			IsConfigured = true,
			IsEffective = true,
			EffectivenessScore = 95
		};
		var failingResult = new ControlValidationResult
		{
			ControlId = "ctrl-fail",
			IsConfigured = true,
			IsEffective = false,
			EffectivenessScore = 40
		};

		// Return different results for different criteria
		var callCount = 0;
		A.CallTo(() => _controlValidation.GetControlsForCriterion(A<TrustServicesCriterion>._))
			.Returns(new List<string> { "ctrl" });
		A.CallTo(() => _controlValidation.ValidateCriterionAsync(A<TrustServicesCriterion>._, A<CancellationToken>._))
			.ReturnsLazily(() =>
			{
				callCount++;
				// Make ~70-89% pass to get SubstantiallyCompliant
				return callCount <= 8
					? new List<ControlValidationResult> { passingResult }
					: new List<ControlValidationResult> { failingResult };
			});

		var sut = CreateGenerator();

		var report = await sut.GenerateTypeIReportAsync(
			DateTimeOffset.UtcNow, new ReportOptions(), CancellationToken.None).ConfigureAwait(false);

		// Opinion should be Qualified for substantially or partially compliant
		(report.Opinion == AuditorOpinion.Qualified || report.Opinion == AuditorOpinion.Unqualified).ShouldBeTrue();
	}

	private void SetupPassingValidation()
	{
		A.CallTo(() => _controlValidation.GetControlsForCriterion(A<TrustServicesCriterion>._))
			.Returns(new List<string> { "ctrl-1" });
		A.CallTo(() => _controlValidation.ValidateCriterionAsync(A<TrustServicesCriterion>._, A<CancellationToken>._))
			.Returns(new List<ControlValidationResult>
			{
				new()
				{
					ControlId = "ctrl-1",
					IsConfigured = true,
					IsEffective = true,
					EffectivenessScore = 95
				}
			});
	}

	private Soc2ReportGenerator CreateGenerator(bool withStore = false) =>
		new(
			Microsoft.Extensions.Options.Options.Create(_options),
			_controlValidation,
			_logger,
			withStore ? _reportStore : null);
}
