// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Globalization;

using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Excalibur.Compliance.Pdf;

/// <summary>
/// Renders a SOC 2 report as a PDF document using QuestPDF.
/// </summary>
/// <remarks>
/// <para>
/// The hosting application must configure the QuestPDF license itself, for example
/// <c>QuestPDF.Settings.License = LicenseType.Community;</c> at startup. This library never assigns
/// <c>QuestPDF.Settings.License</c>: it is a process-global static, so writing it would overwrite a
/// Professional or Enterprise license the application configured, and would assert Community
/// eligibility (total annual gross revenue under USD 1,000,000, measured company-wide) on the
/// application's behalf. With no license configured, QuestPDF throws when a PDF is rendered.
/// </para>
/// <para>
/// Every setting on <see cref="Soc2ReportExportOptions.PdfOptions"/> is honored here. When no PDF
/// options are supplied the documented defaults apply: a cover page, a table of contents and page
/// numbers, in portrait orientation.
/// </para>
/// </remarks>
internal sealed class QuestPdfSoc2PdfRenderer : ISoc2PdfRenderer
{
	private readonly TimeProvider _timeProvider;

	/// <summary>
	/// Initializes a new instance of the <see cref="QuestPdfSoc2PdfRenderer"/> class.
	/// </summary>
	/// <param name="timeProvider">Time provider for the generated-at stamp in the report header.</param>
	public QuestPdfSoc2PdfRenderer(TimeProvider timeProvider) =>
		_timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));

	/// <inheritdoc />
	public byte[] Render(Soc2Report report, Soc2ReportExportOptions options)
	{
		ArgumentNullException.ThrowIfNull(report);
		ArgumentNullException.ThrowIfNull(options);

		var pdfOptions = options.PdfOptions ?? new PdfExportOptions();
		var pageSize = pdfOptions.Orientation == PageOrientation.Landscape
			? PageSizes.A4.Landscape()
			: PageSizes.A4;

		var document = Document.Create(container =>
		{
			if (pdfOptions.IncludeCoverPage)
			{
				container.Page(page =>
				{
					ConfigurePage(page, pageSize);
					page.Content().Element(c => RenderCoverPage(c, report, options, pdfOptions));
				});
			}

			if (pdfOptions.IncludeTableOfContents)
			{
				container.Page(page =>
				{
					ConfigurePage(page, pageSize);
					RenderPageHeader(page, report, options, pdfOptions);
					page.Content().PaddingVertical(10).Element(c => RenderTableOfContents(c, report));
					RenderPageFooter(page, pdfOptions);
				});
			}

			container.Page(page =>
			{
				ConfigurePage(page, pageSize);
				RenderPageHeader(page, report, options, pdfOptions);

				page.Content().PaddingVertical(10).Column(col =>
				{
					col.Item().Element(c => RenderSummarySection(c, report));

					_ = col.Item().PaddingVertical(5);

					col.Item().Element(c => RenderControlsSection(c, report, options));

					if (options.IncludeExceptions && report.Exceptions.Count > 0)
					{
						_ = col.Item().PaddingVertical(5);
						col.Item().Element(c => RenderExceptionsSection(c, report));
					}
				});

				RenderPageFooter(page, pdfOptions);
			});
		});

		using var stream = new MemoryStream();
		document.GeneratePdf(stream);
		return stream.ToArray();
	}

	/// <summary>
	/// Names the anchor a table-of-contents entry links to, so the entry can report the page the
	/// control section actually landed on.
	/// </summary>
	/// <param name="index">Zero-based position of the control section within the report.</param>
	private static string SectionAnchor(int index) =>
		string.Create(CultureInfo.InvariantCulture, $"control-section-{index}");

	private static void ConfigurePage(PageDescriptor page, PageSize pageSize)
	{
		page.Size(pageSize);
		page.Margin(2, Unit.Centimetre);
		page.DefaultTextStyle(x => x.FontSize(11));
	}

	private void RenderPageHeader(
		PageDescriptor page,
		Soc2Report report,
		Soc2ReportExportOptions options,
		PdfExportOptions pdfOptions) =>
		page.Header().Row(row =>
		{
			var hasLogo = pdfOptions.CompanyLogo is { Length: > 0 };

			if (hasLogo)
			{
				_ = row.ConstantItem(90).MaxHeight(45).Image(pdfOptions.CompanyLogo!).FitArea();
			}

			row.RelativeItem().PaddingLeft(hasLogo ? 10 : 0).Column(col =>
			{
				_ = col.Item().Text(options.CustomTitle ?? $"SOC 2 {report.ReportType} Report")
					.SemiBold().FontSize(20);
				_ = col.Item().Text(report.Title)
					.FontSize(14).FontColor(Colors.Grey.Darken2);

				if (!string.IsNullOrWhiteSpace(pdfOptions.HeaderText))
				{
					_ = col.Item().Text(pdfOptions.HeaderText)
						.FontSize(10).FontColor(Colors.Grey.Darken1);
				}

				_ = col.Item().Text($"Generated: {_timeProvider.GetUtcNow():yyyy-MM-dd HH:mm} UTC")
					.FontSize(10).FontColor(Colors.Grey.Medium);
			});
		});

	private static void RenderPageFooter(PageDescriptor page, PdfExportOptions pdfOptions)
	{
		var hasFooterText = !string.IsNullOrWhiteSpace(pdfOptions.FooterText);

		if (!hasFooterText && !pdfOptions.IncludePageNumbers)
		{
			return;
		}

		page.Footer().Row(row =>
		{
			if (hasFooterText)
			{
				_ = row.RelativeItem().Text(pdfOptions.FooterText)
					.FontSize(9).FontColor(Colors.Grey.Darken1);
			}

			if (pdfOptions.IncludePageNumbers)
			{
				var slot = hasFooterText
					? row.RelativeItem().AlignRight()
					: row.RelativeItem().AlignCenter();

				slot.Text(x =>
				{
					_ = x.Span("Page ");
					_ = x.CurrentPageNumber();
					_ = x.Span(" of ");
					_ = x.TotalPages();
				});
			}
		});
	}

	private void RenderCoverPage(
		IContainer container,
		Soc2Report report,
		Soc2ReportExportOptions options,
		PdfExportOptions pdfOptions) =>
		container.AlignCenter().AlignMiddle().Column(col =>
		{
			if (pdfOptions.CompanyLogo is { Length: > 0 })
			{
				_ = col.Item().MaxHeight(80).Image(pdfOptions.CompanyLogo!).FitArea();
				_ = col.Item().PaddingVertical(15);
			}

			_ = col.Item().Text(options.CustomTitle ?? $"SOC 2 {report.ReportType} Report")
				.SemiBold().FontSize(28);
			_ = col.Item().PaddingTop(8).Text(report.Title)
				.FontSize(16).FontColor(Colors.Grey.Darken2);

			_ = col.Item().PaddingTop(25).Text(
					$"Period: {report.PeriodStart:yyyy-MM-dd} to {report.PeriodEnd:yyyy-MM-dd}")
				.FontSize(12);
			_ = col.Item().Text($"Opinion: {report.Opinion}").FontSize(12);
			_ = col.Item().Text($"Report ID: {report.ReportId}")
				.FontSize(10).FontColor(Colors.Grey.Medium);

			if (!string.IsNullOrWhiteSpace(pdfOptions.HeaderText))
			{
				_ = col.Item().PaddingTop(20).Text(pdfOptions.HeaderText)
					.FontSize(11).FontColor(Colors.Grey.Darken1);
			}

			_ = col.Item().PaddingTop(20).Text(
					$"Generated: {_timeProvider.GetUtcNow():yyyy-MM-dd HH:mm} UTC")
				.FontSize(10).FontColor(Colors.Grey.Medium);
		});

	private static void RenderTableOfContents(IContainer container, Soc2Report report) =>
		container.Column(col =>
		{
			_ = col.Item().Text("Table of Contents").SemiBold().FontSize(16);
			col.Item().PaddingTop(5).LineHorizontal(1).LineColor(Colors.Grey.Lighten2);
			_ = col.Item().PaddingTop(10);

			for (var i = 0; i < report.ControlSections.Count; i++)
			{
				var section = report.ControlSections[i];
				var anchor = SectionAnchor(i);

				col.Item().PaddingBottom(3).Row(row =>
				{
					_ = row.RelativeItem().Text($"[{section.Criterion}] {section.Description}")
						.FontSize(10);
					row.ConstantItem(40).AlignRight().Text(text =>
					{
						text.DefaultTextStyle(x => x.FontSize(10));
						_ = text.BeginPageNumberOfSection(anchor);
					});
				});
			}
		});

	private static void RenderSummarySection(IContainer container, Soc2Report report)
	{
		container.Column(col =>
		{
			_ = col.Item().Text("Executive Summary").SemiBold().FontSize(16);
			col.Item().PaddingTop(5).LineHorizontal(1).LineColor(Colors.Grey.Lighten2);
			_ = col.Item().PaddingTop(10);

			col.Item().Row(row =>
			{
				row.RelativeItem().Column(left =>
				{
					_ = left.Item().Text($"Report Type: {report.ReportType}");
					_ = left.Item().Text($"Opinion: {report.Opinion}");
					_ = left.Item().Text($"Report ID: {report.ReportId}");
				});
				row.RelativeItem().Column(right =>
				{
					_ = right.Item().Text($"Period Start: {report.PeriodStart:yyyy-MM-dd}");
					_ = right.Item().Text($"Period End: {report.PeriodEnd:yyyy-MM-dd}");
					if (report.TenantId is not null)
					{
						_ = right.Item().Text($"Tenant: {report.TenantId}");
					}
				});
			});

			// Categories included
			if (report.CategoriesIncluded.Count > 0)
			{
				_ = col.Item().PaddingTop(10).Text("Trust Services Categories Included:")
					.SemiBold();
				_ = col.Item().Text(string.Join(", ", report.CategoriesIncluded.Select(c => c.ToString())));
			}
		});
	}

	private static void RenderControlsSection(IContainer container, Soc2Report report, Soc2ReportExportOptions options)
	{
		container.Column(col =>
		{
			_ = col.Item().Text("Control Sections").SemiBold().FontSize(16);
			col.Item().PaddingTop(5).LineHorizontal(1).LineColor(Colors.Grey.Lighten2);
			_ = col.Item().PaddingTop(10);

			for (var i = 0; i < report.ControlSections.Count; i++)
			{
				var section = report.ControlSections[i];

				// The anchor lets a table-of-contents entry report the page this section landed on.
				col.Item().Section(SectionAnchor(i)).PaddingBottom(10).Column(sectionCol =>
				{
					// Section header with status indicator
					sectionCol.Item().Row(headerRow =>
					{
						_ = headerRow.AutoItem().Width(10).Height(10)
							.Background(section.IsMet ? Colors.Green.Medium : Colors.Red.Medium);
						_ = headerRow.RelativeItem().PaddingLeft(5).Text($"[{section.Criterion}] {section.Description}")
							.SemiBold();
					});

					_ = sectionCol.Item().Text($"Status: {(section.IsMet ? "MET" : "NOT MET")}")
						.FontSize(10).FontColor(section.IsMet ? Colors.Green.Darken2 : Colors.Red.Darken2);

					// Controls table
					if (section.Controls.Count > 0)
					{
						sectionCol.Item().PaddingTop(5).Table(table =>
						{
							table.ColumnsDefinition(columns =>
							{
								columns.ConstantColumn(80);  // Control ID
								columns.RelativeColumn(2);   // Name
								columns.RelativeColumn(3);   // Description
								if (options.IncludeTestResults && section.TestResults?.Count > 0)
								{
									columns.ConstantColumn(70); // Outcome
								}
							});

							// Header
							table.Header(header =>
							{
								_ = header.Cell().Background(Colors.Grey.Lighten3).Padding(3).Text("Control ID").SemiBold().FontSize(9);
								_ = header.Cell().Background(Colors.Grey.Lighten3).Padding(3).Text("Name").SemiBold().FontSize(9);
								_ = header.Cell().Background(Colors.Grey.Lighten3).Padding(3).Text("Description").SemiBold().FontSize(9);
								if (options.IncludeTestResults && section.TestResults?.Count > 0)
								{
									_ = header.Cell().Background(Colors.Grey.Lighten3).Padding(3).Text("Outcome").SemiBold().FontSize(9);
								}
							});

							// Data rows
							foreach (var control in section.Controls)
							{
								var testResult = section.TestResults?.FirstOrDefault(t => t.ControlId == control.ControlId);

								_ = table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(3).Text(control.ControlId).FontSize(9);
								_ = table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(3).Text(control.Name).FontSize(9);
								_ = table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(3).Text(control.Description).FontSize(9);
								if (options.IncludeTestResults && section.TestResults?.Count > 0)
								{
									_ = table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(3)
										.Text(testResult?.Outcome.ToString() ?? "N/A").FontSize(9);
								}
							}
						});
					}
				});
			}
		});
	}

	private static void RenderExceptionsSection(IContainer container, Soc2Report report)
	{
		container.Column(col =>
		{
			_ = col.Item().Text("Exceptions").SemiBold().FontSize(16).FontColor(Colors.Red.Darken2);
			col.Item().PaddingTop(5).LineHorizontal(1).LineColor(Colors.Red.Lighten2);
			_ = col.Item().PaddingTop(10);

			col.Item().Table(table =>
			{
				table.ColumnsDefinition(columns =>
				{
					columns.ConstantColumn(100); // Exception ID
					columns.ConstantColumn(80);  // Criterion
					columns.ConstantColumn(80);  // Control ID
					columns.RelativeColumn();    // Description
				});

				table.Header(header =>
				{
					_ = header.Cell().Background(Colors.Red.Lighten4).Padding(3).Text("Exception ID").SemiBold().FontSize(9);
					_ = header.Cell().Background(Colors.Red.Lighten4).Padding(3).Text("Criterion").SemiBold().FontSize(9);
					_ = header.Cell().Background(Colors.Red.Lighten4).Padding(3).Text("Control ID").SemiBold().FontSize(9);
					_ = header.Cell().Background(Colors.Red.Lighten4).Padding(3).Text("Description").SemiBold().FontSize(9);
				});

				foreach (var exception in report.Exceptions)
				{
					_ = table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(3).Text(exception.ExceptionId).FontSize(9);
					_ = table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(3).Text(exception.Criterion.ToString()).FontSize(9);
					_ = table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(3).Text(exception.ControlId).FontSize(9);
					_ = table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(3).Text(exception.Description).FontSize(9);
				}
			});
		});
	}
}
