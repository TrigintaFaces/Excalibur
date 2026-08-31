// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Text;

using Excalibur.Compliance.Pdf;
using Excalibur.Compliance.Soc2;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Compliance.Tests.Soc2;

/// <summary>
/// Locks the package boundary that keeps the QuestPDF licence obligation off consumers who never export
/// a PDF: PDF export is unavailable until the Excalibur.Compliance.Pdf package is installed and
/// registered, and every other export format works either way.
/// </summary>
/// <remarks>
/// Each assertion that PDF is refused is paired with one that a permitted format still succeeds, so an
/// exporter that had simply stopped working could not pass this class.
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
[Collection(QuestPdfLicenseCollection.Name)]
public sealed class Soc2PdfExportOptInShould
{
	[Fact]
	public void OmitPdfFromSupportedFormatsWithoutTheRenderer()
	{
		var sut = CreateExporter(pdfRenderer: null);

		var formats = sut.GetSupportedFormats();

		formats.ShouldNotContain(ExportFormat.Pdf);
		// Liveness: the other formats are still offered, so an exporter that simply stopped
		// reporting anything cannot pass.
		formats.ShouldContain(ExportFormat.Json);
		formats.ShouldContain(ExportFormat.Csv);
		formats.ShouldContain(ExportFormat.Xml);
		formats.ShouldContain(ExportFormat.Excel);
		formats.ShouldContain(ExportFormat.Text);
	}

	[Fact]
	public void OfferPdfAmongSupportedFormatsWithTheRenderer()
	{
		var sut = CreateExporter(new QuestPdfSoc2PdfRenderer(TimeProvider.System));

		sut.GetSupportedFormats().ShouldContain(ExportFormat.Pdf);
	}

	[Fact]
	public void RejectPdfValidationWithoutTheRendererAndNameThePackage()
	{
		var sut = CreateExporter(pdfRenderer: null);

		var pdf = sut.ValidateForExport(CreateReport(), ExportFormat.Pdf);

		pdf.IsValid.ShouldBeFalse();
		pdf.Issues.ShouldContain(i => i.Contains("Excalibur.Compliance.Pdf", StringComparison.Ordinal));
		// Liveness: a format the core package does support still validates.
		sut.ValidateForExport(CreateReport(), ExportFormat.Json).IsValid.ShouldBeTrue();
	}

	[Fact]
	public async Task RefusePdfExportWithoutTheRendererButStillExportOtherFormats()
	{
		var sut = CreateExporter(pdfRenderer: null);

		var thrown = await Should.ThrowAsync<InvalidOperationException>(
				() => sut.ExportAsync(CreateReport(), ExportFormat.Pdf, null, CancellationToken.None));

		thrown.Message.ShouldContain("Excalibur.Compliance.Pdf");

		// Liveness: JSON export is unaffected by the missing PDF package.
		var json = await sut.ExportAsync(CreateReport(), ExportFormat.Json, null, CancellationToken.None);
		json.Data.ShouldNotBeEmpty();
		json.ContentType.ShouldBe("application/json");
	}

	[Fact]
	public async Task PackageEvidenceWithoutTheRendererOnTheDefaultOptions()
	{
		// The default report formats must not require the PDF package, or the evidence package would be
		// unusable for a consumer who never opted in.
		var sut = CreateExporter(pdfRenderer: null);

		var result = await sut.ExportWithEvidenceAsync(
				CreateReport(), [], null, CancellationToken.None);

		result.Data.ShouldNotBeEmpty();
		result.ContentType.ShouldBe("application/zip");
	}

	[Fact]
	public async Task ExportPdfThroughRealDependencyInjectionOnceThePdfPackageIsRegistered()
	{
		await using var withPdf = new ServiceCollection()
				.AddLogging()
				.AddSoc2Compliance()
				.AddSoc2PdfExport()
				.BuildServiceProvider();

		using var scope = withPdf.CreateScope();
		var exporter = scope.ServiceProvider.GetRequiredService<ISoc2ReportExporter>();

		exporter.GetSupportedFormats().ShouldContain(ExportFormat.Pdf);

		var result = await exporter.ExportAsync(
				CreateReport(), ExportFormat.Pdf, null, CancellationToken.None);

		Encoding.ASCII.GetString(result.Data, 0, 4).ShouldBe("%PDF");
		result.ContentType.ShouldBe("application/pdf");
	}

	[Fact]
	public async Task LeavePdfUnsupportedThroughRealDependencyInjectionWithoutThePdfPackage()
	{
		await using var withoutPdf = new ServiceCollection()
				.AddLogging()
				.AddSingleton(TimeProvider.System)
				.AddSoc2Compliance()
				.BuildServiceProvider();

		using var scope = withoutPdf.CreateScope();
		var exporter = scope.ServiceProvider.GetRequiredService<ISoc2ReportExporter>();

		exporter.GetSupportedFormats().ShouldNotContain(ExportFormat.Pdf);
		// Liveness: the resolved exporter is a working exporter, not an inert one.
		var json = await exporter.ExportAsync(
				CreateReport(), ExportFormat.Json, null, CancellationToken.None);
		json.Data.ShouldNotBeEmpty();
	}

	private static Soc2ReportExporter CreateExporter(ISoc2PdfRenderer? pdfRenderer) =>
			new(NullLogger<Soc2ReportExporter>.Instance, TimeProvider.System, pdfRenderer);

	private static Soc2Report CreateReport() =>
		new()
		{
			ReportId = Guid.NewGuid(),
			Title = "Opt-In Boundary Report",
			ReportType = Soc2ReportType.TypeII,
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
			ControlSections = [],
			CategoriesIncluded = [TrustServicesCategory.Security]
		};
}
