// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Text.RegularExpressions;

using Excalibur.Compliance.Pdf;
using Excalibur.Compliance.Soc2;

using Microsoft.Extensions.Logging.Abstractions;

using UglyToad.PdfPig;

namespace Excalibur.Compliance.Tests.Soc2;

/// <summary>
/// Locks every setting on <see cref="PdfExportOptions"/> against the rendered PDF, not against the
/// options object being handed to the renderer.
/// </summary>
/// <remarks>
/// A SOC 2 export is evidence a consumer hands to an external auditor. If a setting were silently
/// dropped they would ship an unbranded, un-paginated document believing otherwise, and no error would
/// tell them. So each test reads the produced bytes back with a PDF parser and asserts on what an
/// auditor would actually see -- extracted page text, page geometry, page count, embedded images. A
/// test that only observed the option being forwarded could not fail if the renderer ignored it.
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
[Collection(QuestPdfLicenseCollection.Name)]
public sealed partial class Soc2PdfExportOptionsShould
{
	/// <summary>An 8x8 PNG, small enough to inline and real enough for the renderer to embed.</summary>
	private static readonly byte[] LogoPng = Convert.FromBase64String(
		"iVBORw0KGgoAAAANSUhEUgAAAAgAAAAICAIAAABLbSncAAAAEklEQVR4nGP4z8CAFWEXHbQSACj/P8Fu7N9hAAAAAElFTkSuQmCC");

	[Fact]
	public async Task PutTheHeaderTextOnEveryContentPage()
	{
		using var document = await RenderAsync(
			new PdfExportOptions { HeaderText = "Confidential - Contoso Ltd", IncludeCoverPage = false });

		// Every page, not merely somewhere in the document -- a classification banner that appears on
		// page one and nowhere else is exactly the failure a reader would not notice.
		document.NumberOfPages.ShouldBeGreaterThan(1);
		foreach (var page in document.GetPages())
		{
			page.Text.ShouldContain("Confidential - Contoso Ltd");
		}
	}

	[Fact]
	public async Task OmitTheHeaderTextWhenNoneIsConfigured()
	{
		var text = await RenderTextAsync(new PdfExportOptions());

		text.ShouldNotContain("Confidential - Contoso Ltd");
	}

	[Fact]
	public async Task PutTheFooterTextInTheRenderedFooter()
	{
		var text = await RenderTextAsync(new PdfExportOptions { FooterText = "Distribution restricted" });

		text.ShouldContain("Distribution restricted");
	}

	[Fact]
	public async Task RenderPageNumbersOnlyWhenAskedFor()
	{
		var numbered = await RenderTextAsync(new PdfExportOptions { IncludePageNumbers = true });
		var plain = await RenderTextAsync(new PdfExportOptions { IncludePageNumbers = false });

		PageNumberFooter().IsMatch(numbered).ShouldBeTrue();
		PageNumberFooter().IsMatch(plain).ShouldBeFalse();
	}

	[Fact]
	public async Task RenderACoverPageOnlyWhenAskedFor()
	{
		using var withCover = await RenderAsync(new PdfExportOptions { IncludeCoverPage = true, IncludeTableOfContents = false });
		using var withoutCover = await RenderAsync(new PdfExportOptions { IncludeCoverPage = false, IncludeTableOfContents = false });

		// The cover is a page of its own, so dropping it must cost the document exactly one page.
		withCover.NumberOfPages.ShouldBe(withoutCover.NumberOfPages + 1);

		// And it must be the FIRST page, carrying the title -- not merely an extra page somewhere.
		withCover.GetPage(1).Text.ShouldContain("Compliance Evidence Report");
		withoutCover.GetPage(1).Text.ShouldContain("Executive Summary");
	}

	[Fact]
	public async Task RenderATableOfContentsListingEveryControlSection()
	{
		using var withToc = await RenderAsync(new PdfExportOptions { IncludeTableOfContents = true, IncludeCoverPage = false });
		using var withoutToc = await RenderAsync(new PdfExportOptions { IncludeTableOfContents = false, IncludeCoverPage = false });

		var tocPage = withToc.GetPage(1).Text;
		tocPage.ShouldContain("Table of Contents");

		// Every section must be listed. A table of contents missing an entry is worse than none.
		tocPage.ShouldContain("Logical and physical access controls");
		tocPage.ShouldContain("Change management");

		withoutToc.GetPage(1).Text.ShouldNotContain("Table of Contents");
	}

	[Fact]
	public async Task SwapPageGeometryForLandscapeOrientation()
	{
		using var portrait = await RenderAsync(new PdfExportOptions { Orientation = PageOrientation.Portrait });
		using var landscape = await RenderAsync(new PdfExportOptions { Orientation = PageOrientation.Landscape });

		var portraitPage = portrait.GetPage(1);
		var landscapePage = landscape.GetPage(1);

		portraitPage.Height.ShouldBeGreaterThan(portraitPage.Width);
		landscapePage.Width.ShouldBeGreaterThan(landscapePage.Height);
	}

	[Fact]
	public async Task EmbedTheCompanyLogoWhenOneIsSupplied()
	{
		using var branded = await RenderAsync(new PdfExportOptions { CompanyLogo = LogoPng });
		using var unbranded = await RenderAsync(new PdfExportOptions { CompanyLogo = null });

		CountImages(branded).ShouldBeGreaterThan(0);
		CountImages(unbranded).ShouldBe(0);
	}

	[Fact]
	public async Task ApplyTheDocumentedDefaultsWhenNoPdfOptionsAreSupplied()
	{
		// Defaults are cover page + table of contents + page numbers, portrait. A consumer who
		// configures nothing must still get the documented document, so the no-options path is
		// pinned as tightly as the configured ones.
		using var document = await RenderAsync(pdfOptions: null);

		var firstPage = document.GetPage(1);
		firstPage.Text.ShouldContain("Compliance Evidence Report");
		firstPage.Height.ShouldBeGreaterThan(firstPage.Width);
		document.GetPage(2).Text.ShouldContain("Table of Contents");

		// The cover is deliberately unnumbered, as a cover page conventionally is, so numbering
		// starts on the table of contents and runs to the last content page.
		document.GetPage(2).Text.ShouldContain("Page 2 of 3");
		document.GetPage(3).Text.ShouldContain("Page 3 of 3");
	}

	/// <summary>Matches the rendered footer pagination, whatever page the document happens to be on.</summary>
	[GeneratedRegex(@"Page \d+ of \d+")]
	private static partial Regex PageNumberFooter();

	private static int CountImages(PdfDocument document) =>
		document.GetPages().Sum(page => page.GetImages().Count());

	private static async Task<string> RenderTextAsync(PdfExportOptions pdfOptions)
	{
		using var document = await RenderAsync(pdfOptions);
		return string.Concat(document.GetPages().Select(page => page.Text));
	}

	private static async Task<PdfDocument> RenderAsync(PdfExportOptions? pdfOptions)
	{
		var sut = new Soc2ReportExporter(
			NullLogger<Soc2ReportExporter>.Instance,
			TimeProvider.System,
			new QuestPdfSoc2PdfRenderer(TimeProvider.System));

		var result = await sut.ExportAsync(
			CreateReport(),
			ExportFormat.Pdf,
			new Soc2ReportExportOptions { PdfOptions = pdfOptions },
			CancellationToken.None);

		return PdfDocument.Open(result.Data);
	}

	private static Soc2Report CreateReport() =>
		new()
		{
			ReportId = Guid.NewGuid(),
			Title = "Compliance Evidence Report",
			ReportType = Soc2ReportType.TypeII,
			PeriodStart = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
			PeriodEnd = new DateTimeOffset(2026, 12, 31, 0, 0, 0, TimeSpan.Zero),
			Opinion = AuditorOpinion.Unqualified,
			GeneratedAt = new DateTimeOffset(2026, 12, 31, 0, 0, 0, TimeSpan.Zero),
			System = new SystemDescription
			{
				Name = "Ledger",
				Description = "Transaction ledger",
				Services = ["Ledger API"],
				Infrastructure = ["Managed Kubernetes"],
				DataTypes = ["PII"]
			},
			CategoriesIncluded = [TrustServicesCategory.Security],
			ControlSections =
			[
				CreateSection(TrustServicesCriterion.CC6_LogicalAccess, "Logical and physical access controls"),
				CreateSection(TrustServicesCriterion.CC8_ChangeManagement, "Change management")
			]
		};

	private static ControlSection CreateSection(TrustServicesCriterion criterion, string description) =>
		new()
		{
			Criterion = criterion,
			Description = description,
			IsMet = true,
			Controls =
			[
				new ControlDescription
				{
					ControlId = criterion.ToString(),
					Name = description,
					Description = description,
					Implementation = "Enforced by the platform",
					Type = ControlType.Preventive,
					Frequency = ControlFrequency.Continuous
				}
			]
		};
}
