using Excalibur.Dispatch.Compliance;

namespace Excalibur.Dispatch.Compliance.Tests.Soc2;

[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class Soc2ComplianceServiceShould
{
	private readonly IControlValidationService _controlValidation = A.Fake<IControlValidationService>();
	private readonly Soc2Options _soc2Options = new()
	{
		EnabledCategories =
		[
			TrustServicesCategory.Security
		],
		MinimumTypeIIPeriodDays = 90,
		DefaultTestSampleSize = 25
	};

	[Fact]
	public async Task Return_compliance_status_with_category_statuses()
	{
		SetupControlValidation("CC1.1", CreatePassingResult("CC1.1"));

		var sut = CreateService();

		var status = await sut.GetComplianceStatusAsync(null, CancellationToken.None).ConfigureAwait(false);

		status.ShouldNotBeNull();
		status.CategoryStatuses.ShouldNotBeNull();
		status.EvaluatedAt.ShouldNotBe(default);
	}

	[Fact]
	public async Task Return_tenant_id_in_compliance_status()
	{
		SetupControlValidation("CC1.1", CreatePassingResult("CC1.1"));

		var sut = CreateService();

		var status = await sut.GetComplianceStatusAsync("tenant-1", CancellationToken.None).ConfigureAwait(false);

		status.TenantId.ShouldBe("tenant-1");
	}

	[Fact]
	public async Task Generate_type_i_report_with_correct_type()
	{
		SetupControlValidation("CC1.1", CreatePassingResult("CC1.1"));

		var sut = CreateService();
		var asOfDate = DateTimeOffset.UtcNow;
		var options = new ReportOptions();

		var report = await sut.GenerateTypeIReportAsync(asOfDate, options, CancellationToken.None).ConfigureAwait(false);

		report.ShouldNotBeNull();
		report.ReportType.ShouldBe(Soc2ReportType.TypeI);
		report.PeriodStart.ShouldBe(asOfDate);
		report.PeriodEnd.ShouldBe(asOfDate);
		report.GeneratedAt.ShouldNotBe(default);
		report.ReportId.ShouldNotBe(Guid.Empty);
	}

	[Fact]
	public async Task Generate_type_i_report_with_custom_title()
	{
		SetupControlValidation("CC1.1", CreatePassingResult("CC1.1"));

		var sut = CreateService();
		var options = new ReportOptions { CustomTitle = "My Custom Report" };

		var report = await sut.GenerateTypeIReportAsync(DateTimeOffset.UtcNow, options, CancellationToken.None).ConfigureAwait(false);

		report.Title.ShouldBe("My Custom Report");
	}

	[Fact]
	public async Task Generate_type_ii_report_with_correct_period()
	{
		SetupControlValidation("CC1.1", CreatePassingResult("CC1.1"));

		var sut = CreateService();
		var start = DateTimeOffset.UtcNow.AddDays(-180);
		var end = DateTimeOffset.UtcNow;
		var options = new ReportOptions();

		var report = await sut.GenerateTypeIIReportAsync(start, end, options, CancellationToken.None).ConfigureAwait(false);

		report.ShouldNotBeNull();
		report.ReportType.ShouldBe(Soc2ReportType.TypeII);
		report.PeriodStart.ShouldBe(start);
		report.PeriodEnd.ShouldBe(end);
	}

	[Fact]
	public async Task Throw_for_type_ii_report_with_period_too_short()
	{
		var sut = CreateService();
		var start = DateTimeOffset.UtcNow.AddDays(-30);
		var end = DateTimeOffset.UtcNow;
		var options = new ReportOptions();

		await Should.ThrowAsync<ArgumentException>(
			() => sut.GenerateTypeIIReportAsync(start, end, options, CancellationToken.None)).ConfigureAwait(false);
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

		var sut = CreateService();
		var start = DateTimeOffset.UtcNow.AddDays(-180);
		var end = DateTimeOffset.UtcNow;
		var options = new ReportOptions { IncludeTestResults = true };

		var report = await sut.GenerateTypeIIReportAsync(start, end, options, CancellationToken.None).ConfigureAwait(false);

		report.ControlSections.ShouldNotBeEmpty();
	}

	[Fact]
	public async Task Validate_control_returns_not_configured_when_no_controls_for_criterion()
	{
		A.CallTo(() => _controlValidation.GetControlsForCriterion(A<TrustServicesCriterion>._))
			.Returns(new List<string>());

		var sut = CreateService();

		var result = await sut.ValidateControlAsync(TrustServicesCriterion.CC1_ControlEnvironment, CancellationToken.None).ConfigureAwait(false);

		result.IsConfigured.ShouldBeFalse();
		result.IsEffective.ShouldBeFalse();
		result.EffectivenessScore.ShouldBe(0);
	}

	[Fact]
	public async Task Validate_control_delegates_to_control_validation()
	{
		A.CallTo(() => _controlValidation.GetControlsForCriterion(TrustServicesCriterion.CC1_ControlEnvironment))
			.Returns(new List<string> { "ctrl-1" });
		A.CallTo(() => _controlValidation.ValidateControlAsync("ctrl-1", A<CancellationToken>._))
			.Returns(CreatePassingResult("ctrl-1", 88));

		var sut = CreateService();

		var result = await sut.ValidateControlAsync(TrustServicesCriterion.CC1_ControlEnvironment, CancellationToken.None).ConfigureAwait(false);

		result.ControlId.ShouldBe("ctrl-1");
		result.IsEffective.ShouldBeTrue();
	}

	[Fact]
	public async Task Get_evidence_returns_empty_evidence()
	{
		var sut = CreateService();
		var start = DateTimeOffset.UtcNow.AddDays(-30);
		var end = DateTimeOffset.UtcNow;

		var evidence = await sut.GetEvidenceAsync(TrustServicesCriterion.CC1_ControlEnvironment, start, end, CancellationToken.None).ConfigureAwait(false);

		evidence.ShouldNotBeNull();
		evidence.Criterion.ShouldBe(TrustServicesCriterion.CC1_ControlEnvironment);
		evidence.Items.ShouldBeEmpty();
	}

	[Fact]
	public async Task Export_for_auditor_returns_empty_bytes()
	{
		var sut = CreateService();

		var result = await sut.ExportForAuditorAsync(
			ExportFormat.Json,
			DateTimeOffset.UtcNow.AddDays(-30),
			DateTimeOffset.UtcNow,
			CancellationToken.None).ConfigureAwait(false);

		result.ShouldNotBeNull();
		result.ShouldBeEmpty();
	}

	[Fact]
	public void Get_service_returns_self_for_audit_exporter()
	{
		var sut = CreateService();

		var service = sut.GetService(typeof(ISoc2AuditExporter));

		service.ShouldNotBeNull();
		service.ShouldBe(sut);
	}

	[Fact]
	public void Get_service_returns_null_for_unknown_type()
	{
		var sut = CreateService();

		var service = sut.GetService(typeof(string));

		service.ShouldBeNull();
	}

	[Fact]
	public async Task Report_uses_system_description_from_options()
	{
		_soc2Options.SystemDescription = new SystemDescription
		{
			Name = "Custom System",
			Description = "Custom Description",
			Services = ["Svc1"],
			Infrastructure = ["Infra1"],
			DataTypes = ["Data1"]
		};

		SetupControlValidation("CC1.1", CreatePassingResult("CC1.1"));

		var sut = CreateService();
		var report = await sut.GenerateTypeIReportAsync(DateTimeOffset.UtcNow, new ReportOptions(), CancellationToken.None).ConfigureAwait(false);

		report.System.ShouldNotBeNull();
		report.System.Name.ShouldBe("Custom System");
	}

	[Fact]
	public async Task Detect_gaps_when_controls_have_issues()
	{
		SetupControlValidation("CC1.1", new ControlValidationResult
		{
			ControlId = "CC1.1",
			IsConfigured = true,
			IsEffective = false,
			EffectivenessScore = 30,
			ValidatedAt = DateTimeOffset.UtcNow,
			Evidence = [],
			ConfigurationIssues = ["Missing audit trail"]
		});

		var sut = CreateService();

		var status = await sut.GetComplianceStatusAsync(null, CancellationToken.None).ConfigureAwait(false);

		status.ActiveGaps.ShouldNotBeEmpty();
		status.ActiveGaps[0].Description.ShouldBe("Missing audit trail");
	}

	[Fact]
	public async Task Report_unqualified_opinion_for_fully_compliant()
	{
		SetupControlValidation("CC1.1", CreatePassingResult("CC1.1"));

		var sut = CreateService();
		var report = await sut.GenerateTypeIReportAsync(DateTimeOffset.UtcNow, new ReportOptions(), CancellationToken.None).ConfigureAwait(false);

		report.Opinion.ShouldBe(AuditorOpinion.Unqualified);
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
	}

	private Soc2ComplianceService CreateService() =>
		new(Microsoft.Extensions.Options.Options.Create(_soc2Options), _controlValidation);
}
