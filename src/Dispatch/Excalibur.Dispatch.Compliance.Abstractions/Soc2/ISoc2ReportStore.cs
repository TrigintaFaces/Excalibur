// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Compliance;

/// <summary>
/// Sort order options for report queries.
/// </summary>
public enum ReportSortOrder
{
	/// <summary>Most recent reports first.</summary>
	GeneratedAtDescending,

	/// <summary>Oldest reports first.</summary>
	GeneratedAtAscending,

	/// <summary>Most recent period first.</summary>
	PeriodEndDescending,

	/// <summary>Oldest period first.</summary>
	PeriodEndAscending
}

/// <summary>
/// Storage interface for SOC 2 compliance reports.
/// </summary>
/// <remarks>
/// <para>
/// Reports should be stored for audit purposes with
/// tamper-evident integrity verification and configurable retention
/// (default: 7 years per <see cref="Soc2Options.EvidenceRetentionPeriod"/>).
/// </para>
/// </remarks>
public interface ISoc2ReportStore
{
	/// <summary>
	/// Saves a generated report.
	/// </summary>
	/// <param name="report">The report to save.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>A task representing the asynchronous operation.</returns>
	Task SaveReportAsync(
		Soc2Report report,
		CancellationToken cancellationToken);

	/// <summary>
	/// Gets a report by its identifier.
	/// </summary>
	/// <param name="reportId">The report identifier.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>The report if found; otherwise null.</returns>
	Task<Soc2Report?> GetReportAsync(
		Guid reportId,
		CancellationToken cancellationToken);

	/// <summary>
	/// Lists reports matching the specified criteria.
	/// </summary>
	/// <param name="filter">Filter criteria for the query.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>Reports matching the filter criteria.</returns>
	Task<IReadOnlyList<ReportSummary>> ListReportsAsync(
		ReportFilter filter,
		CancellationToken cancellationToken);

	/// <summary>
	/// Deletes a report by its identifier.
	/// </summary>
	/// <param name="reportId">The report identifier.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>True if the report was deleted; false if not found.</returns>
	/// <remarks>
	/// Consider retention policies before deleting reports.
	/// Audit regulations may require reports to be retained for 7+ years.
	/// </remarks>
	Task<bool> DeleteReportAsync(
		Guid reportId,
		CancellationToken cancellationToken);

	/// <summary>
	/// Gets the total count of reports matching the filter.
	/// </summary>
	/// <param name="filter">Filter criteria for the query.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>The total count of matching reports.</returns>
	Task<int> GetReportCountAsync(
		ReportFilter filter,
		CancellationToken cancellationToken);
}

/// <summary>
/// Filter criteria for querying reports.
/// </summary>
public record ReportFilter
{
	/// <summary>
	/// Filter by report type.
	/// </summary>
	public Soc2ReportType? ReportType { get; init; }

	/// <summary>
	/// Filter by tenant.
	/// </summary>
	public string? TenantId { get; init; }

	/// <summary>
	/// Filter by minimum generation date.
	/// </summary>
	public DateTimeOffset? GeneratedAfter { get; init; }

	/// <summary>
	/// Filter by maximum generation date.
	/// </summary>
	public DateTimeOffset? GeneratedBefore { get; init; }

	/// <summary>
	/// Filter by period start (for Type II reports).
	/// </summary>
	public DateTimeOffset? PeriodStartAfter { get; init; }

	/// <summary>
	/// Filter by period end (for Type II reports).
	/// </summary>
	public DateTimeOffset? PeriodEndBefore { get; init; }

	/// <summary>
	/// Filter by opinion type.
	/// </summary>
	public AuditorOpinion? Opinion { get; init; }

	/// <summary>
	/// Maximum number of results to return.
	/// </summary>
	public int? MaxResults { get; init; }

	/// <summary>
	/// Number of results to skip (for pagination).
	/// </summary>
	public int? Skip { get; init; }

	/// <summary>
	/// Sort order for results.
	/// </summary>
	public ReportSortOrder SortOrder { get; init; } = ReportSortOrder.GeneratedAtDescending;
}

/// <summary>
/// Summary information about a report for list queries.
/// </summary>
public record ReportSummary
{
	/// <summary>
	/// Report identifier.
	/// </summary>
	public required Guid ReportId { get; init; }

	/// <summary>
	/// Report type.
	/// </summary>
	public required Soc2ReportType ReportType { get; init; }

	/// <summary>
	/// Report title.
	/// </summary>
	public required string Title { get; init; }

	/// <summary>
	/// Period start.
	/// </summary>
	public required DateTimeOffset PeriodStart { get; init; }

	/// <summary>
	/// Period end.
	/// </summary>
	public required DateTimeOffset PeriodEnd { get; init; }

	/// <summary>
	/// Report generation timestamp.
	/// </summary>
	public required DateTimeOffset GeneratedAt { get; init; }

	/// <summary>
	/// Auditor opinion.
	/// </summary>
	public required AuditorOpinion Opinion { get; init; }

	/// <summary>
	/// Number of exceptions.
	/// </summary>
	public required int ExceptionCount { get; init; }

	/// <summary>
	/// Categories included in the report.
	/// </summary>
	public required IReadOnlyList<TrustServicesCategory> CategoriesIncluded { get; init; }

	/// <summary>
	/// Tenant context.
	/// </summary>
	public string? TenantId { get; init; }
}
