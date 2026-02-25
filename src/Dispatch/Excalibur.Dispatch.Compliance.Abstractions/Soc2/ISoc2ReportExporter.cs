// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Compliance;

/// <summary>
/// Page orientation for PDF exports.
/// </summary>
public enum PageOrientation
{
	/// <summary>Portrait orientation.</summary>
	Portrait,

	/// <summary>Landscape orientation.</summary>
	Landscape
}

/// <summary>
/// Compression level options.
/// </summary>
public enum CompressionLevel
{
	/// <summary>No compression.</summary>
	None,

	/// <summary>Fast compression with larger output.</summary>
	Fastest,

	/// <summary>Balanced compression.</summary>
	Optimal,

	/// <summary>Maximum compression with smaller output.</summary>
	SmallestSize
}

/// <summary>
/// Exports SOC 2 reports to various formats for external auditors.
/// </summary>
/// <remarks>
/// <para>
/// Reports can be exported to PDF, Excel, CSV, JSON, or XML format.
/// Each format is optimized for different use cases:
/// </para>
/// <list type="bullet">
///   <item><description>PDF: Human-readable formatted report for distribution</description></item>
///   <item><description>Excel: Tabular data for analysis and manipulation</description></item>
///   <item><description>CSV: Raw data for import into other systems</description></item>
///   <item><description>JSON: Machine-readable structured data</description></item>
///   <item><description>XML: Machine-readable with schema validation support</description></item>
/// </list>
/// </remarks>
public interface ISoc2ReportExporter
{
	/// <summary>
	/// Exports a report to the specified format.
	/// </summary>
	/// <param name="report">The report to export.</param>
	/// <param name="format">The target export format.</param>
	/// <param name="options">Optional export options.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>The exported report data.</returns>
	Task<ExportResult> ExportAsync(
		Soc2Report report,
		ExportFormat format,
		Soc2ReportExportOptions? options,
		CancellationToken cancellationToken);

	/// <summary>
	/// Exports a report with evidence package for auditors.
	/// </summary>
	/// <param name="report">The report to export.</param>
	/// <param name="evidence">Evidence collection to include.</param>
	/// <param name="options">Optional export options.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>A zip archive containing the report and evidence.</returns>
	/// <remarks>
	/// Creates a comprehensive audit package containing:
	/// - The report in PDF and JSON formats
	/// - All evidence items organized by category/criterion
	/// - A manifest file describing the package contents
	/// - Checksums for integrity verification
	/// </remarks>
	Task<ExportResult> ExportWithEvidenceAsync(
		Soc2Report report,
		IReadOnlyList<AuditEvidence> evidence,
		EvidencePackageOptions? options,
		CancellationToken cancellationToken);

	/// <summary>
	/// Gets the supported export formats.
	/// </summary>
	/// <returns>List of supported export formats.</returns>
	IReadOnlyList<ExportFormat> GetSupportedFormats();

	/// <summary>
	/// Validates that a report can be exported to the specified format.
	/// </summary>
	/// <param name="report">The report to validate.</param>
	/// <param name="format">The target format.</param>
	/// <returns>Validation result with any issues found.</returns>
	ExportValidationResult ValidateForExport(Soc2Report report, ExportFormat format);
}

/// <summary>
/// Result of an export operation.
/// </summary>
public record ExportResult
{
	/// <summary>
	/// The exported data.
	/// </summary>
	public required byte[] Data { get; init; }

	/// <summary>
	/// The MIME type of the exported data.
	/// </summary>
	public required string ContentType { get; init; }

	/// <summary>
	/// Suggested filename for the export.
	/// </summary>
	public required string FileName { get; init; }

	/// <summary>
	/// The export format used.
	/// </summary>
	public required ExportFormat Format { get; init; }

	/// <summary>
	/// When the export was generated.
	/// </summary>
	public required DateTimeOffset GeneratedAt { get; init; }

	/// <summary>
	/// SHA-256 hash of the data for integrity verification.
	/// </summary>
	public string? Checksum { get; init; }
}

/// <summary>
/// Options for export operations.
/// </summary>
public record Soc2ReportExportOptions
{
	/// <summary>
	/// Whether to include detailed evidence in the export.
	/// </summary>
	public bool IncludeEvidence { get; init; } = true;

	/// <summary>
	/// Whether to include exception details.
	/// </summary>
	public bool IncludeExceptions { get; init; } = true;

	/// <summary>
	/// Whether to include test results (Type II reports).
	/// </summary>
	public bool IncludeTestResults { get; init; } = true;

	/// <summary>
	/// Custom title for the export.
	/// </summary>
	public string? CustomTitle { get; init; }

	/// <summary>
	/// Whether to compress the output (applicable to CSV/JSON).
	/// </summary>
	public bool Compress { get; init; }

	/// <summary>
	/// PDF-specific options.
	/// </summary>
	public PdfExportOptions? PdfOptions { get; init; }

	/// <summary>
	/// Excel-specific options.
	/// </summary>
	public ExcelExportOptions? ExcelOptions { get; init; }
}

/// <summary>
/// PDF-specific export options.
/// </summary>
public record PdfExportOptions
{
	/// <summary>
	/// Whether to include a table of contents.
	/// </summary>
	public bool IncludeTableOfContents { get; init; } = true;

	/// <summary>
	/// Whether to include page numbers.
	/// </summary>
	public bool IncludePageNumbers { get; init; } = true;

	/// <summary>
	/// Whether to include a cover page.
	/// </summary>
	public bool IncludeCoverPage { get; init; } = true;

	/// <summary>
	/// Optional company logo to include.
	/// </summary>
	public byte[]? CompanyLogo { get; init; }

	/// <summary>
	/// Custom header text.
	/// </summary>
	public string? HeaderText { get; init; }

	/// <summary>
	/// Custom footer text.
	/// </summary>
	public string? FooterText { get; init; }

	/// <summary>
	/// Page orientation.
	/// </summary>
	public PageOrientation Orientation { get; init; } = PageOrientation.Portrait;
}

/// <summary>
/// Excel-specific export options.
/// </summary>
public record ExcelExportOptions
{
	/// <summary>
	/// Whether to include separate worksheets for each section.
	/// </summary>
	public bool SeparateWorksheets { get; init; } = true;

	/// <summary>
	/// Whether to include a summary worksheet.
	/// </summary>
	public bool IncludeSummarySheet { get; init; } = true;

	/// <summary>
	/// Whether to auto-fit column widths.
	/// </summary>
	public bool AutoFitColumns { get; init; } = true;

	/// <summary>
	/// Whether to freeze header rows.
	/// </summary>
	public bool FreezeHeaderRows { get; init; } = true;

	/// <summary>
	/// Whether to include charts/visualizations.
	/// </summary>
	public bool IncludeCharts { get; init; }
}

/// <summary>
/// Options for evidence package exports.
/// </summary>
public record EvidencePackageOptions
{
	/// <summary>
	/// Report formats to include in the package.
	/// </summary>
	public ExportFormat[] ReportFormats { get; init; } = [ExportFormat.Pdf, ExportFormat.Json];

	/// <summary>
	/// Whether to include a manifest file.
	/// </summary>
	public bool IncludeManifest { get; init; } = true;

	/// <summary>
	/// Whether to include checksums for all files.
	/// </summary>
	public bool IncludeChecksums { get; init; } = true;

	/// <summary>
	/// Maximum size for evidence items (bytes). Larger items are referenced instead of embedded.
	/// </summary>
	public long MaxEvidenceItemSize { get; init; } = 10 * 1024 * 1024; // 10MB

	/// <summary>
	/// Password to encrypt the package (optional).
	/// </summary>
	public string? EncryptionPassword { get; init; }

	/// <summary>
	/// Compression level for the package.
	/// </summary>
	public CompressionLevel CompressionLevel { get; init; } = CompressionLevel.Optimal;
}

/// <summary>
/// Result of export validation.
/// </summary>
public record ExportValidationResult
{
	/// <summary>
	/// Whether the report can be exported.
	/// </summary>
	public required bool IsValid { get; init; }

	/// <summary>
	/// Validation issues found.
	/// </summary>
	public IReadOnlyList<string> Issues { get; init; } = [];

	/// <summary>
	/// Warnings that don't prevent export.
	/// </summary>
	public IReadOnlyList<string> Warnings { get; init; } = [];

	/// <summary>
	/// Creates a successful validation result.
	/// </summary>
	public static ExportValidationResult Success() => new() { IsValid = true };

	/// <summary>
	/// Creates a failed validation result.
	/// </summary>
	/// <param name="issues">The validation issues.</param>
	public static ExportValidationResult Failed(params string[] issues) =>
		new() { IsValid = false, Issues = issues };
}
