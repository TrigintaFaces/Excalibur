// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Security.Tests.Compliance.Soc2;

/// <summary>
/// Unit tests for <see cref="Soc2ReportGenerator"/>.
/// </summary>
[Trait("Category", TestCategories.Unit)]
public sealed class Soc2ReportGeneratorShould
{
	private readonly IControlValidationService _fakeControlValidation;
	private readonly ISoc2ReportStore _fakeReportStore;
	private readonly IOptions<Soc2Options> _fakeOptions;
	private readonly ILogger<Soc2ReportGenerator> _fakeLogger;
	private readonly Soc2ReportGenerator _sut;

	public Soc2ReportGeneratorShould()
	{
		_fakeControlValidation = A.Fake<IControlValidationService>();
		_fakeReportStore = A.Fake<ISoc2ReportStore>();
		_fakeOptions = A.Fake<IOptions<Soc2Options>>();
		_fakeLogger = A.Fake<ILogger<Soc2ReportGenerator>>();

		_ = A.CallTo(() => _fakeOptions.Value).Returns(new Soc2Options
		{
			EnabledCategories = [TrustServicesCategory.Security],
			MinimumTypeIIPeriodDays = 90,
			DefaultTestSampleSize = 25
		});

		_sut = new Soc2ReportGenerator(
			_fakeOptions,
			_fakeControlValidation,
			_fakeLogger,
			_fakeReportStore);
	}

	#region Constructor Tests

	[Fact]
	public void ThrowArgumentNullException_WhenOptionsIsNull()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() => new Soc2ReportGenerator(
			null!,
			_fakeControlValidation,
			_fakeLogger,
			_fakeReportStore))
			.ParamName.ShouldBe("options");
	}

	[Fact]
	public void ThrowArgumentNullException_WhenControlValidationIsNull()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() => new Soc2ReportGenerator(
			_fakeOptions,
			null!,
			_fakeLogger,
			_fakeReportStore))
			.ParamName.ShouldBe("controlValidation");
	}

	[Fact]
	public void ThrowArgumentNullException_WhenLoggerIsNull()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() => new Soc2ReportGenerator(
			_fakeOptions,
			_fakeControlValidation,
			null!,
			_fakeReportStore))
			.ParamName.ShouldBe("logger");
	}

	[Fact]
	public void AllowNullReportStore()
	{
		// Act - Should not throw
		var sut = new Soc2ReportGenerator(
			_fakeOptions,
			_fakeControlValidation,
			_fakeLogger,
			null);

		// Assert
		_ = sut.ShouldNotBeNull();
	}

	#endregion Constructor Tests

	#region GenerateTypeIReportAsync Tests

	[Fact]
	public async Task GenerateTypeIReportAsync_ThrowArgumentNullException_WhenOptionsIsNull()
	{
		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentNullException>(
			() => _sut.GenerateTypeIReportAsync(DateTimeOffset.UtcNow, null!, CancellationToken.None));
	}

	[Fact]
	public async Task GenerateTypeIReportAsync_ReturnTypeIReport()
	{
		// Arrange
		SetupDefaultValidation();
		var options = new ReportOptions();

		// Act
		var result = await _sut.GenerateTypeIReportAsync(DateTimeOffset.UtcNow, options, CancellationToken.None);

		// Assert
		result.ReportType.ShouldBe(Soc2ReportType.TypeI);
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
		result1.ReportId.ShouldNotBe(Guid.Empty);
		result2.ReportId.ShouldNotBe(Guid.Empty);
		result1.ReportId.ShouldNotBe(result2.ReportId);
	}

	[Fact]
	public async Task GenerateTypeIReportAsync_UseAsOfDateForPeriod()
	{
		// Arrange
		SetupDefaultValidation();
		var asOfDate = new DateTimeOffset(2025, 6, 15, 0, 0, 0, TimeSpan.Zero);
		var options = new ReportOptions();

		// Act
		var result = await _sut.GenerateTypeIReportAsync(asOfDate, options, CancellationToken.None);

		// Assert
		result.PeriodStart.ShouldBe(asOfDate);
		result.PeriodEnd.ShouldBe(asOfDate);
	}

	[Fact]
	public async Task GenerateTypeIReportAsync_UseCustomTitle_WhenProvided()
	{
		// Arrange
		SetupDefaultValidation();
		var options = new ReportOptions { CustomTitle = "My Custom Report" };

		// Act
		var result = await _sut.GenerateTypeIReportAsync(DateTimeOffset.UtcNow, options, CancellationToken.None);

		// Assert
		result.Title.ShouldBe("My Custom Report");
	}

	[Fact]
	public async Task GenerateTypeIReportAsync_GenerateDefaultTitle()
	{
		// Arrange
		SetupDefaultValidation();
		var asOfDate = new DateTimeOffset(2025, 3, 20, 0, 0, 0, TimeSpan.Zero);
		var options = new ReportOptions();

		// Act
		var result = await _sut.GenerateTypeIReportAsync(asOfDate, options, CancellationToken.None);

		// Assert
		result.Title.ShouldContain("Type I");
		result.Title.ShouldContain("2025-03-20");
	}

	[Fact]
	public async Task GenerateTypeIReportAsync_UseOptionsCategories()
	{
		// Arrange
		SetupDefaultValidation();
		var options = new ReportOptions
		{
			Categories = [TrustServicesCategory.Availability, TrustServicesCategory.ProcessingIntegrity]
		};

		// Act
		var result = await _sut.GenerateTypeIReportAsync(DateTimeOffset.UtcNow, options, CancellationToken.None);

		// Assert
		result.CategoriesIncluded.ShouldContain(TrustServicesCategory.Availability);
		result.CategoriesIncluded.ShouldContain(TrustServicesCategory.ProcessingIntegrity);
	}

	[Fact]
	public async Task GenerateTypeIReportAsync_UseEnabledCategories_WhenNoneSpecified()
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
		result.System.Services.ShouldNotBeEmpty();
	}

	[Fact]
	public async Task GenerateTypeIReportAsync_UseCustomSystemDescription_WhenConfigured()
	{
		// Arrange
		SetupDefaultValidation();
		_ = A.CallTo(() => _fakeOptions.Value).Returns(new Soc2Options
		{
			EnabledCategories = [TrustServicesCategory.Security],
			SystemDescription = new SystemDescription
			{
				Name = "Custom System",
				Description = "Custom description",
				Services = ["API", "Worker"],
				Infrastructure = ["Azure", "SQL Server"],
				DataTypes = ["User Data", "Transaction Data"]
			}
		});

		var sut = new Soc2ReportGenerator(_fakeOptions, _fakeControlValidation, _fakeLogger, _fakeReportStore);
		var options = new ReportOptions();

		// Act
		var result = await sut.GenerateTypeIReportAsync(DateTimeOffset.UtcNow, options, CancellationToken.None);

		// Assert
		result.System.Name.ShouldBe("Custom System");
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
		var assertionUpperBound1 = DateTimeOffset.UtcNow;
		result.GeneratedAt.ShouldBeLessThanOrEqualTo(assertionUpperBound1);
	}

	[Fact]
	public async Task GenerateTypeIReportAsync_IncludeTenantId()
	{
		// Arrange
		SetupDefaultValidation();
		var options = new ReportOptions { TenantId = "tenant-abc" };

		// Act
		var result = await _sut.GenerateTypeIReportAsync(DateTimeOffset.UtcNow, options, CancellationToken.None);

		// Assert
		result.TenantId.ShouldBe("tenant-abc");
	}

	[Fact]
	public async Task GenerateTypeIReportAsync_DetermineUnqualifiedOpinion_WhenFullyCompliant()
	{
		// Arrange
		SetupValidationWithScore(100, isEffective: true);
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
		SetupValidationWithScore(20, isEffective: false);
		var options = new ReportOptions();

		// Act
		var result = await _sut.GenerateTypeIReportAsync(DateTimeOffset.UtcNow, options, CancellationToken.None);

		// Assert
		result.Opinion.ShouldBe(AuditorOpinion.Adverse);
	}

	#endregion GenerateTypeIReportAsync Tests

	#region GenerateTypeIIReportAsync Tests

	[Fact]
	public async Task GenerateTypeIIReportAsync_ThrowArgumentNullException_WhenOptionsIsNull()
	{
		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentNullException>(
			() => _sut.GenerateTypeIIReportAsync(
				DateTimeOffset.UtcNow.AddDays(-180),
				DateTimeOffset.UtcNow,
				null!, CancellationToken.None));
	}

	[Fact]
	public async Task GenerateTypeIIReportAsync_ThrowException_WhenPeriodEndBeforeStart()
	{
		// Arrange
		var periodStart = DateTimeOffset.UtcNow;
		var periodEnd = DateTimeOffset.UtcNow.AddDays(-30);
		var options = new ReportOptions();

		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentException>(
			() => _sut.GenerateTypeIIReportAsync(periodStart, periodEnd, options, CancellationToken.None));
	}

	[Fact]
	public async Task GenerateTypeIIReportAsync_ThrowException_WhenPeriodTooShort()
	{
		// Arrange
		var periodStart = DateTimeOffset.UtcNow.AddDays(-30);
		var periodEnd = DateTimeOffset.UtcNow;
		var options = new ReportOptions();

		// Act & Assert
		var ex = await Should.ThrowAsync<ArgumentException>(
			() => _sut.GenerateTypeIIReportAsync(periodStart, periodEnd, options, CancellationToken.None));
		ex.Message.ShouldContain("90 days");
	}

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
		_ = A.CallTo(() => _fakeControlValidation.RunControlTestAsync(
				A<string>._,
				A<ControlTestParameters>._,
				A<CancellationToken>._))
			.MustHaveHappened();
	}

	[Fact]
	public async Task GenerateTypeIIReportAsync_NotIncludeTestResults_WhenNotRequested()
	{
		// Arrange
		SetupDefaultValidation();
		var periodStart = DateTimeOffset.UtcNow.AddDays(-180);
		var periodEnd = DateTimeOffset.UtcNow;
		var options = new ReportOptions { IncludeTestResults = false };

		// Act
		var result = await _sut.GenerateTypeIIReportAsync(periodStart, periodEnd, options, CancellationToken.None);

		// Assert
		result.ControlSections.ShouldAllBe(s => s.TestResults == null);
	}

	#endregion GenerateTypeIIReportAsync Tests

	#region GenerateAndStoreReportAsync Tests

	[Fact]
	public async Task GenerateAndStoreReportAsync_ThrowArgumentNullException_WhenRequestIsNull()
	{
		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentNullException>(
			() => _sut.GenerateAndStoreReportAsync(null!, CancellationToken.None));
	}

	[Fact]
	public async Task GenerateAndStoreReportAsync_GenerateTypeIReport()
	{
		// Arrange
		SetupDefaultValidation();
		var request = new ReportGenerationRequest
		{
			ReportType = Soc2ReportType.TypeI,
			PeriodStart = DateTimeOffset.UtcNow,
			Options = new ReportOptions()
		};

		// Act
		var result = await _sut.GenerateAndStoreReportAsync(request, CancellationToken.None);

		// Assert
		result.ReportType.ShouldBe(Soc2ReportType.TypeI);
	}

	[Fact]
	public async Task GenerateAndStoreReportAsync_GenerateTypeIIReport()
	{
		// Arrange
		SetupDefaultValidation();
		var request = new ReportGenerationRequest
		{
			ReportType = Soc2ReportType.TypeII,
			PeriodStart = DateTimeOffset.UtcNow.AddDays(-180),
			PeriodEnd = DateTimeOffset.UtcNow,
			Options = new ReportOptions()
		};

		// Act
		var result = await _sut.GenerateAndStoreReportAsync(request, CancellationToken.None);

		// Assert
		result.ReportType.ShouldBe(Soc2ReportType.TypeII);
	}

	[Fact]
	public async Task GenerateAndStoreReportAsync_ThrowException_WhenTypeIIMissingPeriodEnd()
	{
		// Arrange
		var request = new ReportGenerationRequest
		{
			ReportType = Soc2ReportType.TypeII,
			PeriodStart = DateTimeOffset.UtcNow.AddDays(-180),
			PeriodEnd = null, // Missing
			Options = new ReportOptions()
		};

		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentException>(
			() => _sut.GenerateAndStoreReportAsync(request, CancellationToken.None));
	}

	[Fact]
	public async Task GenerateAndStoreReportAsync_StoreReport_WhenStoreConfigured()
	{
		// Arrange
		SetupDefaultValidation();
		var request = new ReportGenerationRequest
		{
			ReportType = Soc2ReportType.TypeI,
			PeriodStart = DateTimeOffset.UtcNow,
			Options = new ReportOptions()
		};

		// Act
		_ = await _sut.GenerateAndStoreReportAsync(request, CancellationToken.None);

		// Assert
		_ = A.CallTo(() => _fakeReportStore.SaveReportAsync(A<Soc2Report>._, A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task GenerateAndStoreReportAsync_NotStoreReport_WhenNoStoreConfigured()
	{
		// Arrange
		SetupDefaultValidation();
		var sut = new Soc2ReportGenerator(
			_fakeOptions,
			_fakeControlValidation,
			_fakeLogger,
			null); // No store

		var request = new ReportGenerationRequest
		{
			ReportType = Soc2ReportType.TypeI,
			PeriodStart = DateTimeOffset.UtcNow,
			Options = new ReportOptions()
		};

		// Act
		var result = await sut.GenerateAndStoreReportAsync(request, CancellationToken.None);

		// Assert
		_ = result.ShouldNotBeNull();
		A.CallTo(() => _fakeReportStore.SaveReportAsync(A<Soc2Report>._, A<CancellationToken>._))
			.MustNotHaveHappened();
	}

	#endregion GenerateAndStoreReportAsync Tests

	#region GetControlDescriptionsAsync Tests

	[Fact]
	public async Task GetControlDescriptionsAsync_ReturnDescriptions_ForCriterionControls()
	{
		// Arrange
		_ = A.CallTo(() => _fakeControlValidation.GetControlsForCriterion(TrustServicesCriterion.CC6_LogicalAccess))
			.Returns(["SEC-001", "SEC-002"]);

		// Act
		var result = await _sut.GetControlDescriptionsAsync(TrustServicesCriterion.CC6_LogicalAccess, CancellationToken.None);

		// Assert
		result.Count.ShouldBe(2);
		result.ShouldContain(d => d.ControlId == "SEC-001");
		result.ShouldContain(d => d.ControlId == "SEC-002");
	}

	[Fact]
	public async Task GetControlDescriptionsAsync_ReturnEmptyList_WhenNoControls()
	{
		// Arrange
		_ = A.CallTo(() => _fakeControlValidation.GetControlsForCriterion(A<TrustServicesCriterion>._))
			.Returns([]);

		// Act
		var result = await _sut.GetControlDescriptionsAsync(TrustServicesCriterion.CC6_LogicalAccess, CancellationToken.None);

		// Assert
		result.ShouldBeEmpty();
	}

	[Fact]
	public async Task GetControlDescriptionsAsync_IncludeControlMetadata()
	{
		// Arrange
		_ = A.CallTo(() => _fakeControlValidation.GetControlsForCriterion(TrustServicesCriterion.CC6_LogicalAccess))
			.Returns(["SEC-001"]);

		// Act
		var result = await _sut.GetControlDescriptionsAsync(TrustServicesCriterion.CC6_LogicalAccess, CancellationToken.None);

		// Assert
		var control = result[0];
		control.Name.ShouldNotBeNullOrEmpty();
		control.Description.ShouldNotBeNullOrEmpty();
	}

	#endregion GetControlDescriptionsAsync Tests

	#region GetTestResultsAsync Tests

	[Fact]
	public async Task GetTestResultsAsync_ReturnTestResults_ForCriterionControls()
	{
		// Arrange
		_ = A.CallTo(() => _fakeControlValidation.GetControlsForCriterion(TrustServicesCriterion.CC6_LogicalAccess))
			.Returns(["SEC-001"]);
		_ = A.CallTo(() => _fakeControlValidation.RunControlTestAsync(
				"SEC-001",
				A<ControlTestParameters>._,
				A<CancellationToken>._))
			.Returns(new ControlTestResult
			{
				ControlId = "SEC-001",
				Parameters = new ControlTestParameters(),
				ItemsTested = 25,
				ExceptionsFound = 0,
				Outcome = TestOutcome.NoExceptions
			});

		var periodStart = DateTimeOffset.UtcNow.AddDays(-180);
		var periodEnd = DateTimeOffset.UtcNow;

		// Act
		var result = await _sut.GetTestResultsAsync(TrustServicesCriterion.CC6_LogicalAccess, periodStart, periodEnd, CancellationToken.None);

		// Assert
		result.Count.ShouldBe(1);
		result[0].ControlId.ShouldBe("SEC-001");
		result[0].SampleSize.ShouldBe(25);
	}

	[Fact]
	public async Task GetTestResultsAsync_IncludeTestProcedure()
	{
		// Arrange
		_ = A.CallTo(() => _fakeControlValidation.GetControlsForCriterion(TrustServicesCriterion.CC6_LogicalAccess))
			.Returns(["SEC-001"]);
		_ = A.CallTo(() => _fakeControlValidation.RunControlTestAsync(
				A<string>._,
				A<ControlTestParameters>._,
				A<CancellationToken>._))
			.Returns(new ControlTestResult { ControlId = "SEC-001", Parameters = new ControlTestParameters(), ItemsTested = 25, ExceptionsFound = 0, Outcome = TestOutcome.NoExceptions });

		// Act
		var result = await _sut.GetTestResultsAsync(
			TrustServicesCriterion.CC6_LogicalAccess,
			DateTimeOffset.UtcNow.AddDays(-180),
			DateTimeOffset.UtcNow, CancellationToken.None);

		// Assert
		result[0].TestProcedure.ShouldNotBeNullOrEmpty();
	}

	#endregion GetTestResultsAsync Tests

	#region Helper Methods

	private void SetupDefaultValidation()
	{
		_ = A.CallTo(() => _fakeControlValidation.GetControlsForCriterion(A<TrustServicesCriterion>._))
			.Returns(["SEC-001"]);

		_ = A.CallTo(() => _fakeControlValidation.ValidateCriterionAsync(A<TrustServicesCriterion>._, A<CancellationToken>._))
			.Returns(new List<ControlValidationResult>
			{
				new()
				{
					ControlId = "SEC-001",
					IsConfigured = true,
					IsEffective = true,
					EffectivenessScore = 100
				}
			});
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

	private void SetupValidationWithScore(int score, bool isEffective)
	{
		_ = A.CallTo(() => _fakeControlValidation.GetControlsForCriterion(A<TrustServicesCriterion>._))
			.Returns(["SEC-001"]);

		_ = A.CallTo(() => _fakeControlValidation.ValidateCriterionAsync(A<TrustServicesCriterion>._, A<CancellationToken>._))
			.Returns(new List<ControlValidationResult>
			{
				new()
				{
					ControlId = "SEC-001",
					IsConfigured = true,
					IsEffective = isEffective,
					EffectivenessScore = score
				}
			});
	}

	#endregion Helper Methods
}
