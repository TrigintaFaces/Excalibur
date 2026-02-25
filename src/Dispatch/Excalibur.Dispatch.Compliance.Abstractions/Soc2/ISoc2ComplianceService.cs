// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Compliance;

/// <summary>
/// Service for SOC 2 compliance management and reporting.
/// </summary>
/// <remarks>
/// <para>
/// Core compliance operations. For audit export operations, use
/// <see cref="GetService"/> with <c>typeof(ISoc2AuditExporter)</c>.
/// </para>
/// <para>
/// <strong>ISP Split (Sprint 551):</strong> ExportForAuditorAsync moved to
/// <see cref="ISoc2AuditExporter"/> to keep the core interface at or below 5 methods.
/// </para>
/// </remarks>
public interface ISoc2ComplianceService
{
	/// <summary>
	/// Gets the current compliance status for all Trust Services Criteria.
	/// </summary>
	/// <param name="tenantId">Optional tenant filter.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>The current compliance status.</returns>
	Task<ComplianceStatus> GetComplianceStatusAsync(
		string? tenantId,
		CancellationToken cancellationToken);

	/// <summary>
	/// Generates a SOC 2 Type I report (point-in-time assessment).
	/// </summary>
	/// <param name="asOfDate">The point-in-time date for the assessment.</param>
	/// <param name="options">Report generation options.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>The generated Type I report.</returns>
	Task<Soc2Report> GenerateTypeIReportAsync(
		DateTimeOffset asOfDate,
		ReportOptions options,
		CancellationToken cancellationToken);

	/// <summary>
	/// Generates a SOC 2 Type II report (period assessment).
	/// </summary>
	/// <param name="periodStart">The start of the assessment period.</param>
	/// <param name="periodEnd">The end of the assessment period.</param>
	/// <param name="options">Report generation options.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>The generated Type II report.</returns>
	Task<Soc2Report> GenerateTypeIIReportAsync(
		DateTimeOffset periodStart,
		DateTimeOffset periodEnd,
		ReportOptions options,
		CancellationToken cancellationToken);

	/// <summary>
	/// Validates control effectiveness for a specific criterion.
	/// </summary>
	/// <param name="criterion">The criterion to validate.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>The validation result.</returns>
	Task<ControlValidationResult> ValidateControlAsync(
		TrustServicesCriterion criterion,
		CancellationToken cancellationToken);

	/// <summary>
	/// Gets audit evidence for a specific criterion and period.
	/// </summary>
	/// <param name="criterion">The criterion to get evidence for.</param>
	/// <param name="periodStart">The start of the evidence period.</param>
	/// <param name="periodEnd">The end of the evidence period.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>The audit evidence.</returns>
	Task<AuditEvidence> GetEvidenceAsync(
		TrustServicesCriterion criterion,
		DateTimeOffset periodStart,
		DateTimeOffset periodEnd,
		CancellationToken cancellationToken);

	/// <summary>
	/// Gets a sub-interface or service from this provider.
	/// </summary>
	/// <param name="serviceType">The type of service to retrieve (e.g., <c>typeof(ISoc2AuditExporter)</c>).</param>
	/// <returns>The service instance, or <see langword="null"/> if not supported.</returns>
	object? GetService(Type serviceType);
}
