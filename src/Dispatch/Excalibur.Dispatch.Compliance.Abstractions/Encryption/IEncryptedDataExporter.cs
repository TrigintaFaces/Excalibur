// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Compliance;

/// <summary>
/// Specifies the format for encrypted data export.
/// </summary>
public enum EncryptedDataExportFormat
{
	/// <summary>
	/// JSON format for general interoperability.
	/// </summary>
	Json = 0,

	/// <summary>
	/// Binary format for compact storage.
	/// </summary>
	Binary = 1,

	/// <summary>
	/// PKCS#7/CMS format for cryptographic interoperability.
	/// </summary>
	Pkcs7 = 2,

	/// <summary>
	/// JWE (JSON Web Encryption) format for web interoperability.
	/// </summary>
	Jwe = 3,
}

/// <summary>
/// Severity levels for compliance issues.
/// </summary>
public enum ComplianceIssueSeverity
{
	/// <summary>
	/// Informational finding.
	/// </summary>
	Info = 0,

	/// <summary>
	/// Warning that should be addressed.
	/// </summary>
	Warning = 1,

	/// <summary>
	/// Error that must be remediated.
	/// </summary>
	Error = 2,

	/// <summary>
	/// Critical issue requiring immediate attention.
	/// </summary>
	Critical = 3,
}

/// <summary>
/// Provides export capabilities for encrypted data to support portability and interoperability.
/// </summary>
/// <remarks>
/// <para>
/// This interface supports exporting encrypted data in various formats:
/// - Portable formats for cross-system compatibility
/// - Compliance report formats for auditing
/// - Key material export for disaster recovery
/// </para>
/// <para>
/// Export operations maintain encryption - data is never exported in plaintext.
/// Key material exports require explicit authorization and audit logging.
/// </para>
/// </remarks>
public interface IEncryptedDataExporter
{
	/// <summary>
	/// Exports encrypted data to a portable format.
	/// </summary>
	/// <param name="encryptedData"> The encrypted data to export. </param>
	/// <param name="format"> The export format to use. </param>
	/// <param name="cancellationToken"> A token to cancel the operation. </param>
	/// <returns> The exported data package. </returns>
	Task<ExportPackage> ExportAsync(
		EncryptedData encryptedData,
		EncryptedDataExportFormat format,
		CancellationToken cancellationToken);

	/// <summary>
	/// Exports a batch of encrypted data items.
	/// </summary>
	/// <param name="items"> The encrypted data items to export. </param>
	/// <param name="format"> The export format to use. </param>
	/// <param name="options"> Options controlling the export. </param>
	/// <param name="cancellationToken"> A token to cancel the operation. </param>
	/// <returns> The exported data package containing all items. </returns>
	Task<ExportPackage> ExportBatchAsync(
		IReadOnlyList<EncryptedData> items,
		EncryptedDataExportFormat format,
		EncryptedDataExportOptions? options,
		CancellationToken cancellationToken);

	/// <summary>
	/// Imports encrypted data from an export package.
	/// </summary>
	/// <param name="package"> The export package to import. </param>
	/// <param name="cancellationToken"> A token to cancel the operation. </param>
	/// <returns> The imported encrypted data items. </returns>
	Task<IReadOnlyList<EncryptedData>> ImportAsync(
		ExportPackage package,
		CancellationToken cancellationToken);

	/// <summary>
	/// Generates a compliance report for encrypted data.
	/// </summary>
	/// <param name="items"> The encrypted data items to include in the report. </param>
	/// <param name="options"> Options for the compliance report. </param>
	/// <param name="cancellationToken"> A token to cancel the operation. </param>
	/// <returns> The compliance report. </returns>
	Task<ComplianceReport> GenerateComplianceReportAsync(
		IReadOnlyList<EncryptedData> items,
		ComplianceReportOptions? options,
		CancellationToken cancellationToken);

	/// <summary>
	/// Exports key metadata for audit or disaster recovery purposes.
	/// </summary>
	/// <param name="keyIds"> The key IDs to export metadata for, or null for all keys. </param>
	/// <param name="cancellationToken"> A token to cancel the operation. </param>
	/// <returns> The key metadata export. </returns>
	Task<KeyMetadataExport> ExportKeyMetadataAsync(
		IEnumerable<string>? keyIds,
		CancellationToken cancellationToken);
}

/// <summary>
/// Represents a portable package of exported encrypted data.
/// </summary>
public sealed record ExportPackage
{
	/// <summary>
	/// Gets the package format version.
	/// </summary>
	public required int Version { get; init; }

	/// <summary>
	/// Gets the export format used.
	/// </summary>
	public required EncryptedDataExportFormat Format { get; init; }

	/// <summary>
	/// Gets the exported data payload.
	/// </summary>
	public required byte[] Data { get; init; }

	/// <summary>
	/// Gets the number of items in the package.
	/// </summary>
	public required int ItemCount { get; init; }

	/// <summary>
	/// Gets the timestamp when the export was created.
	/// </summary>
	public required DateTimeOffset ExportedAt { get; init; }

	/// <summary>
	/// Gets the checksum of the exported data for integrity verification.
	/// </summary>
	public required string Checksum { get; init; }

	/// <summary>
	/// Gets metadata about the export.
	/// </summary>
	public IReadOnlyDictionary<string, string>? Metadata { get; init; }
}

/// <summary>
/// Options for encrypted data export operations.
/// </summary>
public sealed record EncryptedDataExportOptions
{
	/// <summary>
	/// Gets a value indicating whether to include key metadata in the export.
	/// </summary>
	public bool IncludeKeyMetadata { get; init; }

	/// <summary>
	/// Gets a value indicating whether to compress the export.
	/// </summary>
	public bool Compress { get; init; }

	/// <summary>
	/// Gets the maximum items per export package (for splitting large exports).
	/// </summary>
	public int? MaxItemsPerPackage { get; init; }

	/// <summary>
	/// Gets additional metadata to include in the export.
	/// </summary>
	public IReadOnlyDictionary<string, string>? AdditionalMetadata { get; init; }

	/// <summary>
	/// Gets the default export options.
	/// </summary>
	public static EncryptedDataExportOptions Default => new();
}

/// <summary>
/// Represents a compliance report for encrypted data.
/// </summary>
public sealed record ComplianceReport
{
	/// <summary>
	/// Gets the report identifier.
	/// </summary>
	public required string ReportId { get; init; }

	/// <summary>
	/// Gets the timestamp when the report was generated.
	/// </summary>
	public required DateTimeOffset GeneratedAt { get; init; }

	/// <summary>
	/// Gets the total number of items analyzed.
	/// </summary>
	public required int TotalItems { get; init; }

	/// <summary>
	/// Gets the number of compliant items.
	/// </summary>
	public required int CompliantItems { get; init; }

	/// <summary>
	/// Gets the number of non-compliant items.
	/// </summary>
	public required int NonCompliantItems { get; init; }

	/// <summary>
	/// Gets the compliance issues found.
	/// </summary>
	public IReadOnlyList<ComplianceIssue>? Issues { get; init; }

	/// <summary>
	/// Gets the summary statistics.
	/// </summary>
	public IReadOnlyDictionary<string, object>? Statistics { get; init; }

	/// <summary>
	/// Gets the compliance percentage.
	/// </summary>
	public double CompliancePercentage => TotalItems > 0 ? (double)CompliantItems / TotalItems * 100 : 100;
}

/// <summary>
/// Represents a compliance issue found during analysis.
/// </summary>
public sealed record ComplianceIssue
{
	/// <summary>
	/// Gets the issue severity.
	/// </summary>
	public required ComplianceIssueSeverity Severity { get; init; }

	/// <summary>
	/// Gets the issue code.
	/// </summary>
	public required string Code { get; init; }

	/// <summary>
	/// Gets the issue description.
	/// </summary>
	public required string Description { get; init; }

	/// <summary>
	/// Gets the affected item identifiers.
	/// </summary>
	public IReadOnlyList<string>? AffectedItems { get; init; }

	/// <summary>
	/// Gets the recommended remediation.
	/// </summary>
	public string? Remediation { get; init; }
}

/// <summary>
/// Options for compliance report generation.
/// </summary>
public sealed record ComplianceReportOptions
{
	/// <summary>
	/// Gets the compliance requirements to check.
	/// </summary>
	public ComplianceRequirements? Requirements { get; init; }

	/// <summary>
	/// Gets a value indicating whether to include detailed issue analysis.
	/// </summary>
	public bool IncludeDetails { get; init; } = true;

	/// <summary>
	/// Gets a value indicating whether to include remediation recommendations.
	/// </summary>
	public bool IncludeRemediation { get; init; } = true;

	/// <summary>
	/// Gets the maximum issues to include per category.
	/// </summary>
	public int? MaxIssuesPerCategory { get; init; }
}

/// <summary>
/// Represents exported key metadata for audit or disaster recovery.
/// </summary>
public sealed record KeyMetadataExport
{
	/// <summary>
	/// Gets the export identifier.
	/// </summary>
	public required string ExportId { get; init; }

	/// <summary>
	/// Gets the timestamp when the export was created.
	/// </summary>
	public required DateTimeOffset ExportedAt { get; init; }

	/// <summary>
	/// Gets the exported key metadata.
	/// </summary>
	public required IReadOnlyList<KeyMetadata> Keys { get; init; }

	/// <summary>
	/// Gets the checksum for integrity verification.
	/// </summary>
	public required string Checksum { get; init; }
}
