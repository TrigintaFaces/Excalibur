// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Compliance;

/// <summary>
/// Export format options.
/// </summary>
public enum ExportFormat
{
	/// <summary>PDF report.</summary>
	Pdf,

	/// <summary>Excel workbook.</summary>
	Excel,

	/// <summary>CSV files (zipped).</summary>
	Csv,

	/// <summary>JSON data.</summary>
	Json,

	/// <summary>XML format.</summary>
	Xml
}

/// <summary>
/// Report generation options.
/// </summary>
public record ReportOptions
{
	/// <summary>
	/// Categories to include. If null, includes all enabled categories.
	/// </summary>
	public TrustServicesCategory[]? Categories { get; init; }

	/// <summary>
	/// Whether to include detailed evidence.
	/// </summary>
	public bool IncludeDetailedEvidence { get; init; } = true;

	/// <summary>
	/// Whether to include test results (Type II only).
	/// </summary>
	public bool IncludeTestResults { get; init; } = true;

	/// <summary>
	/// Tenant filter.
	/// </summary>
	public string? TenantId { get; init; }

	/// <summary>
	/// Custom report title.
	/// </summary>
	public string? CustomTitle { get; init; }

	/// <summary>
	/// Whether to include management's assertion.
	/// </summary>
	public bool IncludeManagementAssertion { get; init; } = true;

	/// <summary>
	/// Whether to include system description.
	/// </summary>
	public bool IncludeSystemDescription { get; init; } = true;

	/// <summary>
	/// Maximum evidence items per criterion.
	/// </summary>
	public int? MaxEvidenceItemsPerCriterion { get; init; }
}
