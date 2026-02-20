using Excalibur.Dispatch.Compliance;

using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Dispatch.Compliance.Tests.Soc2;

[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class Soc2ReportGeneratorShould
{
	private readonly IControlValidationService _controlValidation = A.Fake<IControlValidationService>();
	private readonly ISoc2ReportStore _reportStore = A.Fake<ISoc2ReportStore>();
	private readonly Soc2Options _soc2Options = new()
	{
		EnabledCategories =
		[
			TrustServicesCategory.Security
		],
		MinimumTypeIIPeriodDays = 90,
		DefaultTestSampleSize = 25
	};

	private readonly NullLogger<Soc2ReportGenerator> _logger = NullLogger<Soc2ReportGenerator>.Instance;

	[Fact]
	public async Task Generate_type_i_report_with_correct_type()
	{
		SetupControlValidation("CC1.1", CreatePassingResult("CC1.1"));

		var sut = CreateGenerator();

		var report = await sut.GenerateTypeIReportAsync(
			DateTimeOffset.UtcNow, new ReportOptions(), CancellationToken.None).ConfigureAwait(false);

		report.ShouldNotBeNull();
		report.ReportType.ShouldBe(Soc2ReportType.TypeI);
		report.ReportId.ShouldNotBe(Guid.Empty);
		report.GeneratedAt.ShouldNotBe(default);
	}

	[Fact]
	public async Task Generate_type_i_report_with_same_start_and_end_period()
	{
		SetupControlValidation("CC1.1", CreatePassingResult("CC1.1"));

		var sut = CreateGenerator();
		var asOfDate = DateTimeOffset.UtcNow;

		var report = await sut.GenerateTypeIReportAsync(
			asOfDate, new ReportOptions(), CancellationToken.None).ConfigureAwait(false);

		report.PeriodStart.ShouldBe(asOfDate);
		report.PeriodEnd.ShouldBe(asOfDate);
	}

	[Fact]
	public async Task Generate_type_i_report_with_custom_title()
	{
		SetupControlValidation("CC1.1", CreatePassingResult("CC1.1"));

		var sut = CreateGenerator();
		var options = new ReportOptions { CustomTitle = "Custom Report" };

		var report = await sut.GenerateTypeIReportAsync(
			DateTimeOffset.UtcNow, options, CancellationToken.None).ConfigureAwait(false);

		report.Title.ShouldBe("Custom Report");
	}

	[Fact]
	public async Task Generate_type_ii_report_with_correct_type()
	{
		SetupControlValidation("CC1.1", CreatePassingResult("CC1.1"));

		var sut = CreateGenerator();
		var start = DateTimeOffset.UtcNow.AddDays(-180);
		var end = DateTimeOffset.UtcNow;

		var report = await sut.GenerateTypeIIReportAsync(
			start, end, new ReportOptions(), CancellationToken.None).ConfigureAwait(false);

		report.ShouldNotBeNull();
		report.ReportType.ShouldBe(Soc2ReportType.TypeII);
		report.PeriodStart.ShouldBe(start);
		report.PeriodEnd.ShouldBe(end);
	}

	[Fact]
	public async Task Throw_for_type_ii_period_too_short()
	{
		var sut = CreateGenerator();
		var start = DateTimeOffset.UtcNow.AddDays(-30);
		var end = DateTimeOffset.UtcNow;

		await Should.ThrowAsync<ArgumentException>(
			() => sut.GenerateTypeIIReportAsync(start, end, new ReportOptions(), CancellationToken.None))
			.ConfigureAwait(false);
	}

	[Fact]
	public async Task Generate_type_ii_report_with_test_results_when_requested()
	{
		SetupControlValidation("CC1.1", CreatePassingResult("CC1.1"));
		A.CallTo(() => _controlValidation.RunControlTestAsync(A<string>._, A<ControlTestParameters>._, A<CancellationToken>._))
			.Returns(new ControlTestResult
			{
				ControlId = "CC1.1",
				Parameters = new ControlTestParameters { SampleSize = 25 },
				ItemsTested = 25,
				ExceptionsFound = 0,
				Outcome = TestOutcome.NoExceptions
			});

		var sut = CreateGenerator();
		var start = DateTimeOffset.UtcNow.AddDays(-180);
		var end = DateTimeOffset.UtcNow;
		var options = new ReportOptions { IncludeTestResults = true };

		var report = await sut.GenerateTypeIIReportAsync(
			start, end, options, CancellationToken.None).ConfigureAwait(false);

		report.ControlSections.ShouldNotBeEmpty();
	}

	[Fact]
	public async Task Generate_and_store_type_i_report()
	{
		SetupControlValidation("CC1.1", CreatePassingResult("CC1.1"));

		var sut = CreateGenerator(withStore: true);
		var request = new ReportGenerationRequest
		{
			ReportType = Soc2ReportType.TypeI,
			PeriodStart = DateTimeOffset.UtcNow,
			Options = new ReportOptions()
		};

		var report = await sut.GenerateAndStoreReportAsync(request, CancellationToken.None).ConfigureAwait(false);

		report.ShouldNotBeNull();
		A.CallTo(() => _reportStore.SaveReportAsync(A<Soc2Report>._, A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task Generate_and_store_type_ii_report()
	{
		SetupControlValidation("CC1.1", CreatePassingResult("CC1.1"));

		var sut = CreateGenerator(withStore: true);
		var request = new ReportGenerationRequest
		{
			ReportType = Soc2ReportType.TypeII,
			PeriodStart = DateTimeOffset.UtcNow.AddDays(-180),
			PeriodEnd = DateTimeOffset.UtcNow,
			Options = new ReportOptions()
		};

		var report = await sut.GenerateAndStoreReportAsync(request, CancellationToken.None).ConfigureAwait(false);

		report.ShouldNotBeNull();
		report.ReportType.ShouldBe(Soc2ReportType.TypeII);
	}

	[Fact]
	public async Task Throw_when_type_ii_request_missing_period_end()
	{
		var sut = CreateGenerator();
		var request = new ReportGenerationRequest
		{
			ReportType = Soc2ReportType.TypeII,
			PeriodStart = DateTimeOffset.UtcNow.AddDays(-180),
			PeriodEnd = null,
			Options = new ReportOptions()
		};

		await Should.ThrowAsync<ArgumentException>(
			() => sut.GenerateAndStoreReportAsync(request, CancellationToken.None))
			.ConfigureAwait(false);
	}

	[Fact]
	public async Task Not_store_when_no_report_store_provided()
	{
		SetupControlValidation("CC1.1", CreatePassingResult("CC1.1"));

		var sut = CreateGenerator(withStore: false);
		var request = new ReportGenerationRequest
		{
			ReportType = Soc2ReportType.TypeI,
			PeriodStart = DateTimeOffset.UtcNow,
			Options = new ReportOptions()
		};

		var report = await sut.GenerateAndStoreReportAsync(request, CancellationToken.None).ConfigureAwait(false);

		report.ShouldNotBeNull();
		A.CallTo(() => _reportStore.SaveReportAsync(A<Soc2Report>._, A<CancellationToken>._))
			.MustNotHaveHappened();
	}

	[Fact]
	public async Task Get_control_descriptions_for_criterion()
	{
		A.CallTo(() => _controlValidation.GetControlsForCriterion(TrustServicesCriterion.CC1_ControlEnvironment))
			.Returns(new List<string> { "ctrl-1", "ctrl-2" });

		var sut = CreateGenerator();

		var descriptions = await sut.GetControlDescriptionsAsync(
			TrustServicesCriterion.CC1_ControlEnvironment, CancellationToken.None).ConfigureAwait(false);

		descriptions.ShouldNotBeNull();
		descriptions.Count.ShouldBe(2);
	}

	[Fact]
	public async Task Get_test_results_for_criterion()
	{
		A.CallTo(() => _controlValidation.GetControlsForCriterion(TrustServicesCriterion.CC1_ControlEnvironment))
			.Returns(new List<string> { "ctrl-1" });
		A.CallTo(() => _controlValidation.RunControlTestAsync("ctrl-1", A<ControlTestParameters>._, A<CancellationToken>._))
			.Returns(new ControlTestResult
			{
				ControlId = "ctrl-1",
				Parameters = new ControlTestParameters { SampleSize = 25 },
				ItemsTested = 25,
				ExceptionsFound = 0,
				Outcome = TestOutcome.NoExceptions
			});

		var sut = CreateGenerator();
		var start = DateTimeOffset.UtcNow.AddDays(-180);
		var end = DateTimeOffset.UtcNow;

		var results = await sut.GetTestResultsAsync(
			TrustServicesCriterion.CC1_ControlEnvironment, start, end, CancellationToken.None).ConfigureAwait(false);

		results.ShouldNotBeEmpty();
		results[0].ControlId.ShouldBe("ctrl-1");
		results[0].Outcome.ShouldBe(TestOutcome.NoExceptions);
	}

	[Fact]
	public async Task Throw_for_null_options_in_type_i()
	{
		var sut = CreateGenerator();

		await Should.ThrowAsync<ArgumentNullException>(
			() => sut.GenerateTypeIReportAsync(DateTimeOffset.UtcNow, null!, CancellationToken.None))
			.ConfigureAwait(false);
	}

	[Fact]
	public async Task Throw_for_null_options_in_type_ii()
	{
		var sut = CreateGenerator();

		await Should.ThrowAsync<ArgumentNullException>(
			() => sut.GenerateTypeIIReportAsync(
				DateTimeOffset.UtcNow.AddDays(-180), DateTimeOffset.UtcNow, null!, CancellationToken.None))
			.ConfigureAwait(false);
	}

	[Fact]
	public async Task Throw_for_null_request_in_generate_and_store()
	{
		var sut = CreateGenerator();

		await Should.ThrowAsync<ArgumentNullException>(
			() => sut.GenerateAndStoreReportAsync(null!, CancellationToken.None))
			.ConfigureAwait(false);
	}

	[Fact]
	public void Throw_for_null_options_in_constructor()
	{
		Should.Throw<ArgumentNullException>(() =>
			new Soc2ReportGenerator(null!, _controlValidation, _logger));
	}

	[Fact]
	public void Throw_for_null_control_validation_in_constructor()
	{
		Should.Throw<ArgumentNullException>(() =>
			new Soc2ReportGenerator(
				Microsoft.Extensions.Options.Options.Create(_soc2Options), null!, _logger));
	}

	[Fact]
	public void Throw_for_null_logger_in_constructor()
	{
		Should.Throw<ArgumentNullException>(() =>
			new Soc2ReportGenerator(
				Microsoft.Extensions.Options.Options.Create(_soc2Options), _controlValidation, null!));
	}

	[Fact]
	public async Task Return_unqualified_opinion_for_fully_compliant()
	{
		SetupControlValidation("CC1.1", CreatePassingResult("CC1.1"));

		var sut = CreateGenerator();

		var report = await sut.GenerateTypeIReportAsync(
			DateTimeOffset.UtcNow, new ReportOptions(), CancellationToken.None).ConfigureAwait(false);

		report.Opinion.ShouldBe(AuditorOpinion.Unqualified);
	}

	[Fact]
	public async Task Use_system_description_from_options()
	{
		_soc2Options.SystemDescription = new SystemDescription
		{
			Name = "Test System",
			Description = "Test Desc",
			Services = ["Svc"],
			Infrastructure = ["Infra"],
			DataTypes = ["Data"]
		};
		SetupControlValidation("CC1.1", CreatePassingResult("CC1.1"));

		var sut = CreateGenerator();

		var report = await sut.GenerateTypeIReportAsync(
			DateTimeOffset.UtcNow, new ReportOptions(), CancellationToken.None).ConfigureAwait(false);

		report.System.ShouldNotBeNull();
		report.System.Name.ShouldBe("Test System");
	}

	private static ControlValidationResult CreatePassingResult(string controlId, int score = 95) =>
		new()
		{
			ControlId = controlId,
			IsConfigured = true,
			IsEffective = true,
			EffectivenessScore = score,
			ValidatedAt = DateTimeOffset.UtcNow,
			Evidence = [],
			ConfigurationIssues = []
		};

	private void SetupControlValidation(string controlId, ControlValidationResult result)
	{
		A.CallTo(() => _controlValidation.GetControlsForCriterion(A<TrustServicesCriterion>._))
			.Returns(new List<string> { controlId });
		A.CallTo(() => _controlValidation.ValidateControlAsync(controlId, A<CancellationToken>._))
			.Returns(result);
		A.CallTo(() => _controlValidation.ValidateCriterionAsync(A<TrustServicesCriterion>._, A<CancellationToken>._))
			.Returns(new List<ControlValidationResult> { result });
	}

	private Soc2ReportGenerator CreateGenerator(bool withStore = false) =>
		new(
			Microsoft.Extensions.Options.Options.Create(_soc2Options),
			_controlValidation,
			_logger,
			withStore ? _reportStore : null);
}
