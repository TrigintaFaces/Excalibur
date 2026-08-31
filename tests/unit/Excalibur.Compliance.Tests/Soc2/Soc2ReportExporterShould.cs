// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.IO.Compression;
using System.Security.Cryptography;

using Excalibur.Compliance;
using Excalibur.Compliance.Pdf;
using Excalibur.Compliance.Soc2;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

namespace Excalibur.Compliance.Tests.Soc2;

[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
[Collection(QuestPdfLicenseCollection.Name)]
public sealed class Soc2ReportExporterShould
{
	private readonly Soc2ReportExporter _sut;

	public Soc2ReportExporterShould()
	{
		// The PDF renderer stands in for the Excalibur.Compliance.Pdf package a consumer installs when
		// they want PDF export. The behaviour when it is absent is covered by Soc2PdfExportOptInShould.
		_sut = new Soc2ReportExporter(
				NullLogger<Soc2ReportExporter>.Instance,
				TimeProvider.System,
				new QuestPdfSoc2PdfRenderer(TimeProvider.System));
	}

	[Fact]
	public void ThrowWhenLoggerIsNull()
	{
		Should.Throw<ArgumentNullException>(() => new Soc2ReportExporter(null!, TimeProvider.System));
	}

	[Fact]
	public void ThrowWhenTimeProviderIsNull()
	{
		Should.Throw<ArgumentNullException>(() => new Soc2ReportExporter(NullLogger<Soc2ReportExporter>.Instance, null!));
	}

	[Fact]
	public async Task UseInjectedTimeProviderForGeneratedAt()
	{
		// Arrange
		var fixedTime = new DateTimeOffset(2026, 6, 15, 10, 30, 0, TimeSpan.Zero);
		var fakeTimeProvider = new FakeTimeProvider(fixedTime);
		var sut = new Soc2ReportExporter(NullLogger<Soc2ReportExporter>.Instance, fakeTimeProvider);
		var report = CreateReportWithControlSections();

		// Act
		var result = await sut.ExportAsync(report, ExportFormat.Json, null, CancellationToken.None);

		// Assert
		result.GeneratedAt.ShouldBe(fixedTime);
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

	// --- Evidence-package encryption: the option is refused, never silently dropped. ---
	//
	// The package is a plain ZIP and ZipArchive cannot encrypt entries. The failure this pair locks is
	// the one that leaves no trace: a caller sets a password, gets bytes back, and hands an auditor an
	// unencrypted evidence package believing it is protected. Both arms are on the SAME options object
	// so the only difference between them is the password itself.

	[Fact]
	public async Task RefuseEvidencePackageWhenAnEncryptionPasswordIsSet()
	{
		var report = CreateReport();

		var error = await Should.ThrowAsync<NotSupportedException>(
			() => _sut.ExportWithEvidenceAsync(
				report,
				[],
				new EvidencePackageOptions { EncryptionPassword = "correct-horse" },
				CancellationToken.None));

		// Names the option and the remedy, so the refusal is actionable without reading our source.
		error.Message.ShouldContain(nameof(EvidencePackageOptions.EncryptionPassword));
		error.Message.ShouldContain("encrypt ExportResult.Data yourself");

		// And the password itself never reaches the message.
		error.Message.ShouldNotContain("correct-horse");
	}

	[Fact]
	public async Task ProduceEvidencePackageWhenNoEncryptionPasswordIsSet()
	{
		var report = CreateReport();

		var result = await _sut.ExportWithEvidenceAsync(
			report,
			[],
			new EvidencePackageOptions(),
			CancellationToken.None);

		result.Data.ShouldNotBeEmpty();
		result.ContentType.ShouldBe("application/zip");
	}

	[Fact]
	public async Task ChecksumTheFilesTheEvidencePackageActuallyEmbeds()
	{
		// The load-bearing property is that an auditor can verify the package, not that a checksums file
		// exists. The predicate behind the old listing could never match, so the shipped default handed an
		// auditor a checksums file that named nothing while the package carried real report bytes.
		var report = CreateReport();

		var result = await _sut.ExportWithEvidenceAsync(
			report,
			[],
			new EvidencePackageOptions { IncludeChecksums = true },
			CancellationToken.None);

		using var archive = new ZipArchive(new MemoryStream(result.Data), ZipArchiveMode.Read);

		var embedded = archive.Entries
			.Where(e => e.FullName.StartsWith("report/", StringComparison.Ordinal))
			.ToList();
		embedded.ShouldNotBeEmpty("the package must embed at least one report file for this arm to mean anything");

		var checksumEntry = archive.GetEntry("checksums.sha256");
		_ = checksumEntry.ShouldNotBeNull();

		using var reader = new StreamReader(checksumEntry.Open());
		var text = await reader.ReadToEndAsync();

		var listed = text.ReplaceLineEndings("\n").Split('\n')
			.Select(l => l.Trim())
			.Where(l => l.Length > 0 && !l.StartsWith('#'))
			.ToList();

		listed.Count.ShouldBe(
			embedded.Count,
			"every file the package embeds must be listed, or the checksums file asserts integrity coverage it does not have");

		foreach (var entry in embedded)
		{
			using var entryStream = entry.Open();
			using var buffer = new MemoryStream();
			await entryStream.CopyToAsync(buffer);
			var expected = Convert.ToHexStringLower(SHA256.HashData(buffer.ToArray()));

			listed.ShouldContain(
				$"{expected}  {entry.FullName}",
				$"the listed checksum for {entry.FullName} must be the hash of the bytes actually in the package");
		}
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
