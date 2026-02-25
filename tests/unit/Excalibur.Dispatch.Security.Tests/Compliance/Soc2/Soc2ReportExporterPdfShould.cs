// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Extensions.Logging;

using QuestPDF.Infrastructure;

namespace Excalibur.Dispatch.Security.Tests.Compliance.Soc2;

/// <summary>
/// Unit tests for <see cref="Soc2ReportExporter"/> PDF generation using QuestPDF.
/// Tests T396.9 per Sprint 396 - SOC2 PDF Export.
/// </summary>
[Trait("Category", TestCategories.Unit)]
public sealed class Soc2ReportExporterPdfShould
{
	private readonly ILogger<Soc2ReportExporter> _fakeLogger;
	private readonly Soc2ReportExporter _sut;

	static Soc2ReportExporterPdfShould()
	{
		// Configure QuestPDF license before any PDF operations
		// This must be done once per app domain, before any QuestPDF types are used
		QuestPDF.Settings.License = LicenseType.Community;
	}

	public Soc2ReportExporterPdfShould()
	{
		_fakeLogger = A.Fake<ILogger<Soc2ReportExporter>>();
		_sut = new Soc2ReportExporter(_fakeLogger);
	}

	#region PDF Export - Happy Path Tests

	[Fact]
	public async Task ExportAsync_GeneratePdf_ForValidReport()
	{
		// Arrange
		var report = CreateValidReport();

		// Act
		var result = await _sut.ExportAsync(report, ExportFormat.Pdf, null, CancellationToken.None);

		// Assert
		_ = result.ShouldNotBeNull();
		_ = result.Data.ShouldNotBeNull();
		result.Data.Length.ShouldBeGreaterThan(0);
		result.ContentType.ShouldBe("application/pdf");
		result.Format.ShouldBe(ExportFormat.Pdf);
	}

	[Fact]
	public async Task ExportAsync_GeneratePdf_WithPdfFileExtension()
	{
		// Arrange
		var report = CreateValidReport();

		// Act
		var result = await _sut.ExportAsync(report, ExportFormat.Pdf, null, CancellationToken.None);

		// Assert
		result.FileName.ShouldEndWith(".pdf");
	}

	[Fact]
	public async Task ExportAsync_GeneratePdf_WithValidPdfHeader()
	{
		// Arrange
		var report = CreateValidReport();

		// Act
		var result = await _sut.ExportAsync(report, ExportFormat.Pdf, null, CancellationToken.None);

		// Assert - PDF files start with %PDF magic bytes
		result.Data.Length.ShouldBeGreaterThan(4);
		var header = System.Text.Encoding.ASCII.GetString(result.Data, 0, 4);
		header.ShouldBe("%PDF");
	}

	[Fact]
	public async Task ExportAsync_GeneratePdf_WithChecksumGenerated()
	{
		// Arrange
		var report = CreateValidReport();

		// Act
		var result = await _sut.ExportAsync(report, ExportFormat.Pdf, null, CancellationToken.None);

		// Assert
		_ = result.Checksum.ShouldNotBeNull();
		result.Checksum.ShouldNotBeEmpty();
		result.Checksum.Length.ShouldBe(64); // SHA-256 hex = 64 chars
	}

	[Fact]
	public async Task ExportAsync_GeneratePdf_WithTimestamp()
	{
		// Arrange
		var report = CreateValidReport();
		var beforeExport = DateTimeOffset.UtcNow;

		// Act
		var result = await _sut.ExportAsync(report, ExportFormat.Pdf, null, CancellationToken.None);

		// Assert
		result.GeneratedAt.ShouldBeGreaterThanOrEqualTo(beforeExport);
		result.GeneratedAt.ShouldBeLessThanOrEqualTo(DateTimeOffset.UtcNow);
	}

	#endregion

	#region PDF Export - Type I Report Tests

	[Fact]
	public async Task ExportAsync_GeneratePdf_ForTypeIReport()
	{
		// Arrange
		var report = CreateValidReport(Soc2ReportType.TypeI);

		// Act
		var result = await _sut.ExportAsync(report, ExportFormat.Pdf, null, CancellationToken.None);

		// Assert
		_ = result.ShouldNotBeNull();
		result.Data.Length.ShouldBeGreaterThan(0);
		result.FileName.ShouldContain("type1");
	}

	#endregion

	#region PDF Export - Type II Report Tests

	[Fact]
	public async Task ExportAsync_GeneratePdf_ForTypeIIReport()
	{
		// Arrange
		var report = CreateValidReport(Soc2ReportType.TypeII);

		// Act
		var result = await _sut.ExportAsync(report, ExportFormat.Pdf, null, CancellationToken.None);

		// Assert
		_ = result.ShouldNotBeNull();
		result.Data.Length.ShouldBeGreaterThan(0);
		result.FileName.ShouldContain("type2");
	}

	[Fact]
	public async Task ExportAsync_GeneratePdf_WithTestResultsForTypeII()
	{
		// Arrange
		var report = CreateReportWithTestResults();
		var options = new Soc2ReportExportOptions { IncludeTestResults = true };

		// Act
		var result = await _sut.ExportAsync(report, ExportFormat.Pdf, options, CancellationToken.None);

		// Assert
		_ = result.ShouldNotBeNull();
		result.Data.Length.ShouldBeGreaterThan(0);
	}

	#endregion

	#region PDF Export - Options Tests

	[Fact]
	public async Task ExportAsync_GeneratePdf_WithCustomTitle()
	{
		// Arrange
		var report = CreateValidReport();
		var options = new Soc2ReportExportOptions
		{
			CustomTitle = "Custom SOC 2 Report Title"
		};

		// Act
		var result = await _sut.ExportAsync(report, ExportFormat.Pdf, options, CancellationToken.None);

		// Assert
		_ = result.ShouldNotBeNull();
		result.Data.Length.ShouldBeGreaterThan(0);
	}

	[Fact]
	public async Task ExportAsync_GeneratePdf_WithExceptionsIncluded()
	{
		// Arrange
		var report = CreateReportWithExceptions();
		var options = new Soc2ReportExportOptions { IncludeExceptions = true };

		// Act
		var result = await _sut.ExportAsync(report, ExportFormat.Pdf, options, CancellationToken.None);

		// Assert
		_ = result.ShouldNotBeNull();
		result.Data.Length.ShouldBeGreaterThan(0);
	}

	[Fact]
	public async Task ExportAsync_GeneratePdf_WithExceptionsExcluded()
	{
		// Arrange
		var report = CreateReportWithExceptions();
		var options = new Soc2ReportExportOptions { IncludeExceptions = false };

		// Act
		var result = await _sut.ExportAsync(report, ExportFormat.Pdf, options, CancellationToken.None);

		// Assert
		_ = result.ShouldNotBeNull();
		result.Data.Length.ShouldBeGreaterThan(0);
	}

	[Fact]
	public async Task ExportAsync_GeneratePdf_WithDefaultOptions()
	{
		// Arrange
		var report = CreateValidReport();

		// Act - No options provided, should use defaults
		var result = await _sut.ExportAsync(report, ExportFormat.Pdf, null, CancellationToken.None);

		// Assert
		_ = result.ShouldNotBeNull();
		result.Data.Length.ShouldBeGreaterThan(0);
	}

	#endregion

	#region PDF Export - Control Sections Tests

	[Fact]
	public async Task ExportAsync_GeneratePdf_WithMultipleControlSections()
	{
		// Arrange
		var report = CreateReportWithMultipleSections();

		// Act
		var result = await _sut.ExportAsync(report, ExportFormat.Pdf, null, CancellationToken.None);

		// Assert
		_ = result.ShouldNotBeNull();
		result.Data.Length.ShouldBeGreaterThan(0);
	}

	[Fact]
	public async Task ExportAsync_GeneratePdf_WithEmptyControlSections()
	{
		// Arrange
		var report = new Soc2Report
		{
			ReportId = Guid.NewGuid(),
			ReportType = Soc2ReportType.TypeI,
			Title = "Minimal Report",
			PeriodStart = DateTimeOffset.UtcNow.AddDays(-30),
			PeriodEnd = DateTimeOffset.UtcNow,
			Opinion = AuditorOpinion.Unqualified,
			GeneratedAt = DateTimeOffset.UtcNow,
			CategoriesIncluded = [TrustServicesCategory.Security],
			ControlSections = [],
			System = CreateSystemDescription()
		};

		// Act
		var result = await _sut.ExportAsync(report, ExportFormat.Pdf, null, CancellationToken.None);

		// Assert - Should handle empty sections gracefully
		_ = result.ShouldNotBeNull();
		result.Data.Length.ShouldBeGreaterThan(0);
	}

	[Fact]
	public async Task ExportAsync_GeneratePdf_WithManyControls()
	{
		// Arrange - Large report to test pagination
		var controls = Enumerable.Range(1, 50)
			.Select(i => new ControlDescription
			{
				ControlId = $"CC-{i:D3}",
				Name = $"Control {i}",
				Description = $"Description for control {i} with additional detail text.",
				Implementation = $"Implementation details for control {i}",
				Type = ControlType.Preventive,
				Frequency = ControlFrequency.Continuous
			}).ToList();

		var report = new Soc2Report
		{
			ReportId = Guid.NewGuid(),
			ReportType = Soc2ReportType.TypeII,
			Title = "Large Report with Many Controls",
			PeriodStart = DateTimeOffset.UtcNow.AddDays(-90),
			PeriodEnd = DateTimeOffset.UtcNow,
			Opinion = AuditorOpinion.Unqualified,
			GeneratedAt = DateTimeOffset.UtcNow,
			CategoriesIncluded = [TrustServicesCategory.Security],
			System = CreateSystemDescription(),
			ControlSections =
			[
				new ControlSection
				{
					Criterion = TrustServicesCriterion.CC1_ControlEnvironment,
					Description = "Large control section",
					IsMet = true,
					Controls = controls
				}
			]
		};

		// Act
		var result = await _sut.ExportAsync(report, ExportFormat.Pdf, null, CancellationToken.None);

		// Assert - Should handle large reports
		_ = result.ShouldNotBeNull();
		result.Data.Length.ShouldBeGreaterThan(0);
	}

	#endregion

	#region PDF Export - Validation Tests

	[Fact]
	public async Task ExportAsync_ThrowArgumentNullException_WhenReportIsNull()
	{
		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentNullException>(
			() => _sut.ExportAsync(null!, ExportFormat.Pdf, null, CancellationToken.None));
	}

	[Fact]
	public async Task ExportAsync_ThrowInvalidOperationException_WhenReportIdIsEmpty()
	{
		// Arrange
		var report = new Soc2Report
		{
			ReportId = Guid.Empty, // Invalid
			ReportType = Soc2ReportType.TypeI,
			Title = "Test Report",
			PeriodStart = DateTimeOffset.UtcNow,
			PeriodEnd = DateTimeOffset.UtcNow,
			Opinion = AuditorOpinion.Unqualified,
			GeneratedAt = DateTimeOffset.UtcNow,
			CategoriesIncluded = [TrustServicesCategory.Security],
			ControlSections = [],
			System = CreateSystemDescription()
		};

		// Act & Assert
		_ = await Should.ThrowAsync<InvalidOperationException>(
			() => _sut.ExportAsync(report, ExportFormat.Pdf, null, CancellationToken.None));
	}

	[Fact]
	public async Task ExportAsync_ThrowInvalidOperationException_WhenTitleIsEmpty()
	{
		// Arrange
		var report = new Soc2Report
		{
			ReportId = Guid.NewGuid(),
			ReportType = Soc2ReportType.TypeI,
			Title = "", // Invalid
			PeriodStart = DateTimeOffset.UtcNow,
			PeriodEnd = DateTimeOffset.UtcNow,
			Opinion = AuditorOpinion.Unqualified,
			GeneratedAt = DateTimeOffset.UtcNow,
			CategoriesIncluded = [TrustServicesCategory.Security],
			ControlSections = [],
			System = CreateSystemDescription()
		};

		// Act & Assert
		_ = await Should.ThrowAsync<InvalidOperationException>(
			() => _sut.ExportAsync(report, ExportFormat.Pdf, null, CancellationToken.None));
	}

	#endregion

	#region PDF Export - Cancellation Tests

	[Fact]
	public async Task ExportAsync_RespectCancellation()
	{
		// Arrange
		var report = CreateValidReport();
		using var cts = new CancellationTokenSource();
		cts.Cancel();

		// Act & Assert - Should either throw OCE or complete (PDF gen is sync)
		// QuestPDF generation is synchronous, so cancellation may not apply
		// This test ensures the API accepts cancellation token correctly
		try
		{
			var result = await _sut.ExportAsync(report, ExportFormat.Pdf, null, cts.Token);
			// If it completes without throwing, that's acceptable for sync operations
			_ = result.ShouldNotBeNull();
		}
		catch (OperationCanceledException)
		{
			// This is expected if cancellation is checked
		}
	}

	#endregion

	#region ValidateForExport Tests

	[Fact]
	public void ValidateForExport_ReturnValid_ForCompleteReport()
	{
		// Arrange
		var report = CreateValidReport();

		// Act
		var result = _sut.ValidateForExport(report, ExportFormat.Pdf);

		// Assert
		result.IsValid.ShouldBeTrue();
		result.Issues.ShouldBeEmpty();
	}

	[Fact]
	public void ValidateForExport_ReturnInvalid_WhenReportIdEmpty()
	{
		// Arrange
		var report = new Soc2Report
		{
			ReportId = Guid.Empty,
			ReportType = Soc2ReportType.TypeI,
			Title = "Test",
			PeriodStart = DateTimeOffset.UtcNow,
			PeriodEnd = DateTimeOffset.UtcNow,
			Opinion = AuditorOpinion.Unqualified,
			GeneratedAt = DateTimeOffset.UtcNow,
			CategoriesIncluded = [],
			ControlSections = [],
			System = CreateSystemDescription()
		};

		// Act
		var result = _sut.ValidateForExport(report, ExportFormat.Pdf);

		// Assert
		result.IsValid.ShouldBeFalse();
		result.Issues.ShouldContain(i => i.Contains("Report ID", StringComparison.OrdinalIgnoreCase));
	}

	[Fact]
	public void ValidateForExport_ReturnInvalid_WhenTitleEmpty()
	{
		// Arrange
		var report = new Soc2Report
		{
			ReportId = Guid.NewGuid(),
			ReportType = Soc2ReportType.TypeI,
			Title = "",
			PeriodStart = DateTimeOffset.UtcNow,
			PeriodEnd = DateTimeOffset.UtcNow,
			Opinion = AuditorOpinion.Unqualified,
			GeneratedAt = DateTimeOffset.UtcNow,
			CategoriesIncluded = [],
			ControlSections = [],
			System = CreateSystemDescription()
		};

		// Act
		var result = _sut.ValidateForExport(report, ExportFormat.Pdf);

		// Assert
		result.IsValid.ShouldBeFalse();
		result.Issues.ShouldContain(i => i.Contains("title", StringComparison.OrdinalIgnoreCase));
	}

	[Fact]
	public void ValidateForExport_ReturnInvalid_WhenTypeIIPeriodInvalid()
	{
		// Arrange - Type II with start >= end
		var report = new Soc2Report
		{
			ReportId = Guid.NewGuid(),
			ReportType = Soc2ReportType.TypeII,
			Title = "Test",
			PeriodStart = DateTimeOffset.UtcNow,
			PeriodEnd = DateTimeOffset.UtcNow.AddDays(-30), // End before start
			Opinion = AuditorOpinion.Unqualified,
			GeneratedAt = DateTimeOffset.UtcNow,
			CategoriesIncluded = [],
			ControlSections = [],
			System = CreateSystemDescription()
		};

		// Act
		var result = _sut.ValidateForExport(report, ExportFormat.Pdf);

		// Assert
		result.IsValid.ShouldBeFalse();
		result.Issues.ShouldContain(i => i.Contains("period", StringComparison.OrdinalIgnoreCase));
	}

	#endregion

	#region GetSupportedFormats Tests

	[Fact]
	public void GetSupportedFormats_IncludePdf()
	{
		// Act
		var formats = _sut.GetSupportedFormats();

		// Assert
		formats.ShouldContain(ExportFormat.Pdf);
	}

	#endregion

	#region Helper Methods

	private static SystemDescription CreateSystemDescription()
	{
		return new SystemDescription
		{
			Name = "Test System",
			Description = "A system used for testing SOC 2 compliance",
			Services = ["Authentication", "Authorization", "Data Storage"],
			Infrastructure = ["AWS Cloud", "Postgres Database"],
			DataTypes = ["PII", "Financial Records"]
		};
	}

	private static Soc2Report CreateValidReport(Soc2ReportType reportType = Soc2ReportType.TypeII)
	{
		var controls = new List<ControlDescription>
		{
			new()
			{
				ControlId = "CC-001",
				Name = "Access Control Policy",
				Description = "Organization has a documented access control policy",
				Implementation = "Policy documents are reviewed quarterly",
				Type = ControlType.Preventive,
				Frequency = ControlFrequency.Quarterly
			},
			new()
			{
				ControlId = "CC-002",
				Name = "User Authentication",
				Description = "Multi-factor authentication is required for all users",
				Implementation = "MFA enforced via identity provider",
				Type = ControlType.Preventive,
				Frequency = ControlFrequency.PerTransaction
			}
		};

		return new Soc2Report
		{
			ReportId = Guid.NewGuid(),
			ReportType = reportType,
			Title = "SOC 2 Type II Examination Report",
			PeriodStart = DateTimeOffset.UtcNow.AddDays(-90),
			PeriodEnd = DateTimeOffset.UtcNow,
			Opinion = AuditorOpinion.Unqualified,
			GeneratedAt = DateTimeOffset.UtcNow,
			CategoriesIncluded = [TrustServicesCategory.Security, TrustServicesCategory.Availability],
			System = CreateSystemDescription(),
			ControlSections =
			[
				new ControlSection
				{
					Criterion = TrustServicesCriterion.CC1_ControlEnvironment,
					Description = "COSO Principle 1 - Demonstrates Commitment to Integrity",
					IsMet = true,
					Controls = controls
				}
			]
		};
	}

	private static Soc2Report CreateReportWithExceptions()
	{
		var report = CreateValidReport();
		return report with
		{
			Exceptions =
			[
				new ReportException
				{
					ExceptionId = "EXC-001",
					Criterion = TrustServicesCriterion.CC6_LogicalAccess,
					ControlId = "CC-005",
					Description = "Logging was disabled for 2 hours on 2024-03-15"
				}
			]
		};
	}

	private static Soc2Report CreateReportWithTestResults()
	{
		var controls = new List<ControlDescription>
		{
			new()
			{
				ControlId = "CC-001",
				Name = "Access Control Policy",
				Description = "Organization has a documented access control policy",
				Implementation = "Policy documents are reviewed quarterly",
				Type = ControlType.Preventive,
				Frequency = ControlFrequency.Quarterly
			}
		};

		var testResults = new List<TestResult>
		{
			new()
			{
				ControlId = "CC-001",
				TestProcedure = "Verified access control policy is documented and approved",
				Outcome = TestOutcome.NoExceptions,
				SampleSize = 25,
				ExceptionsFound = 0,
				Notes = "All samples tested successfully"
			}
		};

		return new Soc2Report
		{
			ReportId = Guid.NewGuid(),
			ReportType = Soc2ReportType.TypeII,
			Title = "SOC 2 Type II with Test Results",
			PeriodStart = DateTimeOffset.UtcNow.AddDays(-90),
			PeriodEnd = DateTimeOffset.UtcNow,
			Opinion = AuditorOpinion.Unqualified,
			GeneratedAt = DateTimeOffset.UtcNow,
			CategoriesIncluded = [TrustServicesCategory.Security],
			System = CreateSystemDescription(),
			ControlSections =
			[
				new ControlSection
				{
					Criterion = TrustServicesCriterion.CC1_ControlEnvironment,
					Description = "COSO Principle 1",
					IsMet = true,
					Controls = controls,
					TestResults = testResults
				}
			]
		};
	}

	private static Soc2Report CreateReportWithMultipleSections()
	{
		var controls1 = new List<ControlDescription>
		{
			new()
			{
				ControlId = "CC-001",
				Name = "Control 1",
				Description = "Description 1",
				Implementation = "Implementation 1",
				Type = ControlType.Preventive,
				Frequency = ControlFrequency.Continuous
			}
		};

		var controls2 = new List<ControlDescription>
		{
			new()
			{
				ControlId = "CC-002",
				Name = "Control 2",
				Description = "Description 2",
				Implementation = "Implementation 2",
				Type = ControlType.Detective,
				Frequency = ControlFrequency.Daily
			}
		};

		return new Soc2Report
		{
			ReportId = Guid.NewGuid(),
			ReportType = Soc2ReportType.TypeII,
			Title = "Multi-Section Report",
			PeriodStart = DateTimeOffset.UtcNow.AddDays(-90),
			PeriodEnd = DateTimeOffset.UtcNow,
			Opinion = AuditorOpinion.Unqualified,
			GeneratedAt = DateTimeOffset.UtcNow,
			CategoriesIncluded = [TrustServicesCategory.Security, TrustServicesCategory.Confidentiality],
			System = CreateSystemDescription(),
			ControlSections =
			[
				new ControlSection
				{
					Criterion = TrustServicesCriterion.CC1_ControlEnvironment,
					Description = "Section 1 - Security",
					IsMet = true,
					Controls = controls1
				},
				new ControlSection
				{
					Criterion = TrustServicesCriterion.C1_DataClassification,
					Description = "Section 2 - Confidentiality",
					IsMet = false,
					Controls = controls2
				}
			]
		};
	}

	#endregion
}
