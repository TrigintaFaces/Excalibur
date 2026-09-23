using Excalibur.Compliance;
using Excalibur.Compliance.Soc2;

namespace Excalibur.Compliance.Tests.Soc2;

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
	public async Task Validate_criterion_returns_no_results_when_no_controls_are_registered()
	{
		A.CallTo(() => _controlValidation.ValidateCriterionAsync(A<TrustServicesCriterion>._, A<CancellationToken>._))
			.Returns<IReadOnlyList<ControlValidationResult>>([]);

		var sut = CreateService();

		var results = await sut.ValidateCriterionAsync(TrustServicesCriterion.CC1_ControlEnvironment, CancellationToken.None).ConfigureAwait(false);

		// An unregistered criterion is an assessment that did not run. It must not be reported as a
		// control that was assessed and found ineffective, which is what a fabricated result would say.
		results.ShouldBeEmpty();
	}

	[Fact]
	public async Task Validate_criterion_reports_a_failing_control_that_a_passing_one_precedes()
	{
		// The ordering matters: the failure is SECOND. A verdict built from the first control alone
		// reports this criterion as effective while one of its controls is not.
		A.CallTo(() => _controlValidation.ValidateCriterionAsync(TrustServicesCriterion.CC1_ControlEnvironment, A<CancellationToken>._))
			.Returns<IReadOnlyList<ControlValidationResult>>(
				[CreatePassingResult("ctrl-1", ControlEffectiveness.Effective), CreateFailingResult("ctrl-2")]);

		var sut = CreateService();

		var results = await sut.ValidateCriterionAsync(TrustServicesCriterion.CC1_ControlEnvironment, CancellationToken.None).ConfigureAwait(false);

		results.Count.ShouldBe(2);
		results.ShouldContain(r => r.ControlId == "ctrl-2" && r.Outcome != ControlOutcome.Effective);
	}

	// Both arms below previously asserted the empty result. The name said it plainly -- "returns empty
	// evidence" -- and what the service actually returned was an empty set carrying a real SHA-256
	// chain-of-custody hash, which an auditor reads as collected and verified.

	[Fact]
	public async Task Get_evidence_throws_when_no_evidence_store_is_configured()
	{
		var sut = CreateService();

		_ = await Should.ThrowAsync<NotSupportedException>(() => sut.GetEvidenceAsync(
			TrustServicesCriterion.CC1_ControlEnvironment,
			DateTimeOffset.UtcNow.AddDays(-30),
			DateTimeOffset.UtcNow,
			CancellationToken.None)).ConfigureAwait(false);
	}

	[Fact]
	public async Task Export_for_auditor_throws_when_there_is_nothing_to_export()
	{
		var sut = CreateService();

		_ = await Should.ThrowAsync<NotSupportedException>(() => sut.ExportForAuditorAsync(
			ExportFormat.Json,
			DateTimeOffset.UtcNow.AddDays(-30),
			DateTimeOffset.UtcNow,
			CancellationToken.None)).ConfigureAwait(false);
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
			EffectivenessScore = ControlEffectiveness.ViolationDetected,
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
	public async Task Report_fully_compliant_when_every_control_passes()
	{
		SetupControlValidation("CC1.1", CreatePassingResult("CC1.1"));

		var sut = CreateService();
		var report = await sut.GenerateTypeIReportAsync(DateTimeOffset.UtcNow, new ReportOptions(), CancellationToken.None).ConfigureAwait(false);

		report.OverallLevel.ShouldBe(ComplianceLevel.FullyCompliant);
	}

	/// <summary>
	/// SAFETY. With no validators registered, the report must not accuse the consumer of anything.
	/// </summary>
	/// <remarks>
	/// Validators are OPT-IN, so a consumer who has registered none is the DEFAULT case, not an error
	/// path. The service used to substitute a compliance percentage of 0 for "nothing was assessed",
	/// which fell below every rung of the ladder to <see cref="ComplianceLevel.NonCompliant"/> - the
	/// worst verdict available, asserted on evidence that does not exist. Unknown is what this library
	/// reports when the evidence was never gathered.
	/// </remarks>
	[Fact]
	public async Task Never_report_non_compliant_when_nothing_was_assessed()
	{
		// No controls for any criterion: the shape a consumer gets when they register no validators.
		A.CallTo(() => _controlValidation.GetControlsForCriterion(A<TrustServicesCriterion>._))
			.Returns(new List<string>());

		var report = await CreateService()
			.GenerateTypeIReportAsync(DateTimeOffset.UtcNow, new ReportOptions(), CancellationToken.None)
			.ConfigureAwait(false);

		report.OverallLevel.ShouldBe(
			ComplianceLevel.Unknown,
			"no control was assessed, so the only honest level is Unknown");
	}

	/// <summary>
	/// SAFETY, the OTHER direction, and it is the one the enum ordering hid.
	/// </summary>
	/// <remarks>
	/// <c>ComplianceLevel.Unknown</c> is the LAST enum member, so an unassessed category failed the
	/// <c>All(level &lt;= SubstantiallyCompliant)</c> test and fell through to PartiallyCompliant - a
	/// PARTIAL level on a report where nothing was examined. Fixing only the NonCompliant direction would
	/// have moved the dishonesty rather than removed it, so both ends are asserted.
	/// </remarks>
	[Fact]
	public async Task Never_report_compliant_when_nothing_was_assessed()
	{
		A.CallTo(() => _controlValidation.GetControlsForCriterion(A<TrustServicesCriterion>._))
			.Returns(new List<string>());

		var report = await CreateService()
			.GenerateTypeIReportAsync(DateTimeOffset.UtcNow, new ReportOptions(), CancellationToken.None)
			.ConfigureAwait(false);

		report.OverallLevel.ShouldNotBe(ComplianceLevel.SubstantiallyCompliant);
		report.OverallLevel.ShouldNotBe(ComplianceLevel.FullyCompliant);
	}

	/// <summary>
	/// SAFETY. The status must say nothing was assessed, not that zero per cent passed.
	/// </summary>
	[Fact]
	public async Task Report_an_unassessed_category_as_unknown_with_its_coverage()
	{
		A.CallTo(() => _controlValidation.GetControlsForCriterion(A<TrustServicesCriterion>._))
			.Returns(new List<string>());

		var status = await CreateService()
			.GetComplianceStatusAsync(null, CancellationToken.None).ConfigureAwait(false);

		var category = status.CategoryStatuses.Values.ShouldHaveSingleItem();
		category.Level.ShouldBe(ComplianceLevel.Unknown);

		// The coverage is what lets a reader tell "nothing was looked at" from "everything failed" -
		// the single percentage cannot separate those two, which is why it is not the assertion here.
		category.CriteriaAssessed.ShouldBe(0);
		category.CriteriaEnabled.ShouldBeGreaterThan(0);
	}

	/// <summary>
	/// PRECISION. A control that really was assessed and really failed must STILL be Adverse.
	/// </summary>
	/// <remarks>
	/// Without this the safety arms are satisfied by a service that never reports Adverse at all, which
	/// would hide real deficiencies instead of inventing them - the same defect pointed the other way.
	/// </remarks>
	[Fact]
	public async Task Still_report_non_compliant_when_an_assessed_control_genuinely_fails()
	{
		SetupControlValidation("CC1.1", CreateFailingResult("CC1.1"));

		var report = await CreateService()
			.GenerateTypeIReportAsync(DateTimeOffset.UtcNow, new ReportOptions(), CancellationToken.None)
			.ConfigureAwait(false);

		report.OverallLevel.ShouldBe(
			ComplianceLevel.NonCompliant,
			"this control WAS assessed and it failed, which is a real deficiency and must be reported");
	}

	private static ControlValidationResult CreatePassingResult(
		string controlId,
		ControlEffectiveness score = ControlEffectiveness.Effective) =>
		new()
		{
			ControlId = controlId,
			IsConfigured = true,
			EffectivenessScore = score,
			ValidatedAt = DateTimeOffset.UtcNow,
			Evidence = [],
			ConfigurationIssues = []
		};

	private static ControlValidationResult CreateFailingResult(
		string controlId,
		ControlEffectiveness score = ControlEffectiveness.ViolationDetected) =>
		new()
		{
			ControlId = controlId,
			IsConfigured = true,
			EffectivenessScore = score,
			ValidatedAt = DateTimeOffset.UtcNow,
			Evidence = [],
			ConfigurationIssues = ["control is configured but not operating effectively"]
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
