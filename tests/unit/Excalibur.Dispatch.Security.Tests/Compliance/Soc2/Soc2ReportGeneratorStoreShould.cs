// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Security.Tests.Compliance.Soc2;

/// <summary>
/// Unit tests for <see cref="Soc2ReportGenerator"/>.GenerateAndStoreReportAsync
/// and edge cases in report generation.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("Component", "Compliance")]
public sealed class Soc2ReportGeneratorStoreShould
{
	private readonly IControlValidationService _controlValidation;
	private readonly ISoc2ReportStore _reportStore;
	private readonly Soc2Options _soc2Options;

	public Soc2ReportGeneratorStoreShould()
	{
		_controlValidation = A.Fake<IControlValidationService>();
		_reportStore = A.Fake<ISoc2ReportStore>();
		_soc2Options = new Soc2Options
		{
			EnabledCategories = [TrustServicesCategory.Security],
			MinimumTypeIIPeriodDays = 30,
			DefaultTestSampleSize = 25
		};

		// Setup default behaviors for control validation
		A.CallTo(() => _controlValidation.GetControlsForCriterion(A<TrustServicesCriterion>._))
			.Returns(new List<string> { "SEC-001" });
		A.CallTo(() => _controlValidation.ValidateCriterionAsync(A<TrustServicesCriterion>._, A<CancellationToken>._))
			.Returns(new List<ControlValidationResult>
			{
				new()
				{
					ControlId = "SEC-001",
					IsConfigured = true,
					IsEffective = true,
					EffectivenessScore = 95
				}
			});
		A.CallTo(() => _controlValidation.RunControlTestAsync(A<string>._, A<ControlTestParameters>._, A<CancellationToken>._))
			.Returns(new ControlTestResult
			{
				ControlId = "SEC-001",
				Parameters = new ControlTestParameters { SampleSize = 25 },
				ItemsTested = 25,
				ExceptionsFound = 0,
				Outcome = TestOutcome.NoExceptions,
				Exceptions = []
			});
	}

	#region Constructor Tests

	[Fact]
	public void Constructor_ThrowsArgumentNullException_WhenOptionsIsNull()
	{
		_ = Should.Throw<ArgumentNullException>(() => new Soc2ReportGenerator(
			null!,
			_controlValidation,
			NullLogger<Soc2ReportGenerator>.Instance));
	}

	[Fact]
	public void Constructor_ThrowsArgumentNullException_WhenControlValidationIsNull()
	{
		_ = Should.Throw<ArgumentNullException>(() => new Soc2ReportGenerator(
			Microsoft.Extensions.Options.Options.Create(_soc2Options),
			null!,
			NullLogger<Soc2ReportGenerator>.Instance));
	}

	[Fact]
	public void Constructor_ThrowsArgumentNullException_WhenLoggerIsNull()
	{
		_ = Should.Throw<ArgumentNullException>(() => new Soc2ReportGenerator(
			Microsoft.Extensions.Options.Options.Create(_soc2Options),
			_controlValidation,
			null!));
	}

	#endregion

	#region GenerateAndStoreReportAsync Tests

	[Fact]
	public async Task GenerateAndStoreReportAsync_ThrowsArgumentNullException_WhenRequestIsNull()
	{
		// Arrange
		var sut = CreateSut(withStore: true);

		// Act & Assert
		await Should.ThrowAsync<ArgumentNullException>(
			() => sut.GenerateAndStoreReportAsync(null!, CancellationToken.None)).ConfigureAwait(false);
	}

	[Fact]
	public async Task GenerateAndStoreReportAsync_GeneratesTypeI_AndStores()
	{
		// Arrange
		var sut = CreateSut(withStore: true);
		var request = new ReportGenerationRequest
		{
			ReportType = Soc2ReportType.TypeI,
			PeriodStart = DateTimeOffset.UtcNow,
			Options = new ReportOptions()
		};

		// Act
		var report = await sut.GenerateAndStoreReportAsync(request, CancellationToken.None).ConfigureAwait(false);

		// Assert
		report.ShouldNotBeNull();
		report.ReportType.ShouldBe(Soc2ReportType.TypeI);
		A.CallTo(() => _reportStore.SaveReportAsync(A<Soc2Report>._, A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task GenerateAndStoreReportAsync_GeneratesTypeII_AndStores()
	{
		// Arrange
		var sut = CreateSut(withStore: true);
		var request = new ReportGenerationRequest
		{
			ReportType = Soc2ReportType.TypeII,
			PeriodStart = DateTimeOffset.UtcNow.AddDays(-90),
			PeriodEnd = DateTimeOffset.UtcNow,
			Options = new ReportOptions { IncludeTestResults = true }
		};

		// Act
		var report = await sut.GenerateAndStoreReportAsync(request, CancellationToken.None).ConfigureAwait(false);

		// Assert
		report.ShouldNotBeNull();
		report.ReportType.ShouldBe(Soc2ReportType.TypeII);
		A.CallTo(() => _reportStore.SaveReportAsync(A<Soc2Report>._, A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task GenerateAndStoreReportAsync_ThrowsArgumentException_WhenTypeIIMissingPeriodEnd()
	{
		// Arrange
		var sut = CreateSut(withStore: true);
		var request = new ReportGenerationRequest
		{
			ReportType = Soc2ReportType.TypeII,
			PeriodStart = DateTimeOffset.UtcNow.AddDays(-90),
			PeriodEnd = null,
			Options = new ReportOptions()
		};

		// Act & Assert
		await Should.ThrowAsync<ArgumentException>(
			() => sut.GenerateAndStoreReportAsync(request, CancellationToken.None)).ConfigureAwait(false);
	}

	[Fact]
	public async Task GenerateAndStoreReportAsync_DoesNotStore_WhenNoStoreConfigured()
	{
		// Arrange
		var sut = CreateSut(withStore: false);
		var request = new ReportGenerationRequest
		{
			ReportType = Soc2ReportType.TypeI,
			PeriodStart = DateTimeOffset.UtcNow,
			Options = new ReportOptions()
		};

		// Act
		var report = await sut.GenerateAndStoreReportAsync(request, CancellationToken.None).ConfigureAwait(false);

		// Assert
		report.ShouldNotBeNull();
		A.CallTo(() => _reportStore.SaveReportAsync(A<Soc2Report>._, A<CancellationToken>._))
			.MustNotHaveHappened();
	}

	#endregion

	#region GenerateTypeIIReportAsync - Validation Tests

	[Fact]
	public async Task GenerateTypeIIReportAsync_ThrowsArgumentException_WhenPeriodEndBeforeStart()
	{
		// Arrange
		var sut = CreateSut(withStore: false);
		var options = new ReportOptions();

		// Act & Assert
		await Should.ThrowAsync<ArgumentException>(
			() => sut.GenerateTypeIIReportAsync(
				DateTimeOffset.UtcNow,
				DateTimeOffset.UtcNow.AddDays(-1),
				options,
				CancellationToken.None)).ConfigureAwait(false);
	}

	[Fact]
	public async Task GenerateTypeIIReportAsync_ThrowsArgumentException_WhenPeriodTooShort()
	{
		// Arrange
		var sut = CreateSut(withStore: false);
		var options = new ReportOptions();

		// Act & Assert - 30 day minimum, only providing 10 days
		await Should.ThrowAsync<ArgumentException>(
			() => sut.GenerateTypeIIReportAsync(
				DateTimeOffset.UtcNow.AddDays(-10),
				DateTimeOffset.UtcNow,
				options,
				CancellationToken.None)).ConfigureAwait(false);
	}

	[Fact]
	public async Task GenerateTypeIIReportAsync_ThrowsArgumentNullException_WhenOptionsIsNull()
	{
		// Arrange
		var sut = CreateSut(withStore: false);

		// Act & Assert
		await Should.ThrowAsync<ArgumentNullException>(
			() => sut.GenerateTypeIIReportAsync(
				DateTimeOffset.UtcNow.AddDays(-90),
				DateTimeOffset.UtcNow,
				null!,
				CancellationToken.None)).ConfigureAwait(false);
	}

	#endregion

	#region GenerateTypeIReportAsync Tests

	[Fact]
	public async Task GenerateTypeIReportAsync_ThrowsArgumentNullException_WhenOptionsIsNull()
	{
		// Arrange
		var sut = CreateSut(withStore: false);

		// Act & Assert
		await Should.ThrowAsync<ArgumentNullException>(
			() => sut.GenerateTypeIReportAsync(
				DateTimeOffset.UtcNow,
				null!,
				CancellationToken.None)).ConfigureAwait(false);
	}

	[Fact]
	public async Task GenerateTypeIReportAsync_UsesCustomTitle_WhenProvided()
	{
		// Arrange
		var sut = CreateSut(withStore: false);
		var options = new ReportOptions { CustomTitle = "Custom Title Report" };

		// Act
		var report = await sut.GenerateTypeIReportAsync(
			DateTimeOffset.UtcNow,
			options,
			CancellationToken.None).ConfigureAwait(false);

		// Assert
		report.Title.ShouldBe("Custom Title Report");
	}

	[Fact]
	public async Task GenerateTypeIReportAsync_UsesCustomCategories_WhenProvided()
	{
		// Arrange
		var sut = CreateSut(withStore: false);
		var options = new ReportOptions
		{
			Categories = [TrustServicesCategory.Security, TrustServicesCategory.Availability]
		};

		// Act
		var report = await sut.GenerateTypeIReportAsync(
			DateTimeOffset.UtcNow,
			options,
			CancellationToken.None).ConfigureAwait(false);

		// Assert
		report.CategoriesIncluded.ShouldContain(TrustServicesCategory.Security);
		report.CategoriesIncluded.ShouldContain(TrustServicesCategory.Availability);
	}

	[Fact]
	public async Task GenerateTypeIReportAsync_ReturnsUnqualifiedOpinion_WhenFullyCompliant()
	{
		// Arrange
		var sut = CreateSut(withStore: false);

		// Act
		var report = await sut.GenerateTypeIReportAsync(
			DateTimeOffset.UtcNow,
			new ReportOptions(),
			CancellationToken.None).ConfigureAwait(false);

		// Assert
		report.Opinion.ShouldBe(AuditorOpinion.Unqualified);
		report.Exceptions.ShouldBeEmpty();
	}

	[Fact]
	public async Task GenerateTypeIReportAsync_ReturnsAdverseOpinion_WhenNonCompliant()
	{
		// Arrange - configure validation to return low scores
		A.CallTo(() => _controlValidation.ValidateCriterionAsync(A<TrustServicesCriterion>._, A<CancellationToken>._))
			.Returns(new List<ControlValidationResult>
			{
				new()
				{
					ControlId = "SEC-001",
					IsConfigured = true,
					IsEffective = false,
					EffectivenessScore = 20
				}
			});

		var sut = CreateSut(withStore: false);

		// Act
		var report = await sut.GenerateTypeIReportAsync(
			DateTimeOffset.UtcNow,
			new ReportOptions(),
			CancellationToken.None).ConfigureAwait(false);

		// Assert
		report.Opinion.ShouldBe(AuditorOpinion.Adverse);
		report.Exceptions.ShouldNotBeEmpty();
	}

	#endregion

	#region GetControlDescriptionsAsync Tests

	[Fact]
	public async Task GetControlDescriptionsAsync_ReturnsDescriptions()
	{
		// Arrange
		var sut = CreateSut(withStore: false);
		A.CallTo(() => _controlValidation.GetControlsForCriterion(TrustServicesCriterion.CC6_LogicalAccess))
			.Returns(new List<string> { "SEC-001", "SEC-002" });

		// Act
		var results = await sut.GetControlDescriptionsAsync(TrustServicesCriterion.CC6_LogicalAccess, CancellationToken.None).ConfigureAwait(false);

		// Assert
		results.Count.ShouldBe(2);
	}

	#endregion

	#region Helpers

	private Soc2ReportGenerator CreateSut(bool withStore)
	{
		return new Soc2ReportGenerator(
			Microsoft.Extensions.Options.Options.Create(_soc2Options),
			_controlValidation,
			NullLogger<Soc2ReportGenerator>.Instance,
			withStore ? _reportStore : null);
	}

	#endregion
}
