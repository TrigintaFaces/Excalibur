// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Compliance;

/// <summary>
/// Service for generating SOC 2 compliance reports.
/// </summary>
/// <remarks>
/// <para>
/// This service supports generation of both Type I (point-in-time)
/// and Type II (period-based) SOC 2 compliance reports.
/// </para>
/// <para>
/// Type I reports assess control design at a specific point in time, while
/// Type II reports assess both design and operating effectiveness over a period
/// (minimum 90 days per SOC 2 standards).
/// </para>
/// </remarks>
public interface ISoc2ReportGenerator
{
	/// <summary>
	/// Generates a SOC 2 Type I report (point-in-time assessment).
	/// </summary>
	/// <param name="asOfDate">The point-in-time date for the assessment.</param>
	/// <param name="options">Report generation options.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>The generated Type I report.</returns>
	/// <remarks>
	/// Type I reports assess whether controls are suitably designed as of a
	/// specific date. They do not test operating effectiveness.
	/// </remarks>
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
	/// <exception cref="ArgumentException">
	/// Thrown when the period is less than the minimum required (typically 90 days).
	/// </exception>
	/// <remarks>
	/// Type II reports assess both control design and operating effectiveness
	/// over a period of time. The minimum period is typically 90 days but can
	/// be configured via <see cref="Soc2Options.MinimumTypeIIPeriodDays"/>.
	/// </remarks>
	Task<Soc2Report> GenerateTypeIIReportAsync(
		DateTimeOffset periodStart,
		DateTimeOffset periodEnd,
		ReportOptions options,
		CancellationToken cancellationToken);

	/// <summary>
	/// Generates a report and stores it.
	/// </summary>
	/// <param name="request">The report generation request.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>The generated and stored report.</returns>
	Task<Soc2Report> GenerateAndStoreReportAsync(
		ReportGenerationRequest request,
		CancellationToken cancellationToken);

	/// <summary>
	/// Gets control descriptions for a specific criterion.
	/// </summary>
	/// <param name="criterion">The criterion to get controls for.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>Control descriptions mapped to the criterion.</returns>
	Task<IReadOnlyList<ControlDescription>> GetControlDescriptionsAsync(
		TrustServicesCriterion criterion,
		CancellationToken cancellationToken);

	/// <summary>
	/// Gets test results for a criterion during a specific period.
	/// </summary>
	/// <param name="criterion">The criterion to get test results for.</param>
	/// <param name="periodStart">Start of the test period.</param>
	/// <param name="periodEnd">End of the test period.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>Test results for the criterion.</returns>
	Task<IReadOnlyList<TestResult>> GetTestResultsAsync(
		TrustServicesCriterion criterion,
		DateTimeOffset periodStart,
		DateTimeOffset periodEnd,
		CancellationToken cancellationToken);
}

/// <summary>
/// Request for generating a SOC 2 report.
/// </summary>
public record ReportGenerationRequest
{
	/// <summary>
	/// Report type to generate.
	/// </summary>
	public required Soc2ReportType ReportType { get; init; }

	/// <summary>
	/// For Type I: the point-in-time date.
	/// For Type II: the period start date.
	/// </summary>
	public required DateTimeOffset PeriodStart { get; init; }

	/// <summary>
	/// For Type II: the period end date.
	/// For Type I: should equal PeriodStart or be omitted.
	/// </summary>
	public DateTimeOffset? PeriodEnd { get; init; }

	/// <summary>
	/// Report generation options.
	/// </summary>
	public required ReportOptions Options { get; init; }

	/// <summary>
	/// User requesting the report generation.
	/// </summary>
	public string? RequestedBy { get; init; }

	/// <summary>
	/// Additional metadata to include with the report.
	/// </summary>
	public IReadOnlyDictionary<string, string>? Metadata { get; init; }
}
