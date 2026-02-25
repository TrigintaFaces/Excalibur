// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Compliance;

using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Dispatch.Compliance.Tests.Soc2;

[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class Soc2ReportExporterShould
{
	private readonly Soc2ReportExporter _sut;

	public Soc2ReportExporterShould()
	{
		_sut = new Soc2ReportExporter(NullLogger<Soc2ReportExporter>.Instance);
	}

	[Fact]
	public void ThrowWhenLoggerIsNull()
	{
		Should.Throw<ArgumentNullException>(() => new Soc2ReportExporter(null!));
	}

	[Fact]
	public void ReturnSupportedFormats()
	{
		// Act
		var formats = _sut.GetSupportedFormats();

		// Assert
		formats.ShouldNotBeEmpty();
		formats.ShouldContain(ExportFormat.Json);
		formats.ShouldContain(ExportFormat.Csv);
		formats.ShouldContain(ExportFormat.Xml);
		formats.ShouldContain(ExportFormat.Pdf);
		formats.ShouldContain(ExportFormat.Excel);
	}

	[Fact]
	public void ValidateReportWithEmptyId()
	{
		// Arrange
		var report = CreateReport(reportId: Guid.Empty);

		// Act
		var result = _sut.ValidateForExport(report, ExportFormat.Json);

		// Assert
		result.IsValid.ShouldBeFalse();
		result.Issues.ShouldContain(i => i.Contains("Report ID"));
	}

	[Fact]
	public void ValidateReportWithEmptyTitle()
	{
		// Arrange
		var report = CreateReport(title: "");

		// Act
		var result = _sut.ValidateForExport(report, ExportFormat.Json);

		// Assert
		result.IsValid.ShouldBeFalse();
		result.Issues.ShouldContain(i => i.Contains("title"));
	}

	[Fact]
	public void ValidateReportWithNoControlSections()
	{
		// Arrange
		var report = CreateReport();

		// Act
		var result = _sut.ValidateForExport(report, ExportFormat.Json);

		// Assert
		result.IsValid.ShouldBeTrue();
		result.Warnings.ShouldContain(w => w.Contains("no control sections"));
	}

	[Fact]
	public void ValidateTypeIIReportWithInvalidPeriod()
	{
		// Arrange — End before start
		var report = CreateReport(
			reportType: Soc2ReportType.TypeII,
			periodStart: DateTimeOffset.UtcNow,
			periodEnd: DateTimeOffset.UtcNow.AddDays(-1));

		// Act
		var result = _sut.ValidateForExport(report, ExportFormat.Json);

		// Assert
		result.IsValid.ShouldBeFalse();
		result.Issues.ShouldContain(i => i.Contains("period"));
	}

	[Fact]
	public void ValidateReportForPdfWithoutSystemDescription()
	{
		// Arrange — null System via record with expression
		var report = CreateReportWithControlSections() with { System = null! };

		// Act
		var result = _sut.ValidateForExport(report, ExportFormat.Pdf);

		// Assert
		result.IsValid.ShouldBeTrue();
		result.Warnings.ShouldContain(w => w.Contains("System description"));
	}

	[Fact]
	public void ValidateValidReport()
	{
		// Arrange
		var report = CreateReportWithControlSections();

		// Act
		var result = _sut.ValidateForExport(report, ExportFormat.Json);

		// Assert
		result.IsValid.ShouldBeTrue();
		result.Issues.ShouldBeEmpty();
	}

	[Fact]
	public async Task ExportToJson()
	{
		// Arrange
		var report = CreateReportWithControlSections();

		// Act
		var result = await _sut.ExportAsync(report, ExportFormat.Json, null, CancellationToken.None);

		// Assert
		result.ShouldNotBeNull();
		result.Data.ShouldNotBeEmpty();
		result.ContentType.ShouldBe("application/json");
		result.Format.ShouldBe(ExportFormat.Json);
		result.FileName.ShouldContain(".json");
		result.Checksum.ShouldNotBeNullOrEmpty();
	}

	[Fact]
	public async Task ExportToCsv()
	{
		// Arrange
		var report = CreateReportWithControlSections();

		// Act
		var result = await _sut.ExportAsync(report, ExportFormat.Csv, null, CancellationToken.None);

		// Assert
		result.Data.ShouldNotBeEmpty();
		result.ContentType.ShouldBe("text/csv");
		result.FileName.ShouldContain(".csv");
	}

	[Fact]
	public async Task ExportToXml()
	{
		// Arrange
		var report = CreateReportWithControlSections();

		// Act
		var result = await _sut.ExportAsync(report, ExportFormat.Xml, null, CancellationToken.None);

		// Assert
		result.Data.ShouldNotBeEmpty();
		result.ContentType.ShouldBe("application/xml");
		result.FileName.ShouldContain(".xml");
	}

	[Fact]
	public async Task ExportToPdf()
	{
		// Arrange
		var report = CreateReportWithControlSections();

		// Act
		var result = await _sut.ExportAsync(report, ExportFormat.Pdf, null, CancellationToken.None);

		// Assert
		result.Data.ShouldNotBeEmpty();
		result.ContentType.ShouldBe("application/pdf");
		result.FileName.ShouldContain(".pdf");
	}

	[Fact]
	public async Task ExportToExcel()
	{
		// Arrange
		var report = CreateReportWithControlSections();

		// Act
		var result = await _sut.ExportAsync(report, ExportFormat.Excel, null, CancellationToken.None);

		// Assert
		result.Data.ShouldNotBeEmpty();
		result.ContentType.ShouldBe("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
		result.FileName.ShouldContain(".xlsx");
	}

	[Fact]
	public async Task CompressJsonExport()
	{
		// Arrange
		var report = CreateReportWithControlSections();
		var options = new Soc2ReportExportOptions { Compress = true };

		// Act
		var result = await _sut.ExportAsync(report, ExportFormat.Json, options, CancellationToken.None);

		// Assert
		result.ContentType.ShouldBe("application/gzip");
		result.FileName.ShouldContain(".gz");
	}

	[Fact]
	public async Task ThrowWhenReportIsNull()
	{
		await Should.ThrowAsync<ArgumentNullException>(
			() => _sut.ExportAsync(null!, ExportFormat.Json, null, CancellationToken.None));
	}

	[Fact]
	public async Task ThrowWhenReportInvalidForExport()
	{
		// Arrange - empty report ID
		var report = CreateReport(reportId: Guid.Empty);

		// Act & Assert
		await Should.ThrowAsync<InvalidOperationException>(
			() => _sut.ExportAsync(report, ExportFormat.Json, null, CancellationToken.None));
	}

	[Fact]
	public async Task ExportWithEvidence()
	{
		// Arrange
		var report = CreateReportWithControlSections();
		var evidence = new List<AuditEvidence>
		{
			new()
			{
				Criterion = TrustServicesCriterion.CC6_LogicalAccess,
				PeriodStart = DateTimeOffset.UtcNow.AddMonths(-12),
				PeriodEnd = DateTimeOffset.UtcNow,
				Items =
				[
					new EvidenceItem
					{
						EvidenceId = "EV-001",
						Description = "Test evidence",
						Type = EvidenceType.Policy,
						Source = "test",
						CollectedAt = DateTimeOffset.UtcNow
					}
				],
				Summary = new EvidenceSummary
				{
					TotalItems = 1,
					ByType = new Dictionary<EvidenceType, int> { [EvidenceType.Policy] = 1 },
					AuditLogEntries = 0,
					ConfigurationSnapshots = 0,
					TestResults = 0
				},
				ChainOfCustodyHash = "abc123"
			}
		};

		// Act
		var result = await _sut.ExportWithEvidenceAsync(
			report, evidence, null, CancellationToken.None);

		// Assert
		result.ShouldNotBeNull();
		result.ContentType.ShouldBe("application/zip");
		result.Data.ShouldNotBeEmpty();
	}

	[Fact]
	public async Task ThrowWhenReportIsNull_ExportWithEvidence()
	{
		await Should.ThrowAsync<ArgumentNullException>(
			() => _sut.ExportWithEvidenceAsync(null!, [], null, CancellationToken.None));
	}

	[Fact]
	public async Task ThrowWhenEvidenceIsNull_ExportWithEvidence()
	{
		var report = CreateReportWithControlSections();
		await Should.ThrowAsync<ArgumentNullException>(
			() => _sut.ExportWithEvidenceAsync(report, null!, null, CancellationToken.None));
	}

	[Fact]
	public async Task IncludeExceptionsInExport()
	{
		// Arrange
		var report = CreateReportWithControlSections(exceptions:
		[
			new ReportException
			{
				ExceptionId = "EXC-001",
				Criterion = TrustServicesCriterion.CC6_LogicalAccess,
				ControlId = "CTL-001",
				Description = "Test exception"
			}
		]);
		var options = new Soc2ReportExportOptions { IncludeExceptions = true };

		// Act
		var result = await _sut.ExportAsync(report, ExportFormat.Xml, options, CancellationToken.None);

		// Assert
		result.Data.ShouldNotBeEmpty();
		var xml = System.Text.Encoding.UTF8.GetString(result.Data);
		xml.ShouldContain("EXC-001");
	}

	[Fact]
	public async Task ExportTypeIReportWithCorrectFileName()
	{
		// Arrange
		var report = CreateReportWithControlSections(reportType: Soc2ReportType.TypeI);

		// Act
		var result = await _sut.ExportAsync(report, ExportFormat.Json, null, CancellationToken.None);

		// Assert
		result.FileName.ShouldContain("type1");
	}

	private static Soc2Report CreateReport(
		Guid? reportId = null,
		string? title = null,
		Soc2ReportType reportType = Soc2ReportType.TypeII,
		DateTimeOffset? periodStart = null,
		DateTimeOffset? periodEnd = null)
	{
		return new Soc2Report
		{
			ReportId = reportId ?? Guid.NewGuid(),
			Title = title ?? "Test SOC 2 Report",
			ReportType = reportType,
			PeriodStart = periodStart ?? DateTimeOffset.UtcNow.AddMonths(-12),
			PeriodEnd = periodEnd ?? DateTimeOffset.UtcNow,
			Opinion = AuditorOpinion.Unqualified,
			GeneratedAt = DateTimeOffset.UtcNow,
			System = new SystemDescription
			{
				Name = "Test System",
				Description = "A test system",
				Services = ["Service A"],
				Infrastructure = ["Server 1"],
				DataTypes = ["PII"]
			},
			ControlSections = [],
			CategoriesIncluded = [TrustServicesCategory.Security]
		};
	}

	private static Soc2Report CreateReportWithControlSections(
		Soc2ReportType reportType = Soc2ReportType.TypeII,
		IReadOnlyList<ReportException>? exceptions = null)
	{
		return new Soc2Report
		{
			ReportId = Guid.NewGuid(),
			Title = "Test SOC 2 Report",
			ReportType = reportType,
			PeriodStart = DateTimeOffset.UtcNow.AddMonths(-12),
			PeriodEnd = DateTimeOffset.UtcNow,
			Opinion = AuditorOpinion.Unqualified,
			GeneratedAt = DateTimeOffset.UtcNow,
			System = new SystemDescription
			{
				Name = "Test System",
				Description = "A test system",
				Services = ["Service A"],
				Infrastructure = ["Server 1"],
				DataTypes = ["PII"]
			},
			ControlSections =
			[
				new ControlSection
				{
					Criterion = TrustServicesCriterion.CC6_LogicalAccess,
					Description = "Logical and Physical Access Controls",
					IsMet = true,
					Controls =
					[
						new ControlDescription
						{
							ControlId = "CTL-001",
							Name = "Access Control",
							Description = "Implements logical access controls",
							Implementation = "RBAC with MFA",
							Type = ControlType.Preventive,
							Frequency = ControlFrequency.Continuous
						}
					]
				}
			],
			CategoriesIncluded = [TrustServicesCategory.Security],
			Exceptions = exceptions ?? []
		};
	}
}
