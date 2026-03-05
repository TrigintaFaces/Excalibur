// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Security.Tests.Compliance.Soc2;

/// <summary>
/// Unit tests for <see cref="Soc2ComplianceService"/>.
/// </summary>
[Trait("Category", TestCategories.Unit)]
public sealed class Soc2ComplianceServiceShould
{
	private readonly IControlValidationService _fakeControlValidation;
	private readonly IOptions<Soc2Options> _fakeOptions;
	private readonly Soc2ComplianceService _sut;

	public Soc2ComplianceServiceShould()
	{
		_fakeControlValidation = A.Fake<IControlValidationService>();
		_fakeOptions = A.Fake<IOptions<Soc2Options>>();

		// Default options with Security category enabled
		_ = A.CallTo(() => _fakeOptions.Value).Returns(new Soc2Options
		{
			EnabledCategories = [TrustServicesCategory.Security],
			MinimumTypeIIPeriodDays = 90,
			DefaultTestSampleSize = 25
		});

		_sut = new Soc2ComplianceService(_fakeOptions, _fakeControlValidation);
	}

	#region GetComplianceStatusAsync Tests

	[Fact]
	public async Task GetComplianceStatusAsync_ReturnStatus_WithEvaluatedTimestamp()
	{
		// Arrange
		SetupDefaultValidation();
		var beforeTime = DateTimeOffset.UtcNow;

		// Act
		var result = await _sut.GetComplianceStatusAsync(null, CancellationToken.None);

		// Assert
		result.EvaluatedAt.ShouldBeGreaterThanOrEqualTo(beforeTime);
		var assertionUpperBound1 = DateTimeOffset.UtcNow;
		result.EvaluatedAt.ShouldBeLessThanOrEqualTo(assertionUpperBound1);
	}

	[Fact]
	public async Task GetComplianceStatusAsync_IncludeTenantId_WhenProvided()
	{
		// Arrange
		SetupDefaultValidation();
		var tenantId = "tenant-123";

		// Act
		var result = await _sut.GetComplianceStatusAsync(tenantId, CancellationToken.None);

		// Assert
		result.TenantId.ShouldBe(tenantId);
	}

	[Fact]
	public async Task GetComplianceStatusAsync_ReturnFullyCompliant_WhenAllControlsEffective()
	{
		// Arrange
		var controlId = "SEC-001";
		SetupValidationResult(controlId, isEffective: true, score: 100);

		// Act
		var result = await _sut.GetComplianceStatusAsync(null, CancellationToken.None);

		// Assert
		result.OverallLevel.ShouldBe(ComplianceLevel.FullyCompliant);
	}

	[Fact]
	public async Task GetComplianceStatusAsync_ReturnNonCompliant_WhenControlsIneffective()
	{
		// Arrange
		var controlId = "SEC-001";
		SetupValidationResult(controlId, isEffective: false, score: 20);

		// Act
		var result = await _sut.GetComplianceStatusAsync(null, CancellationToken.None);

		// Assert
		result.OverallLevel.ShouldBe(ComplianceLevel.NonCompliant);
	}

	[Fact]
	public async Task GetComplianceStatusAsync_CollectGaps_WhenControlsHaveIssues()
	{
		// Arrange
		var controlId = "SEC-001";
		SetupValidationResult(controlId, isEffective: false, score: 50, issues: ["Missing encryption"]);

		// Act
		var result = await _sut.GetComplianceStatusAsync(null, CancellationToken.None);

		// Assert
		result.ActiveGaps.ShouldNotBeEmpty();
		result.ActiveGaps.ShouldContain(g => g.Description == "Missing encryption");
	}

	[Fact]
	public async Task GetComplianceStatusAsync_SetGapSeverity_BasedOnScore()
	{
		// Arrange
		var controlId = "SEC-001";
		SetupValidationResult(controlId, isEffective: false, score: 20, issues: ["Critical issue"]);

		// Act
		var result = await _sut.GetComplianceStatusAsync(null, CancellationToken.None);

		// Assert
		result.ActiveGaps.ShouldContain(g => g.Severity == GapSeverity.Critical);
	}

	[Fact]
	public async Task GetComplianceStatusAsync_BuildCategoryStatus_ForEachEnabledCategory()
	{
		// Arrange
		_ = A.CallTo(() => _fakeOptions.Value).Returns(new Soc2Options
		{
			EnabledCategories =
			[
				TrustServicesCategory.Security,
				TrustServicesCategory.Availability
			]
		});

		SetupDefaultValidation();
		var sut = new Soc2ComplianceService(_fakeOptions, _fakeControlValidation);

		// Act
		var result = await sut.GetComplianceStatusAsync(null, CancellationToken.None);

		// Assert
		result.CategoryStatuses.ShouldContainKey(TrustServicesCategory.Security);
		result.CategoryStatuses.ShouldContainKey(TrustServicesCategory.Availability);
	}

	[Fact]
	public async Task GetComplianceStatusAsync_ReturnUnknown_WhenNoCategories()
	{
		// Arrange
		_ = A.CallTo(() => _fakeOptions.Value).Returns(new Soc2Options
		{
			EnabledCategories = []
		});

		var sut = new Soc2ComplianceService(_fakeOptions, _fakeControlValidation);

		// Act
		var result = await sut.GetComplianceStatusAsync(null, CancellationToken.None);

		// Assert
		result.OverallLevel.ShouldBe(ComplianceLevel.Unknown);
	}

	#endregion GetComplianceStatusAsync Tests

	#region GenerateTypeIReportAsync Tests

	[Fact]
	public async Task GenerateTypeIReportAsync_ReturnTypeIReport()
	{
		// Arrange
		SetupDefaultValidation();
		var asOfDate = DateTimeOffset.UtcNow;
		var options = new ReportOptions();

		// Act
		var result = await _sut.GenerateTypeIReportAsync(asOfDate, options, CancellationToken.None);

		// Assert
		result.ReportType.ShouldBe(Soc2ReportType.TypeI);
	}

	[Fact]
	public async Task GenerateTypeIReportAsync_SetPeriodToSameDate()
	{
		// Arrange
		SetupDefaultValidation();
		var asOfDate = DateTimeOffset.UtcNow.Date;
		var options = new ReportOptions();

		// Act
		var result = await _sut.GenerateTypeIReportAsync(asOfDate, options, CancellationToken.None);

		// Assert
		result.PeriodStart.ShouldBe(asOfDate);
		result.PeriodEnd.ShouldBe(asOfDate);
	}

	[Fact]
	public async Task GenerateTypeIReportAsync_GenerateUniqueReportId()
	{
		// Arrange
		SetupDefaultValidation();
		var options = new ReportOptions();

		// Act
		var result1 = await _sut.GenerateTypeIReportAsync(DateTimeOffset.UtcNow, options, CancellationToken.None);
		var result2 = await _sut.GenerateTypeIReportAsync(DateTimeOffset.UtcNow, options, CancellationToken.None);

		// Assert
		result1.ReportId.ShouldNotBe(result2.ReportId);
	}

	[Fact]
	public async Task GenerateTypeIReportAsync_UseCustomTitle_WhenProvided()
	{
		// Arrange
		SetupDefaultValidation();
		var options = new ReportOptions { CustomTitle = "Custom SOC 2 Report" };

		// Act
		var result = await _sut.GenerateTypeIReportAsync(DateTimeOffset.UtcNow, options, CancellationToken.None);

		// Assert
		result.Title.ShouldBe("Custom SOC 2 Report");
	}

	[Fact]
	public async Task GenerateTypeIReportAsync_GenerateDefaultTitle_WhenNotProvided()
	{
		// Arrange
		SetupDefaultValidation();
		var asOfDate = new DateTimeOffset(2025, 1, 15, 0, 0, 0, TimeSpan.Zero);
		var options = new ReportOptions();

		// Act
		var result = await _sut.GenerateTypeIReportAsync(asOfDate, options, CancellationToken.None);

		// Assert
		result.Title.ShouldContain("Type I");
		result.Title.ShouldContain("2025-01-15");
	}

	[Fact]
	public async Task GenerateTypeIReportAsync_IncludeEnabledCategories()
	{
		// Arrange
		SetupDefaultValidation();
		var options = new ReportOptions();

		// Act
		var result = await _sut.GenerateTypeIReportAsync(DateTimeOffset.UtcNow, options, CancellationToken.None);

		// Assert
		result.CategoriesIncluded.ShouldContain(TrustServicesCategory.Security);
	}

	[Fact]
	public async Task GenerateTypeIReportAsync_UseOptionsCategories_WhenProvided()
	{
		// Arrange
		SetupDefaultValidation();
		var options = new ReportOptions
		{
			Categories = [TrustServicesCategory.Availability]
		};

		// Act
		var result = await _sut.GenerateTypeIReportAsync(DateTimeOffset.UtcNow, options, CancellationToken.None);

		// Assert
		result.CategoriesIncluded.ShouldContain(TrustServicesCategory.Availability);
		result.CategoriesIncluded.ShouldNotContain(TrustServicesCategory.Security);
	}

	[Fact]
	public async Task GenerateTypeIReportAsync_NotIncludeTestResults()
	{
		// Arrange
		SetupDefaultValidation();
		var options = new ReportOptions();

		// Act
		var result = await _sut.GenerateTypeIReportAsync(DateTimeOffset.UtcNow, options, CancellationToken.None);

		// Assert
		result.ControlSections.ShouldAllBe(s => s.TestResults == null);
	}

	[Fact]
	public async Task GenerateTypeIReportAsync_SetGeneratedTimestamp()
	{
		// Arrange
		SetupDefaultValidation();
		var options = new ReportOptions();
		var beforeTime = DateTimeOffset.UtcNow;

		// Act
		var result = await _sut.GenerateTypeIReportAsync(DateTimeOffset.UtcNow, options, CancellationToken.None);

		// Assert
		result.GeneratedAt.ShouldBeGreaterThanOrEqualTo(beforeTime);
	}

	[Fact]
	public async Task GenerateTypeIReportAsync_DetermineUnqualifiedOpinion_WhenFullyCompliant()
	{
		// Arrange
		var controlId = "SEC-001";
		SetupValidationResult(controlId, isEffective: true, score: 100);
		var options = new ReportOptions();

		// Act
		var result = await _sut.GenerateTypeIReportAsync(DateTimeOffset.UtcNow, options, CancellationToken.None);

		// Assert
		result.Opinion.ShouldBe(AuditorOpinion.Unqualified);
	}

	[Fact]
	public async Task GenerateTypeIReportAsync_DetermineAdverseOpinion_WhenNonCompliant()
	{
		// Arrange
		var controlId = "SEC-001";
		SetupValidationResult(controlId, isEffective: false, score: 20);
		var options = new ReportOptions();

		// Act
		var result = await _sut.GenerateTypeIReportAsync(DateTimeOffset.UtcNow, options, CancellationToken.None);

		// Assert
		result.Opinion.ShouldBe(AuditorOpinion.Adverse);
	}

	[Fact]
	public async Task GenerateTypeIReportAsync_IncludeSystemDescription()
	{
		// Arrange
		SetupDefaultValidation();
		var options = new ReportOptions();

		// Act
		var result = await _sut.GenerateTypeIReportAsync(DateTimeOffset.UtcNow, options, CancellationToken.None);

		// Assert
		_ = result.System.ShouldNotBeNull();
		result.System.Name.ShouldNotBeNullOrEmpty();
	}

	#endregion GenerateTypeIReportAsync Tests

	#region GenerateTypeIIReportAsync Tests

	[Fact]
	public async Task GenerateTypeIIReportAsync_ReturnTypeIIReport()
	{
		// Arrange
		SetupDefaultValidation();
		var periodStart = DateTimeOffset.UtcNow.AddDays(-180);
		var periodEnd = DateTimeOffset.UtcNow;
		var options = new ReportOptions();

		// Act
		var result = await _sut.GenerateTypeIIReportAsync(periodStart, periodEnd, options, CancellationToken.None);

		// Assert
		result.ReportType.ShouldBe(Soc2ReportType.TypeII);
	}

	[Fact]
	public async Task GenerateTypeIIReportAsync_SetCorrectPeriod()
	{
		// Arrange
		SetupDefaultValidation();
		var periodStart = DateTimeOffset.UtcNow.AddDays(-180);
		var periodEnd = DateTimeOffset.UtcNow;
		var options = new ReportOptions();

		// Act
		var result = await _sut.GenerateTypeIIReportAsync(periodStart, periodEnd, options, CancellationToken.None);

		// Assert
		result.PeriodStart.ShouldBe(periodStart);
		result.PeriodEnd.ShouldBe(periodEnd);
	}

	[Fact]
	public async Task GenerateTypeIIReportAsync_ThrowException_WhenPeriodTooShort()
	{
		// Arrange
		var periodStart = DateTimeOffset.UtcNow.AddDays(-30);
		var periodEnd = DateTimeOffset.UtcNow;
		var options = new ReportOptions();

		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentException>(
			() => _sut.GenerateTypeIIReportAsync(periodStart, periodEnd, options, CancellationToken.None));
	}

	[Fact]
	public async Task GenerateTypeIIReportAsync_IncludeTestResults_WhenRequested()
	{
		// Arrange
		SetupDefaultValidationWithTestResults();
		var periodStart = DateTimeOffset.UtcNow.AddDays(-180);
		var periodEnd = DateTimeOffset.UtcNow;
		var options = new ReportOptions { IncludeTestResults = true };

		// Act
		var result = await _sut.GenerateTypeIIReportAsync(periodStart, periodEnd, options, CancellationToken.None);

		// Assert
		// TestResults will be populated if controls exist
		_ = A.CallTo(() => _fakeControlValidation.RunControlTestAsync(
				A<string>._,
				A<ControlTestParameters>._,
				A<CancellationToken>._))
			.MustHaveHappened();
	}

	[Fact]
	public async Task GenerateTypeIIReportAsync_GenerateDefaultTitle()
	{
		// Arrange
		SetupDefaultValidation();
		var periodStart = new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero);
		var periodEnd = new DateTimeOffset(2025, 6, 30, 0, 0, 0, TimeSpan.Zero);
		var options = new ReportOptions();

		// Act
		var result = await _sut.GenerateTypeIIReportAsync(periodStart, periodEnd, options, CancellationToken.None);

		// Assert
		result.Title.ShouldContain("Type II");
		result.Title.ShouldContain("2025-01-01");
		result.Title.ShouldContain("2025-06-30");
	}

	[Fact]
	public async Task GenerateTypeIIReportAsync_IncludeTenantId_WhenProvided()
	{
		// Arrange
		SetupDefaultValidation();
		var periodStart = DateTimeOffset.UtcNow.AddDays(-180);
		var periodEnd = DateTimeOffset.UtcNow;
		var options = new ReportOptions { TenantId = "tenant-456" };

		// Act
		var result = await _sut.GenerateTypeIIReportAsync(periodStart, periodEnd, options, CancellationToken.None);

		// Assert
		result.TenantId.ShouldBe("tenant-456");
	}

	#endregion GenerateTypeIIReportAsync Tests

	#region ValidateControlAsync Tests

	[Fact]
	public async Task ValidateControlAsync_ReturnNotConfigured_WhenNoControlsForCriterion()
	{
		// Arrange
		_ = A.CallTo(() => _fakeControlValidation.GetControlsForCriterion(A<TrustServicesCriterion>._))
			.Returns([]);

		// Act
		var result = await _sut.ValidateControlAsync(TrustServicesCriterion.CC6_LogicalAccess, CancellationToken.None);

		// Assert
		result.IsConfigured.ShouldBeFalse();
		result.IsEffective.ShouldBeFalse();
		result.EffectivenessScore.ShouldBe(0);
	}

	[Fact]
	public async Task ValidateControlAsync_ValidateFirstControl_WhenMultipleExist()
	{
		// Arrange
		var controlIds = new List<string> { "SEC-001", "SEC-002" };
		_ = A.CallTo(() => _fakeControlValidation.GetControlsForCriterion(TrustServicesCriterion.CC6_LogicalAccess))
			.Returns(controlIds);
		_ = A.CallTo(() => _fakeControlValidation.ValidateControlAsync("SEC-001", A<CancellationToken>._))
			.Returns(CreateValidationResult("SEC-001", true, 100));

		// Act
		_ = await _sut.ValidateControlAsync(TrustServicesCriterion.CC6_LogicalAccess, CancellationToken.None);

		// Assert
		_ = A.CallTo(() => _fakeControlValidation.ValidateControlAsync("SEC-001", A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	#endregion ValidateControlAsync Tests

	#region GetEvidenceAsync Tests

	[Fact]
	public async Task GetEvidenceAsync_ReturnEvidence_WithCorrectCriterion()
	{
		// Arrange
		var criterion = TrustServicesCriterion.CC6_LogicalAccess;
		var periodStart = DateTimeOffset.UtcNow.AddDays(-30);
		var periodEnd = DateTimeOffset.UtcNow;

		// Act
		var result = await _sut.GetEvidenceAsync(criterion, periodStart, periodEnd, CancellationToken.None);

		// Assert
		result.Criterion.ShouldBe(criterion);
		result.PeriodStart.ShouldBe(periodStart);
		result.PeriodEnd.ShouldBe(periodEnd);
	}

	[Fact]
	public async Task GetEvidenceAsync_ReturnEmptyItems_WhenNoEvidence()
	{
		// Act
		var result = await _sut.GetEvidenceAsync(
			TrustServicesCriterion.CC6_LogicalAccess,
			DateTimeOffset.UtcNow.AddDays(-30),
			DateTimeOffset.UtcNow, CancellationToken.None);

		// Assert
		result.Items.ShouldBeEmpty();
	}

	[Fact]
	public async Task GetEvidenceAsync_IncludeChainOfCustodyHash()
	{
		// Act
		var result = await _sut.GetEvidenceAsync(
			TrustServicesCriterion.CC6_LogicalAccess,
			DateTimeOffset.UtcNow.AddDays(-30),
			DateTimeOffset.UtcNow, CancellationToken.None);

		// Assert
		result.ChainOfCustodyHash.ShouldNotBeNullOrEmpty();
	}

	#endregion GetEvidenceAsync Tests

	#region ExportForAuditorAsync Tests

	[Fact]
	public async Task ExportForAuditorAsync_ReturnEmptyArray()
	{
		// Act
		var result = await _sut.ExportForAuditorAsync(
			ExportFormat.Pdf,
			DateTimeOffset.UtcNow.AddDays(-30),
			DateTimeOffset.UtcNow, CancellationToken.None);

		// Assert
		result.ShouldBeEmpty();
	}

	#endregion ExportForAuditorAsync Tests

	#region Helper Methods

	private static ControlValidationResult CreateValidationResult(
		string controlId,
		bool isEffective,
		int score,
		IReadOnlyList<string>? issues = null)
	{
		return new ControlValidationResult
		{
			ControlId = controlId,
			IsConfigured = true,
			IsEffective = isEffective,
			EffectivenessScore = score,
			ConfigurationIssues = issues ?? [],
			Evidence = [],
			ValidatedAt = DateTimeOffset.UtcNow
		};
	}

	private void SetupDefaultValidation()
	{
		_ = A.CallTo(() => _fakeControlValidation.GetControlsForCriterion(A<TrustServicesCriterion>._))
			.Returns(["SEC-001"]);

		_ = A.CallTo(() => _fakeControlValidation.ValidateControlAsync(A<string>._, A<CancellationToken>._))
			.Returns(CreateValidationResult("SEC-001", true, 100));
	}

	private void SetupDefaultValidationWithTestResults()
	{
		SetupDefaultValidation();

		_ = A.CallTo(() => _fakeControlValidation.RunControlTestAsync(
				A<string>._,
				A<ControlTestParameters>._,
				A<CancellationToken>._))
			.Returns(new ControlTestResult
			{
				ControlId = "SEC-001",
				Parameters = new ControlTestParameters(),
				ItemsTested = 25,
				ExceptionsFound = 0,
				Outcome = TestOutcome.NoExceptions,
				Exceptions = []
			});
	}

	private void SetupValidationResult(
		string controlId,
		bool isEffective,
		int score,
		IReadOnlyList<string>? issues = null)
	{
		_ = A.CallTo(() => _fakeControlValidation.GetControlsForCriterion(A<TrustServicesCriterion>._))
			.Returns([controlId]);

		_ = A.CallTo(() => _fakeControlValidation.ValidateControlAsync(controlId, A<CancellationToken>._))
			.Returns(CreateValidationResult(controlId, isEffective, score, issues));
	}

	#endregion Helper Methods
}
