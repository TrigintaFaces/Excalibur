// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Globalization;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

using Microsoft.Extensions.Logging;

using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Excalibur.Dispatch.Compliance;

/// <summary>
/// Implementation of <see cref="ISoc2ReportExporter"/> for exporting SOC 2 reports.
/// </summary>
/// <remarks>
/// <para>
/// This service exports reports to PDF, Excel, CSV, JSON, and XML formats
/// using QuestPDF for PDF generation.
/// </para>
/// </remarks>
[System.Diagnostics.CodeAnalysis.SuppressMessage(
	"Maintainability",
	"CA1506:Avoid excessive class coupling",
	Justification = "Export service necessarily couples to multiple export formats (PDF, Excel, CSV, JSON, XML), " +
					"report models, QuestPDF document building APIs, and compression/hashing utilities. " +
					"This coupling is inherent to the export aggregation responsibility per ADR-055.")]
public sealed partial class Soc2ReportExporter : ISoc2ReportExporter
{
	private static readonly ExportFormat[] SupportedFormats =
[
	ExportFormat.Json,
		ExportFormat.Csv,
		ExportFormat.Xml,
		ExportFormat.Pdf,
				ExportFormat.Excel
	];

	private static readonly CompositeFormat ReportNotExportableFormat =
			CompositeFormat.Parse(Resources.Soc2ReportExporter_ReportNotExportable);

	private readonly ILogger<Soc2ReportExporter> _logger;

	[LoggerMessage(LogLevel.Information, "Exporting report {ReportId} to {Format}")]
	private partial void LogExportingReport(Guid reportId, ExportFormat format);

	[LoggerMessage(LogLevel.Information, "Exported report {ReportId} to {Format}: {Size} bytes")]
	private partial void LogExportedReport(Guid reportId, ExportFormat format, int size);

	[LoggerMessage(LogLevel.Information,
			"Creating evidence package for report {ReportId} with {EvidenceCount} items")]
	private partial void LogCreatingEvidencePackage(Guid reportId, int evidenceCount);

	[LoggerMessage(LogLevel.Information,
			"Created evidence package for report {ReportId}: {Size} bytes, {ItemCount} items")]
	private partial void LogCreatedEvidencePackage(Guid reportId, int size, int itemCount);

	[LoggerMessage(LogLevel.Warning,
			"Excel export is a basic Open XML implementation. For production, use ClosedXML or EPPlus for better formatting")]
	private partial void LogExcelPlaceholder();

	/// <summary>
	/// Initializes a new instance of the <see cref="Soc2ReportExporter"/> class.
	/// </summary>
	/// <param name="logger">Logger instance.</param>
	public Soc2ReportExporter(ILogger<Soc2ReportExporter> logger)
	{
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
	}

	/// <inheritdoc />
	public async Task<ExportResult> ExportAsync(
		Soc2Report report,
		ExportFormat format,
		Soc2ReportExportOptions? options,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(report);

		var validation = ValidateForExport(report, format);
		if (!validation.IsValid)
		{
			throw new InvalidOperationException(string.Format(
							CultureInfo.CurrentCulture,
							ReportNotExportableFormat,
							string.Join(", ", validation.Issues)));
		}

		LogExportingReport(report.ReportId, format);

		options ??= new Soc2ReportExportOptions();

		var (data, contentType, _) = format switch
		{
			ExportFormat.Json => await ExportToJsonAsync(report, options, cancellationToken).ConfigureAwait(false),
			ExportFormat.Csv => await ExportToCsvAsync(report, options, cancellationToken).ConfigureAwait(false),
			ExportFormat.Xml => await ExportToXmlAsync(report, options, cancellationToken).ConfigureAwait(false),
			ExportFormat.Pdf => await ExportToPdfAsync(report, options, cancellationToken).ConfigureAwait(false),
			ExportFormat.Excel => await ExportToExcelAsync(report, options, cancellationToken).ConfigureAwait(false),
			_ => throw new ArgumentOutOfRangeException(
					nameof(format),
					format,
					Resources.Soc2ReportExporter_UnsupportedFormat)
		};

		var finalData = options.Compress && format is ExportFormat.Json or ExportFormat.Csv or ExportFormat.Xml
			? await CompressAsync(data, cancellationToken).ConfigureAwait(false)
			: data;

		var fileName = GenerateFileName(report, format, options.Compress);
		var checksum = ComputeChecksum(finalData);

		LogExportedReport(report.ReportId, format, finalData.Length);

		return new ExportResult
		{
			Data = finalData,
			ContentType = options.Compress ? "application/gzip" : contentType,
			FileName = fileName,
			Format = format,
			GeneratedAt = DateTimeOffset.UtcNow,
			Checksum = checksum
		};
	}

	/// <inheritdoc />
#pragma warning disable CA1506 // Avoid excessive class coupling - necessary for comprehensive evidence packaging
	public async Task<ExportResult> ExportWithEvidenceAsync(
		Soc2Report report,
		IReadOnlyList<AuditEvidence> evidence,
		EvidencePackageOptions? options,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(report);
		ArgumentNullException.ThrowIfNull(evidence);

		options ??= new EvidencePackageOptions();

		LogCreatingEvidencePackage(report.ReportId, evidence.Count);

		using var memoryStream = new MemoryStream();
		using (var archive = new ZipArchive(memoryStream, ZipArchiveMode.Create, leaveOpen: true))
		{
			// Export report in requested formats
			foreach (var format in options.ReportFormats)
			{
				var exportResult = await ExportAsync(report, format, null, cancellationToken).ConfigureAwait(false);
				var entry = archive.CreateEntry($"report/{exportResult.FileName}", GetCompressionLevel(options.CompressionLevel));
#if NET10_0_OR_GREATER
				await using var entryStream = await entry.OpenAsync(cancellationToken).ConfigureAwait(false);
#else
				await using var entryStream = entry.Open();
#endif
				await entryStream.WriteAsync(exportResult.Data, cancellationToken).ConfigureAwait(false);
			}

			// Organize evidence by criterion
			var evidenceByCriterion = evidence
				.GroupBy(e => e.Criterion)
				.ToDictionary(g => g.Key, g => g.ToList());

			var manifest = new EvidenceManifest
			{
				ReportId = report.ReportId,
				ReportTitle = report.Title,
				GeneratedAt = DateTimeOffset.UtcNow,
				EvidenceItems = []
			};

			foreach (var (criterion, auditEvidenceList) in evidenceByCriterion)
			{
				var categoryFolder = $"evidence/{criterion.ToString().ToUpperInvariant()}";

				// AuditEvidence contains a list of EvidenceItems - iterate over each
				foreach (var auditEvidence in auditEvidenceList)
				{
					foreach (var item in auditEvidence.Items)
					{
						var itemFileName = SanitizeFileName($"{item.EvidenceId}_{item.Description}");
						var itemPath = $"{categoryFolder}/{itemFileName}";

						// EvidenceItem doesn't contain raw content - it has DataReference pointing to actual data
						// Add manifest entry as external reference
						manifest.EvidenceItems.Add(new ManifestItem
						{
							EvidenceId = item.EvidenceId,
							Path = item.DataReference ?? itemPath,
							Type = item.Type,
							Category = criterion.GetCategory(),
							Criterion = criterion,
							Size = 0, // Size would need to be fetched from the data reference
							IsEmbedded = false,
							Note = item.DataReference is not null
								? $"Reference to external evidence: {item.Source}"
								: "Evidence metadata only"
						});
					}
				}
			}

			// Add manifest
			if (options.IncludeManifest)
			{
				var manifestJson = JsonSerializer.SerializeToUtf8Bytes(
						manifest,
						Soc2ReportJsonContext.Default.EvidenceManifest);
				var manifestEntry = archive.CreateEntry("manifest.json", GetCompressionLevel(options.CompressionLevel));
#if NET10_0_OR_GREATER
				await using var manifestStream = await manifestEntry.OpenAsync(cancellationToken).ConfigureAwait(false);
#else
				await using var manifestStream = manifestEntry.Open();
#endif
				await manifestStream.WriteAsync(manifestJson, cancellationToken).ConfigureAwait(false);
			}

			// Add checksums file
			if (options.IncludeChecksums)
			{
				var checksums = new StringBuilder();
				checksums.AppendLine("# SHA-256 Checksums");
				checksums.AppendLine($"# Generated: {DateTimeOffset.UtcNow:O}");
				checksums.AppendLine();

				foreach (var item in manifest.EvidenceItems.Where(i => i.IsEmbedded && i.Checksum is not null))
				{
					checksums.AppendLine($"{item.Checksum}  {item.Path}");
				}

				var checksumsEntry = archive.CreateEntry("checksums.sha256", GetCompressionLevel(options.CompressionLevel));
#if NET10_0_OR_GREATER
				await using var checksumsStream = await checksumsEntry.OpenAsync(cancellationToken).ConfigureAwait(false);
#else
				await using var checksumsStream = checksumsEntry.Open();
#endif
				await checksumsStream.WriteAsync(Encoding.UTF8.GetBytes(checksums.ToString()), cancellationToken).ConfigureAwait(false);
			}
		}

		var packageData = memoryStream.ToArray();
		var packageFileName = $"soc2-evidence-{report.ReportId:N}-{DateTimeOffset.UtcNow:yyyyMMdd}.zip";

		LogCreatedEvidencePackage(report.ReportId, packageData.Length, evidence.Count);

		return new ExportResult
		{
			Data = packageData,
			ContentType = "application/zip",
			FileName = packageFileName,
			Format = ExportFormat.Json, // Primary format in package
			GeneratedAt = DateTimeOffset.UtcNow,
			Checksum = ComputeChecksum(packageData)
		};
	}

	/// <inheritdoc />
	public IReadOnlyList<ExportFormat> GetSupportedFormats() => SupportedFormats;

	/// <inheritdoc />
	public ExportValidationResult ValidateForExport(Soc2Report report, ExportFormat format)
	{
		var issues = new List<string>();
		var warnings = new List<string>();

		if (report.ReportId == Guid.Empty)
		{
			issues.Add("Report ID is required");
		}

		if (string.IsNullOrWhiteSpace(report.Title))
		{
			issues.Add("Report title is required");
		}

		if (report.ControlSections.Count == 0)
		{
			warnings.Add("Report has no control sections");
		}

		if (report.ReportType == Soc2ReportType.TypeII && report.PeriodStart >= report.PeriodEnd)
		{
			issues.Add("Type II report period end must be after period start");
		}

		// Format-specific validation
		if (format == ExportFormat.Pdf && report.System is null)
		{
			warnings.Add("System description is recommended for PDF export");
		}

		return new ExportValidationResult
		{
			IsValid = issues.Count == 0,
			Issues = issues,
			Warnings = warnings
		};
	}

#pragma warning disable IDE0060 // Remove unused parameter - kept for API consistency
	private Task<(byte[] Data, string ContentType, string Extension)> ExportToJsonAsync(
		Soc2Report report,
		Soc2ReportExportOptions options,
		CancellationToken cancellationToken)
	{
		var exportData = CreateExportModel(report, options);
		var json = JsonSerializer.SerializeToUtf8Bytes(
				exportData,
				Soc2ReportJsonContext.Default.Soc2ReportExportModel);
		return Task.FromResult((json, "application/json", ".json"));
	}

	private Task<(byte[] Data, string ContentType, string Extension)> ExportToCsvAsync(
		Soc2Report report,
		Soc2ReportExportOptions options,
		CancellationToken cancellationToken)
	{
		var csv = new StringBuilder();

		// Header
		_ = csv.AppendLine("Section,Criterion,Description,Status,ControlId,ControlName,TestOutcome,ExceptionsFound");

		// Data rows
		foreach (var section in report.ControlSections)
		{
			foreach (var control in section.Controls)
			{
				var testResult = section.TestResults?.FirstOrDefault(t => t.ControlId == control.ControlId);

				_ = csv.AppendLine(string.Join(",",
					EscapeCsv(section.Criterion.GetDisplayName()),
					EscapeCsv(section.Criterion.ToString()),
					EscapeCsv(section.Description),
					section.IsMet ? "Met" : "Not Met",
					EscapeCsv(control.ControlId),
					EscapeCsv(control.Name),
					testResult?.Outcome.ToString() ?? "N/A",
					testResult?.ExceptionsFound.ToString() ?? "0"));
			}
		}

		var data = Encoding.UTF8.GetBytes(csv.ToString());
		return Task.FromResult((data, "text/csv", ".csv"));
	}

	private Task<(byte[] Data, string ContentType, string Extension)> ExportToXmlAsync(
		Soc2Report report,
		Soc2ReportExportOptions options,
		CancellationToken cancellationToken)
	{
		var xml = new StringBuilder();
		_ = xml.AppendLine("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
		_ = xml.AppendLine($"<soc2Report xmlns=\"urn:dispatch:compliance:soc2\" generated=\"{DateTimeOffset.UtcNow:O}\">");
		_ = xml.AppendLine($"  <reportId>{report.ReportId}</reportId>");
		_ = xml.AppendLine($"  <reportType>{report.ReportType}</reportType>");
		_ = xml.AppendLine($"  <title>{EscapeXml(report.Title)}</title>");
		_ = xml.AppendLine($"  <periodStart>{report.PeriodStart:O}</periodStart>");
		_ = xml.AppendLine($"  <periodEnd>{report.PeriodEnd:O}</periodEnd>");
		_ = xml.AppendLine($"  <opinion>{report.Opinion}</opinion>");
		_ = xml.AppendLine($"  <generatedAt>{report.GeneratedAt:O}</generatedAt>");

		if (report.TenantId is not null)
		{
			_ = xml.AppendLine($"  <tenantId>{EscapeXml(report.TenantId)}</tenantId>");
		}

		_ = xml.AppendLine("  <controlSections>");
		foreach (var section in report.ControlSections)
		{
			var metValue = section.IsMet ? "true" : "false";
			_ = xml.AppendLine($"    <section criterion=\"{section.Criterion}\" met=\"{metValue}\">");
			_ = xml.AppendLine($"      <description>{EscapeXml(section.Description)}</description>");
			_ = xml.AppendLine("      <controls>");
			foreach (var control in section.Controls)
			{
				_ = xml.AppendLine($"        <control id=\"{EscapeXml(control.ControlId)}\">");
				_ = xml.AppendLine($"          <name>{EscapeXml(control.Name)}</name>");
				_ = xml.AppendLine($"          <description>{EscapeXml(control.Description)}</description>");
				_ = xml.AppendLine($"          <type>{control.Type}</type>");
				_ = xml.AppendLine("        </control>");
			}
			_ = xml.AppendLine("      </controls>");
			_ = xml.AppendLine("    </section>");
		}
		_ = xml.AppendLine("  </controlSections>");

		if (options.IncludeExceptions && report.Exceptions.Count > 0)
		{
			_ = xml.AppendLine("  <exceptions>");
			foreach (var exception in report.Exceptions)
			{
				_ = xml.AppendLine($"    <exception id=\"{EscapeXml(exception.ExceptionId)}\">");
				_ = xml.AppendLine($"      <criterion>{exception.Criterion}</criterion>");
				_ = xml.AppendLine($"      <controlId>{EscapeXml(exception.ControlId)}</controlId>");
				_ = xml.AppendLine($"      <description>{EscapeXml(exception.Description)}</description>");
				_ = xml.AppendLine("    </exception>");
			}
			_ = xml.AppendLine("  </exceptions>");
		}

		_ = xml.AppendLine("</soc2Report>");

		var data = Encoding.UTF8.GetBytes(xml.ToString());
		return Task.FromResult((data, "application/xml", ".xml"));
	}

	private Task<(byte[] Data, string ContentType, string Extension)> ExportToPdfAsync(
		Soc2Report report,
		Soc2ReportExportOptions options,
		CancellationToken cancellationToken)
	{
		var pdfOptions = options.PdfOptions ?? new PdfExportOptions();

		// Configure QuestPDF license (Community Edition is MIT, free for < $1M revenue)
		QuestPDF.Settings.License = LicenseType.Community;

		var document = Document.Create(container =>
		{
			_ = container.Page(page =>
			{
				page.Size(PageSizes.A4);
				page.Margin(2, Unit.Centimetre);
				page.DefaultTextStyle(x => x.FontSize(11));

				// Header
				page.Header().Element(header => RenderPdfHeader(header, report, options));

				// Content
				page.Content().PaddingVertical(10).Column(col =>
				{
					// Executive Summary Section
					col.Item().Element(c => RenderPdfSummarySection(c, report));

					_ = col.Item().PaddingVertical(5);

					// Controls Status Section
					col.Item().Element(c => RenderPdfControlsSection(c, report, options));

					// Exceptions Section (if included)
					if (options.IncludeExceptions && report.Exceptions.Count > 0)
					{
						_ = col.Item().PaddingVertical(5);
						col.Item().Element(c => RenderPdfExceptionsSection(c, report));
					}
				});

				// Footer with page numbers
				page.Footer().AlignCenter().Text(x =>
				{
					_ = x.Span("Page ");
					_ = x.CurrentPageNumber();
					_ = x.Span(" of ");
					_ = x.TotalPages();
				});
			});
		});

		using var stream = new MemoryStream();
		document.GeneratePdf(stream);

		return Task.FromResult((stream.ToArray(), "application/pdf", ".pdf"));
	}

	private static void RenderPdfHeader(IContainer container, Soc2Report report, Soc2ReportExportOptions options)
	{
		container.Row(row =>
		{
			row.RelativeItem().Column(col =>
			{
				_ = col.Item().Text(options.CustomTitle ?? $"SOC 2 {report.ReportType} Report")
					.SemiBold().FontSize(20);
				_ = col.Item().Text(report.Title)
					.FontSize(14).FontColor(Colors.Grey.Darken2);
				_ = col.Item().Text($"Generated: {DateTimeOffset.UtcNow:yyyy-MM-dd HH:mm} UTC")
					.FontSize(10).FontColor(Colors.Grey.Medium);
			});
		});
	}

	private static void RenderPdfSummarySection(IContainer container, Soc2Report report)
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

	private static void RenderPdfControlsSection(IContainer container, Soc2Report report, Soc2ReportExportOptions options)
	{
		container.Column(col =>
		{
			_ = col.Item().Text("Control Sections").SemiBold().FontSize(16);
			col.Item().PaddingTop(5).LineHorizontal(1).LineColor(Colors.Grey.Lighten2);
			_ = col.Item().PaddingTop(10);

			foreach (var section in report.ControlSections)
			{
				col.Item().PaddingBottom(10).Column(sectionCol =>
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

	private static void RenderPdfExceptionsSection(IContainer container, Soc2Report report)
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

#pragma warning disable CA1849 // ZipArchiveEntry.OpenAsync is net10.0+ only; this method is intentionally synchronous
	private Task<(byte[] Data, string ContentType, string Extension)> ExportToExcelAsync(
		Soc2Report report,
		Soc2ReportExportOptions options,
		CancellationToken cancellationToken)
	{
		// Simple CSV-based Excel placeholder using Open XML Spreadsheet format
		// In production, integrate with a library like ClosedXML, EPPlus, or NPOI
		_ = options.ExcelOptions ?? new ExcelExportOptions(); // Reserved for future use
		_ = cancellationToken; // Reserved for async Excel library integration

		// Create a minimal XLSX file (which is a ZIP with XML files)
		using var memoryStream = new MemoryStream();
		using (var archive = new ZipArchive(memoryStream, ZipArchiveMode.Create, leaveOpen: true))
		{
			// [Content_Types].xml
			var contentTypesEntry = archive.CreateEntry("[Content_Types].xml");
			using (var contentTypesStream = contentTypesEntry.Open())
			{
				var contentTypes = @"<?xml version=""1.0"" encoding=""UTF-8"" standalone=""yes""?>
<Types xmlns=""http://schemas.openxmlformats.org/package/2006/content-types"">
<Default Extension=""rels"" ContentType=""application/vnd.openxmlformats-package.relationships+xml""/>
<Default Extension=""xml"" ContentType=""application/xml""/>
<Override PartName=""/xl/workbook.xml"" ContentType=""application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml""/>
<Override PartName=""/xl/worksheets/sheet1.xml"" ContentType=""application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml""/>
</Types>";
				contentTypesStream.Write(Encoding.UTF8.GetBytes(contentTypes));
			}

			// _rels/.rels
			var relsEntry = archive.CreateEntry("_rels/.rels");
			using (var relsStream = relsEntry.Open())
			{
				var rels = @"<?xml version=""1.0"" encoding=""UTF-8"" standalone=""yes""?>
<Relationships xmlns=""http://schemas.openxmlformats.org/package/2006/relationships"">
<Relationship Id=""rId1"" Type=""http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument"" Target=""xl/workbook.xml""/>
</Relationships>";
				relsStream.Write(Encoding.UTF8.GetBytes(rels));
			}

			// xl/_rels/workbook.xml.rels
			var wbRelsEntry = archive.CreateEntry("xl/_rels/workbook.xml.rels");
			using (var wbRelsStream = wbRelsEntry.Open())
			{
				var wbRels = @"<?xml version=""1.0"" encoding=""UTF-8"" standalone=""yes""?>
<Relationships xmlns=""http://schemas.openxmlformats.org/package/2006/relationships"">
<Relationship Id=""rId1"" Type=""http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet"" Target=""worksheets/sheet1.xml""/>
</Relationships>";
				wbRelsStream.Write(Encoding.UTF8.GetBytes(wbRels));
			}

			// xl/workbook.xml
			var workbookEntry = archive.CreateEntry("xl/workbook.xml");
			using (var workbookStream = workbookEntry.Open())
			{
				var workbook = @"<?xml version=""1.0"" encoding=""UTF-8"" standalone=""yes""?>
<workbook xmlns=""http://schemas.openxmlformats.org/spreadsheetml/2006/main"" xmlns:r=""http://schemas.openxmlformats.org/officeDocument/2006/relationships"">
<sheets>
<sheet name=""SOC 2 Report"" sheetId=""1"" r:id=""rId1""/>
</sheets>
</workbook>";
				workbookStream.Write(Encoding.UTF8.GetBytes(workbook));
			}

			// xl/worksheets/sheet1.xml
			var sheetEntry = archive.CreateEntry("xl/worksheets/sheet1.xml");
			using (var sheetStream = sheetEntry.Open())
			{
				var sheet = GenerateExcelSheetXml(report, options);
				sheetStream.Write(Encoding.UTF8.GetBytes(sheet));
			}
		}

		LogExcelPlaceholder();

		var data = memoryStream.ToArray();
		return Task.FromResult((data, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", ".xlsx"));
	}
#pragma warning restore CA1849

	private static string GenerateExcelSheetXml(Soc2Report report, Soc2ReportExportOptions options)
	{
		var xml = new StringBuilder();
		_ = xml.AppendLine(@"<?xml version=""1.0"" encoding=""UTF-8"" standalone=""yes""?>");
		_ = xml.AppendLine(@"<worksheet xmlns=""http://schemas.openxmlformats.org/spreadsheetml/2006/main"">");
		_ = xml.AppendLine("<sheetData>");

		// Header row
		var row = 1;
		_ = xml.AppendLine($"<row r=\"{row}\">");
		_ = xml.AppendLine($"<c r=\"A{row}\" t=\"inlineStr\"><is><t>Criterion</t></is></c>");
		_ = xml.AppendLine($"<c r=\"B{row}\" t=\"inlineStr\"><is><t>Description</t></is></c>");
		_ = xml.AppendLine($"<c r=\"C{row}\" t=\"inlineStr\"><is><t>Status</t></is></c>");
		_ = xml.AppendLine($"<c r=\"D{row}\" t=\"inlineStr\"><is><t>Control ID</t></is></c>");
		_ = xml.AppendLine($"<c r=\"E{row}\" t=\"inlineStr\"><is><t>Control Name</t></is></c>");
		_ = xml.AppendLine($"<c r=\"F{row}\" t=\"inlineStr\"><is><t>Test Outcome</t></is></c>");
		_ = xml.AppendLine("</row>");

		// Data rows
		foreach (var section in report.ControlSections)
		{
			foreach (var control in section.Controls)
			{
				row++;
				var testResult = section.TestResults?.FirstOrDefault(t => t.ControlId == control.ControlId);

				_ = xml.AppendLine($"<row r=\"{row}\">");
				_ = xml.AppendLine($"<c r=\"A{row}\" t=\"inlineStr\"><is><t>{EscapeXml(section.Criterion.ToString())}</t></is></c>");
				_ = xml.AppendLine($"<c r=\"B{row}\" t=\"inlineStr\"><is><t>{EscapeXml(section.Description)}</t></is></c>");
				_ = xml.AppendLine($"<c r=\"C{row}\" t=\"inlineStr\"><is><t>{(section.IsMet ? "Met" : "Not Met")}</t></is></c>");
				_ = xml.AppendLine($"<c r=\"D{row}\" t=\"inlineStr\"><is><t>{EscapeXml(control.ControlId)}</t></is></c>");
				_ = xml.AppendLine($"<c r=\"E{row}\" t=\"inlineStr\"><is><t>{EscapeXml(control.Name)}</t></is></c>");
				_ = xml.AppendLine($"<c r=\"F{row}\" t=\"inlineStr\"><is><t>{testResult?.Outcome.ToString() ?? "N/A"}</t></is></c>");
				_ = xml.AppendLine("</row>");
			}
		}

		_ = xml.AppendLine("</sheetData>");
		_ = xml.AppendLine("</worksheet>");

		return xml.ToString();
	}

	private static string GenerateTextReport(Soc2Report report, Soc2ReportExportOptions options)
	{
		var text = new StringBuilder();

		_ = text.AppendLine(new string('=', 80));
		_ = text.AppendLine(options.CustomTitle ?? report.Title);
		_ = text.AppendLine(new string('=', 80));
		_ = text.AppendLine();

		_ = text.AppendLine($"Report ID: {report.ReportId}");
		_ = text.AppendLine($"Report Type: {report.ReportType}");
		_ = text.AppendLine($"Period: {report.PeriodStart:yyyy-MM-dd} to {report.PeriodEnd:yyyy-MM-dd}");
		_ = text.AppendLine($"Opinion: {report.Opinion}");
		_ = text.AppendLine($"Generated: {report.GeneratedAt:yyyy-MM-dd HH:mm:ss}");

		if (report.TenantId is not null)
		{
			_ = text.AppendLine($"Tenant: {report.TenantId}");
		}

		_ = text.AppendLine();
		_ = text.AppendLine(new string('-', 80));
		_ = text.AppendLine("CONTROL SECTIONS");
		_ = text.AppendLine(new string('-', 80));

		foreach (var section in report.ControlSections)
		{
			_ = text.AppendLine();
			_ = text.AppendLine($"[{section.Criterion}] {section.Description}");
			_ = text.AppendLine($"Status: {(section.IsMet ? "MET" : "NOT MET")}");
			_ = text.AppendLine();

			foreach (var control in section.Controls)
			{
				_ = text.AppendLine($"  - {control.ControlId}: {control.Name}");
				_ = text.AppendLine($"    {control.Description}");
			}
		}

		if (options.IncludeExceptions && report.Exceptions.Count > 0)
		{
			_ = text.AppendLine();
			_ = text.AppendLine(new string('-', 80));
			_ = text.AppendLine("EXCEPTIONS");
			_ = text.AppendLine(new string('-', 80));

			foreach (var exception in report.Exceptions)
			{
				_ = text.AppendLine();
				_ = text.AppendLine($"[{exception.ExceptionId}] {exception.Criterion}");
				_ = text.AppendLine($"Control: {exception.ControlId}");
				_ = text.AppendLine($"Description: {exception.Description}");
			}
		}

		return text.ToString();
	}

	private static Soc2ReportExportModel CreateExportModel(
			Soc2Report report,
			Soc2ReportExportOptions options)
	{
		return new Soc2ReportExportModel
		{
			ReportId = report.ReportId,
			ReportType = report.ReportType,
			Title = report.Title,
			PeriodStart = report.PeriodStart,
			PeriodEnd = report.PeriodEnd,
			Opinion = report.Opinion,
			GeneratedAt = report.GeneratedAt,
			TenantId = report.TenantId,
			CategoriesIncluded = report.CategoriesIncluded,
			System = options.IncludeEvidence ? report.System : null,
			ControlSections = report.ControlSections.Select(section => new Soc2ReportExportSection
			{
				Criterion = section.Criterion,
				Description = section.Description,
				IsMet = section.IsMet,
				Controls = section.Controls,
				TestResults = options.IncludeTestResults ? section.TestResults : null
			}).ToList(),
			Exceptions = options.IncludeExceptions ? report.Exceptions : null,
			ExportedAt = DateTimeOffset.UtcNow
		};
	}

	private static async Task<byte[]> CompressAsync(byte[] data, CancellationToken cancellationToken)
	{
		using var output = new MemoryStream();
		await using (var gzip = new GZipStream(output, System.IO.Compression.CompressionLevel.Optimal))
		{
			await gzip.WriteAsync(data, cancellationToken).ConfigureAwait(false);
		}
		return output.ToArray();
	}

#pragma warning disable CA1308 // Normalize strings to uppercase - checksums conventionally use lowercase
	private static string ComputeChecksum(byte[] data)
	{
		var hash = SHA256.HashData(data);
		return Convert.ToHexString(hash).ToLowerInvariant();
	}
#pragma warning restore CA1308

	private static string GenerateFileName(Soc2Report report, ExportFormat format, bool compressed)
	{
		var extension = format switch
		{
			ExportFormat.Json => ".json",
			ExportFormat.Csv => ".csv",
			ExportFormat.Xml => ".xml",
			ExportFormat.Pdf => ".pdf",
			ExportFormat.Excel => ".xlsx",
			_ => ".dat"
		};

		if (compressed)
		{
			extension += ".gz";
		}

		var typeStr = report.ReportType == Soc2ReportType.TypeI ? "type1" : "type2";
		return $"soc2-{typeStr}-{report.ReportId:N}{extension}";
	}

#pragma warning disable CA1307 // Specify StringComparison for clarity - simple char/string operations
	private static string EscapeCsv(string value)
	{
		if (string.IsNullOrEmpty(value))
		{
			return string.Empty;
		}

		if (value.Contains(',') || value.Contains('"') || value.Contains('\n'))
		{
			return $"\"{value.Replace("\"", "\"\"")}\"";
		}

		return value;
	}

	private static string EscapeXml(string value)
	{
		if (string.IsNullOrEmpty(value))
		{
			return string.Empty;
		}

		return value
			.Replace("&", "&amp;")
			.Replace("<", "&lt;")
			.Replace(">", "&gt;")
			.Replace("\"", "&quot;")
			.Replace("'", "&apos;");
	}
#pragma warning restore CA1307

	private static string SanitizeFileName(string name)
	{
		var invalid = Path.GetInvalidFileNameChars();
		var sanitized = new StringBuilder();

		foreach (var c in name)
		{
			_ = sanitized.Append(invalid.Contains(c) ? '_' : c);
		}

		return sanitized.ToString();
	}

	private static System.IO.Compression.CompressionLevel GetCompressionLevel(CompressionLevel level) =>
		level switch
		{
			CompressionLevel.None => System.IO.Compression.CompressionLevel.NoCompression,
			CompressionLevel.Fastest => System.IO.Compression.CompressionLevel.Fastest,
			CompressionLevel.Optimal => System.IO.Compression.CompressionLevel.Optimal,
			CompressionLevel.SmallestSize => System.IO.Compression.CompressionLevel.SmallestSize,
			_ => System.IO.Compression.CompressionLevel.Optimal
		};

	/// <summary>
	/// Internal manifest structure for evidence packages.
	/// </summary>
	internal sealed class EvidenceManifest
	{
		public Guid ReportId { get; init; }
		public string Title => "SOC 2 Evidence Package";
		public string? ReportTitle { get; init; }
		public DateTimeOffset GeneratedAt { get; init; }
		public required List<ManifestItem> EvidenceItems { get; init; }
	}

	/// <summary>
	/// Manifest item for evidence packages.
	/// </summary>
	internal sealed class ManifestItem
	{
		public required string EvidenceId { get; init; }
		public required string Path { get; init; }
		public EvidenceType Type { get; init; }
		public TrustServicesCategory Category { get; init; }
		public TrustServicesCriterion? Criterion { get; init; }
		public string? Checksum { get; init; }
		public long Size { get; init; }
		public bool IsEmbedded { get; init; }
		public string? Note { get; init; }
	}

	internal sealed class Soc2ReportExportModel
	{
		public Guid ReportId { get; init; }
		public Soc2ReportType ReportType { get; init; }
		public string Title { get; init; } = string.Empty;
		public DateTimeOffset PeriodStart { get; init; }
		public DateTimeOffset PeriodEnd { get; init; }
		public AuditorOpinion Opinion { get; init; }
		public DateTimeOffset GeneratedAt { get; init; }
		public string? TenantId { get; init; }
		public IReadOnlyList<TrustServicesCategory> CategoriesIncluded { get; init; } =
				Array.Empty<TrustServicesCategory>();
		public SystemDescription? System { get; init; }
		public IReadOnlyList<Soc2ReportExportSection> ControlSections { get; init; } =
				Array.Empty<Soc2ReportExportSection>();
		public IReadOnlyList<ReportException>? Exceptions { get; init; }
		public DateTimeOffset ExportedAt { get; init; }
	}

	internal sealed class Soc2ReportExportSection
	{
		public TrustServicesCriterion Criterion { get; init; }
		public string Description { get; init; } = string.Empty;
		public bool IsMet { get; init; }
		public IReadOnlyList<ControlDescription> Controls { get; init; } =
				Array.Empty<ControlDescription>();
		public IReadOnlyList<TestResult>? TestResults { get; init; }
	}
}
