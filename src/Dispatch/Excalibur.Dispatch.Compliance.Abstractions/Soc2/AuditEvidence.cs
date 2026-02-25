// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Compliance;

/// <summary>
/// Comprehensive audit evidence for SOC 2 reporting.
/// </summary>
public record AuditEvidence
{
	/// <summary>
	/// Criterion this evidence supports.
	/// </summary>
	public required TrustServicesCriterion Criterion { get; init; }

	/// <summary>
	/// Period start covered by this evidence.
	/// </summary>
	public required DateTimeOffset PeriodStart { get; init; }

	/// <summary>
	/// Period end covered by this evidence.
	/// </summary>
	public required DateTimeOffset PeriodEnd { get; init; }

	/// <summary>
	/// Evidence items collected.
	/// </summary>
	public required IReadOnlyList<EvidenceItem> Items { get; init; }

	/// <summary>
	/// Summary statistics.
	/// </summary>
	public required EvidenceSummary Summary { get; init; }

	/// <summary>
	/// Chain of custody hash for integrity verification.
	/// </summary>
	public required string ChainOfCustodyHash { get; init; }
}

/// <summary>
/// A piece of audit evidence.
/// </summary>
public record EvidenceItem
{
	/// <summary>
	/// Evidence identifier.
	/// </summary>
	public required string EvidenceId { get; init; }

	/// <summary>
	/// Type of evidence.
	/// </summary>
	public required EvidenceType Type { get; init; }

	/// <summary>
	/// Evidence description.
	/// </summary>
	public required string Description { get; init; }

	/// <summary>
	/// Evidence source.
	/// </summary>
	public required string Source { get; init; }

	/// <summary>
	/// When the evidence was collected.
	/// </summary>
	public required DateTimeOffset CollectedAt { get; init; }

	/// <summary>
	/// Reference to the actual evidence data.
	/// </summary>
	public string? DataReference { get; init; }
}

/// <summary>
/// Types of audit evidence.
/// </summary>
public enum EvidenceType
{
	/// <summary>Configuration screenshot or export.</summary>
	Configuration,

	/// <summary>Audit log entries.</summary>
	AuditLog,

	/// <summary>System metrics.</summary>
	Metrics,

	/// <summary>Policy document.</summary>
	Policy,

	/// <summary>Test results.</summary>
	TestResult,

	/// <summary>User acknowledgment.</summary>
	Acknowledgment,

	/// <summary>Third-party attestation.</summary>
	Attestation
}

/// <summary>
/// Summary of evidence collected.
/// </summary>
public record EvidenceSummary
{
	/// <summary>
	/// Total evidence items.
	/// </summary>
	public required int TotalItems { get; init; }

	/// <summary>
	/// Items by type.
	/// </summary>
	public required IReadOnlyDictionary<EvidenceType, int> ByType { get; init; }

	/// <summary>
	/// Audit log entries count.
	/// </summary>
	public required int AuditLogEntries { get; init; }

	/// <summary>
	/// Configuration snapshots count.
	/// </summary>
	public required int ConfigurationSnapshots { get; init; }

	/// <summary>
	/// Test results count.
	/// </summary>
	public required int TestResults { get; init; }
}
