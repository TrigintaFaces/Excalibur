// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Globalization;
using System.Text;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Compliance;

/// <summary>
/// Implementation of <see cref="ISoc2ReportGenerator"/> for generating SOC 2 reports.
/// </summary>
/// <remarks>
/// <para>
/// This service generates Type I (point-in-time) and Type II (period-based)
/// SOC 2 compliance reports with full control validation and evidence collection.
/// </para>
/// </remarks>
public sealed partial class Soc2ReportGenerator : ISoc2ReportGenerator
{
	private static readonly CompositeFormat TypeIIPeriodTooShortFormat =
		CompositeFormat.Parse(Resources.Soc2ReportGenerator_TypeIIPeriodTooShort);

	private readonly Soc2Options _options;
	private readonly IControlValidationService _controlValidation;
	private readonly ISoc2ReportStore? _reportStore;
	private readonly ILogger<Soc2ReportGenerator> _logger;

	/// <summary>
	/// Initializes a new instance of the <see cref="Soc2ReportGenerator"/> class.
	/// </summary>
	/// <param name="options">SOC 2 configuration options.</param>
	/// <param name="controlValidation">Control validation service.</param>
	/// <param name="reportStore">Optional report store for persistence.</param>
	/// <param name="logger">Logger instance.</param>
	public Soc2ReportGenerator(
		IOptions<Soc2Options> options,
		IControlValidationService controlValidation,
		ILogger<Soc2ReportGenerator> logger,
		ISoc2ReportStore? reportStore = null)
	{
		_options = options?.Value ?? throw new ArgumentNullException(nameof(options));
		_controlValidation = controlValidation ?? throw new ArgumentNullException(nameof(controlValidation));
		_reportStore = reportStore;
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
	}

	/// <inheritdoc />
	public async Task<Soc2Report> GenerateTypeIReportAsync(
		DateTimeOffset asOfDate,
		ReportOptions options,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(options);

		LogGeneratingTypeIReport(asOfDate, options.TenantId);

		var categories = options.Categories ?? _options.EnabledCategories;
		var controlSections = await BuildControlSectionsAsync(
			categories,
			asOfDate,
			asOfDate,
			includeTestResults: false,
			cancellationToken).ConfigureAwait(false);

		var complianceStatus = CalculateComplianceStatus(controlSections);

		var report = new Soc2Report
		{
			ReportId = Guid.NewGuid(),
			ReportType = Soc2ReportType.TypeI,
			Title = options.CustomTitle ?? $"SOC 2 Type I Report - {asOfDate:yyyy-MM-dd}",
			PeriodStart = asOfDate,
			PeriodEnd = asOfDate,
			CategoriesIncluded = categories.ToList(),
			System = _options.SystemDescription ?? CreateDefaultSystemDescription(),
			ControlSections = controlSections,
			Opinion = DetermineOpinion(complianceStatus),
			Exceptions = BuildExceptions(controlSections),
			GeneratedAt = DateTimeOffset.UtcNow,
			TenantId = options.TenantId
		};

		LogGeneratedTypeIReport(report.ReportId, report.Opinion, report.Exceptions.Count);

		return report;
	}

	/// <inheritdoc />
	public async Task<Soc2Report> GenerateTypeIIReportAsync(
		DateTimeOffset periodStart,
		DateTimeOffset periodEnd,
		ReportOptions options,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(options);
		ValidateTypeIIPeriod(periodStart, periodEnd);

		LogGeneratingTypeIIReport(periodStart, periodEnd, options.TenantId);

		var categories = options.Categories ?? _options.EnabledCategories;
		var controlSections = await BuildControlSectionsAsync(
			categories,
			periodStart,
			periodEnd,
			includeTestResults: options.IncludeTestResults,
			cancellationToken).ConfigureAwait(false);

		var complianceStatus = CalculateComplianceStatus(controlSections);

		var report = new Soc2Report
		{
			ReportId = Guid.NewGuid(),
			ReportType = Soc2ReportType.TypeII,
			Title = options.CustomTitle ?? $"SOC 2 Type II Report - {periodStart:yyyy-MM-dd} to {periodEnd:yyyy-MM-dd}",
			PeriodStart = periodStart,
			PeriodEnd = periodEnd,
			CategoriesIncluded = categories.ToList(),
			System = _options.SystemDescription ?? CreateDefaultSystemDescription(),
			ControlSections = controlSections,
			Opinion = DetermineOpinion(complianceStatus),
			Exceptions = BuildExceptions(controlSections),
			GeneratedAt = DateTimeOffset.UtcNow,
			TenantId = options.TenantId
		};

		LogGeneratedTypeIIReport(report.ReportId, report.Opinion, report.Exceptions.Count);

		return report;
	}

	/// <inheritdoc />
	public async Task<Soc2Report> GenerateAndStoreReportAsync(
		ReportGenerationRequest request,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(request);

		var report = request.ReportType switch
		{
			Soc2ReportType.TypeI => await GenerateTypeIReportAsync(
				request.PeriodStart,
				request.Options,
				cancellationToken).ConfigureAwait(false),
			Soc2ReportType.TypeII => await GenerateTypeIIReportAsync(
				request.PeriodStart,
				request.PeriodEnd ?? throw new ArgumentException(
					Resources.Soc2ReportGenerator_PeriodEndRequiredForTypeII, nameof(request)),
				request.Options,
				cancellationToken).ConfigureAwait(false),
			_ => throw new ArgumentOutOfRangeException(
				nameof(request),
				request.ReportType,
				Resources.Soc2ReportGenerator_UnknownReportType)
		};

		if (_reportStore is not null)
		{
			await _reportStore.SaveReportAsync(report, cancellationToken).ConfigureAwait(false);
			LogStoredReport(report.ReportId);
		}
		else
		{
			LogReportNotStored(report.ReportId);
		}

		return report;
	}

	/// <inheritdoc />
	public async Task<IReadOnlyList<ControlDescription>> GetControlDescriptionsAsync(
		TrustServicesCriterion criterion,
		CancellationToken cancellationToken)
	{
		var controlIds = _controlValidation.GetControlsForCriterion(criterion);
		var descriptions = new List<ControlDescription>();

		foreach (var controlId in controlIds)
		{
			var metadata = GetControlMetadata(controlId, criterion);
			descriptions.Add(metadata);
		}

		return await Task.FromResult<IReadOnlyList<ControlDescription>>(descriptions).ConfigureAwait(false);
	}

	/// <inheritdoc />
	public async Task<IReadOnlyList<TestResult>> GetTestResultsAsync(
		TrustServicesCriterion criterion,
		DateTimeOffset periodStart,
		DateTimeOffset periodEnd,
		CancellationToken cancellationToken)
	{
		var controlIds = _controlValidation.GetControlsForCriterion(criterion);
		var results = new List<TestResult>();

		foreach (var controlId in controlIds)
		{
			var parameters = new ControlTestParameters
			{
				SampleSize = _options.DefaultTestSampleSize,
				PeriodStart = periodStart,
				PeriodEnd = periodEnd,
				IncludeDetailedEvidence = true
			};

			var testResult = await _controlValidation.RunControlTestAsync(
				controlId,
				parameters,
				cancellationToken).ConfigureAwait(false);

			results.Add(new TestResult
			{
				ControlId = controlId,
				TestProcedure = GetTestProcedureDescription(controlId),
				SampleSize = testResult.ItemsTested,
				ExceptionsFound = testResult.ExceptionsFound,
				Outcome = testResult.Outcome,
				Notes = testResult.Exceptions.Count > 0
					? string.Join("; ", testResult.Exceptions.Select(e => e.Description))
					: null
			});
		}

		return results;
	}

	private static ComplianceLevel CalculateComplianceStatus(IReadOnlyList<ControlSection> sections)
	{
		if (sections.Count == 0)
		{
			return ComplianceLevel.Unknown;
		}

		var metCount = sections.Count(s => s.IsMet);
		var percentage = metCount * 100 / sections.Count;

		return percentage switch
		{
			>= 90 => ComplianceLevel.FullyCompliant,
			>= 70 => ComplianceLevel.SubstantiallyCompliant,
			>= 50 => ComplianceLevel.PartiallyCompliant,
			_ => ComplianceLevel.NonCompliant
		};
	}

	private static AuditorOpinion DetermineOpinion(ComplianceLevel status) =>
		status switch
		{
			ComplianceLevel.FullyCompliant => AuditorOpinion.Unqualified,
			ComplianceLevel.SubstantiallyCompliant => AuditorOpinion.Qualified,
			ComplianceLevel.PartiallyCompliant => AuditorOpinion.Qualified,
			ComplianceLevel.NonCompliant => AuditorOpinion.Adverse,
			_ => AuditorOpinion.Disclaimer
		};

	private static List<ReportException> BuildExceptions(IReadOnlyList<ControlSection> sections)
	{
		var exceptions = new List<ReportException>();

		foreach (var section in sections.Where(s => !s.IsMet))
		{
			if (section.TestResults is not null)
			{
				foreach (var test in section.TestResults.Where(t => t.Outcome != TestOutcome.NoExceptions))
				{
					exceptions.Add(new ReportException
					{
						ExceptionId = $"EXC-{Guid.NewGuid():N}"[..12],
						Criterion = section.Criterion,
						ControlId = test.ControlId,
						Description = $"Control {test.ControlId} had {test.ExceptionsFound} exception(s) " +
									  $"out of {test.SampleSize} samples tested",
						ManagementResponse = null,
						RemediationPlan = test.Notes
					});
				}
			}
			else
			{
				// Type I report - no test results, just mark design issues
				exceptions.Add(new ReportException
				{
					ExceptionId = $"EXC-{Guid.NewGuid():N}"[..12],
					Criterion = section.Criterion,
					ControlId = "N/A",
					Description = $"Criterion {section.Criterion.GetDisplayName()} controls not suitably designed",
					ManagementResponse = null,
					RemediationPlan = "Review control design and implementation"
				});
			}
		}

		return exceptions;
	}

	private static SystemDescription CreateDefaultSystemDescription() =>
		new()
		{
			Name = "Excalibur framework",
			Description = "Message dispatching and event sourcing framework for .NET",
			Services = ["Message Dispatching", "Event Sourcing", "Outbox Pattern", "Compliance Reporting"],
			Infrastructure = ["Cloud-agnostic", "Pluggable providers", "Multi-tenant support"],
			DataTypes = ["Domain Events", "Commands", "Queries", "Audit Logs"]
		};

	private static ControlDescription GetControlMetadata(string controlId, TrustServicesCriterion criterion)
	{
		// Control metadata lookup - in a production system this would come from a catalog
		var metadata = controlId switch
		{
			"SEC-001" => ("Encryption at Rest", "Data encryption using AES-256-GCM", ControlType.Preventive),
			"SEC-002" => ("Encryption in Transit", "TLS 1.2+ for all communications", ControlType.Preventive),
			"SEC-003" => ("Key Management", "Secure key lifecycle management", ControlType.Preventive),
			"SEC-004" => ("Audit Logging", "Tamper-evident audit trail", ControlType.Detective),
			"SEC-005" => ("Security Monitoring", "Real-time security event monitoring", ControlType.Detective),
			"AVL-001" => ("Health Monitoring", "System health check endpoints", ControlType.Detective),
			"AVL-002" => ("Performance Metrics", "OpenTelemetry-based metrics", ControlType.Detective),
			"AVL-003" => ("Backup Verification", "Automated backup validation", ControlType.Corrective),
			"INT-001" => ("Input Validation", "Message validation pipeline", ControlType.Preventive),
			"INT-002" => ("Idempotency", "Duplicate message handling", ControlType.Preventive),
			"INT-003" => ("Delivery Confirmation", "Outbox delivery verification", ControlType.Detective),
			"CNF-001" => ("Data Classification", "Sensitivity-based classification", ControlType.Preventive),
			"CNF-002" => ("Data Protection", "Field-level encryption", ControlType.Preventive),
			"CNF-003" => ("Data Disposal", "Secure erasure procedures", ControlType.Corrective),
			_ => ($"Control {controlId}", $"Implementation for {criterion.GetDisplayName()}", ControlType.Preventive)
		};

		return new ControlDescription
		{
			ControlId = controlId,
			Name = metadata.Item1,
			Description = metadata.Item2,
			Implementation = $"Automated control implementation for {criterion.GetDisplayName()}",
			Type = metadata.Item3,
			Frequency = ControlFrequency.Continuous
		};
	}

	private static string GetTestProcedureDescription(string controlId) =>
		controlId switch
		{
			"SEC-001" => "Verify encryption algorithm and key strength for data at rest",
			"SEC-002" => "Verify TLS version and certificate validity for communications",
			"SEC-003" => "Verify key rotation policy and access controls",
			"SEC-004" => "Verify audit log completeness and hash chain integrity",
			"SEC-005" => "Verify security event detection and alerting",
			"AVL-001" => "Verify health check endpoint availability and accuracy",
			"AVL-002" => "Verify metrics collection and reporting",
			"AVL-003" => "Verify backup creation and restoration capabilities",
			"INT-001" => "Verify input validation rules are enforced",
			"INT-002" => "Verify duplicate message detection and handling",
			"INT-003" => "Verify message delivery confirmation and retry logic",
			"CNF-001" => "Verify data classification tagging and enforcement",
			"CNF-002" => "Verify field-level encryption for sensitive data",
			"CNF-003" => "Verify secure deletion and data disposal",
			_ => $"Automated test procedure for {controlId}"
		};

	[LoggerMessage(LogLevel.Information,
		"Generating Type I report as of {AsOfDate} for tenant {TenantId}")]
	private partial void LogGeneratingTypeIReport(DateTimeOffset asOfDate, string? tenantId);

	[LoggerMessage(LogLevel.Information,
		"Generating Type II report for period {PeriodStart} to {PeriodEnd}, tenant {TenantId}")]
	private partial void LogGeneratingTypeIIReport(
		DateTimeOffset periodStart,
		DateTimeOffset periodEnd,
		string? tenantId);

	[LoggerMessage(LogLevel.Information,
		"Generated Type I report {ReportId} with opinion {Opinion}, {ExceptionCount} exceptions")]
	private partial void LogGeneratedTypeIReport(Guid reportId, AuditorOpinion opinion, int exceptionCount);

	[LoggerMessage(LogLevel.Information,
		"Generated Type II report {ReportId} with opinion {Opinion}, {ExceptionCount} exceptions")]
	private partial void LogGeneratedTypeIIReport(Guid reportId, AuditorOpinion opinion, int exceptionCount);

	[LoggerMessage(LogLevel.Information, "Stored report {ReportId}")]
	private partial void LogStoredReport(Guid reportId);

	[LoggerMessage(LogLevel.Warning,
		"Report {ReportId} generated but not stored - no ISoc2ReportStore configured")]
	private partial void LogReportNotStored(Guid reportId);

	private async Task<IReadOnlyList<ControlSection>> BuildControlSectionsAsync(
		TrustServicesCategory[] categories,
		DateTimeOffset periodStart,
		DateTimeOffset periodEnd,
		bool includeTestResults,
		CancellationToken cancellationToken)
	{
		var sections = new List<ControlSection>();

		foreach (var category in categories)
		{
			foreach (var criterion in category.GetCriteria())
			{
				var controls = await GetControlDescriptionsAsync(criterion, cancellationToken).ConfigureAwait(false);
				var validationResults = await _controlValidation.ValidateCriterionAsync(
					criterion,
					cancellationToken).ConfigureAwait(false);

				IReadOnlyList<TestResult>? testResults = null;
				if (includeTestResults)
				{
					testResults = await GetTestResultsAsync(
						criterion,
						periodStart,
						periodEnd,
						cancellationToken).ConfigureAwait(false);
				}

				var isMet = validationResults.Count > 0 &&
							validationResults.All(r => r.IsEffective) &&
							validationResults.Average(r => r.EffectivenessScore) >= 80;

				sections.Add(new ControlSection
				{
					Criterion = criterion,
					Description = criterion.GetDisplayName(),
					Controls = controls,
					TestResults = testResults,
					IsMet = isMet
				});
			}
		}

		return sections;
	}

	private void ValidateTypeIIPeriod(DateTimeOffset periodStart, DateTimeOffset periodEnd)
	{
		if (periodEnd <= periodStart)
		{
			throw new ArgumentException(
				Resources.Soc2ReportGenerator_PeriodEndAfterStart,
				nameof(periodEnd));
		}

		var days = (periodEnd - periodStart).TotalDays;
		if (days < _options.MinimumTypeIIPeriodDays)
		{
			throw new ArgumentException(
				string.Format(
					CultureInfo.CurrentCulture,
					TypeIIPeriodTooShortFormat,
					_options.MinimumTypeIIPeriodDays,
					days));
		}
	}
}
