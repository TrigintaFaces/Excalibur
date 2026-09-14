// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Compliance;

/// <summary>
/// Service for SOC 2 compliance management and reporting.
/// </summary>
/// <remarks>
/// <para>
/// Core compliance operations. For audit export operations, use
/// <see cref="GetService"/> with <c>typeof(ISoc2AuditExporter)</c>.
/// </para>
/// <para>
/// <strong>ISP Split:</strong> ExportForAuditorAsync moved to
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
	/// Validates every control that the specified criterion covers.
	/// </summary>
	/// <param name="criterion">The criterion whose controls are validated.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>
	/// One result per control the criterion covers. The list is empty when the criterion has no
	/// registered controls, which reports an assessment that did not run rather than one that failed.
	/// </returns>
	Task<IReadOnlyList<ControlValidationResult>> ValidateCriterionAsync(
		TrustServicesCriterion criterion,
		CancellationToken cancellationToken);

	/// <summary>
	/// Gets audit evidence for a specific criterion and period.
	/// </summary>
	/// <param name="criterion">The criterion to get evidence for.</param>
	/// <param name="periodStart">The start of the evidence period.</param>
	/// <param name="periodEnd">The end of the evidence period.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>The audit evidence for the criterion over the period.</returns>
	/// <exception cref="System.NotSupportedException">
	/// The implementation has no evidence store to draw on. <b>Collecting and retaining SOC 2 evidence is
	/// the deploying organisation's responsibility</b>; an implementation that does not gather it must throw
	/// rather than return an empty result, because an empty <see cref="AuditEvidence"/> is indistinguishable
	/// from a period in which nothing happened, and an auditor cannot tell the two apart.
	/// </exception>
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
