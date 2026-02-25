// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Compliance;

/// <summary>
/// Default implementation of <see cref="ISoc2ComplianceService"/>.
/// </summary>
public sealed class Soc2ComplianceService : ISoc2ComplianceService, ISoc2AuditExporter
{
	private readonly Soc2Options _options;
	private readonly IControlValidationService _controlValidation;

	/// <summary>
	/// Initializes a new instance of the <see cref="Soc2ComplianceService"/> class.
	/// </summary>
	/// <param name="options">SOC 2 configuration options.</param>
	/// <param name="controlValidation">Control validation service.</param>
	public Soc2ComplianceService(
		IOptions<Soc2Options> options,
		IControlValidationService controlValidation)
	{
		_options = options.Value;
		_controlValidation = controlValidation;
	}

	/// <inheritdoc />
	public async Task<ComplianceStatus> GetComplianceStatusAsync(
		string? tenantId,
		CancellationToken cancellationToken)
	{
		var categoryStatuses = new Dictionary<TrustServicesCategory, CategoryStatus>();
		var criterionStatuses = new Dictionary<TrustServicesCriterion, CriterionStatus>();
		var activeGaps = new List<ComplianceGap>();

		foreach (var category in _options.EnabledCategories)
		{
			var criteria = category.GetCriteria().ToList();
			var categoryResults = new List<CriterionStatus>();

			foreach (var criterion in criteria)
			{
				var controlIds = _controlValidation.GetControlsForCriterion(criterion);
				var validationResults = new List<ControlValidationResult>();

				foreach (var controlId in controlIds)
				{
					var result = await _controlValidation.ValidateControlAsync(controlId, cancellationToken).ConfigureAwait(false);
					validationResults.Add(result);
				}

				var criterionStatus = BuildCriterionStatus(criterion, validationResults);
				criterionStatuses[criterion] = criterionStatus;
				categoryResults.Add(criterionStatus);

				// Collect gaps
				foreach (var gap in criterionStatus.Gaps)
				{
					activeGaps.Add(new ComplianceGap
					{
						GapId = $"{criterion}-{Guid.NewGuid():N}",
						Criterion = criterion,
						Description = gap,
						Severity = DetermineGapSeverity(criterionStatus.EffectivenessScore),
						Remediation = $"Review and remediate: {gap}",
						IdentifiedAt = DateTimeOffset.UtcNow
					});
				}
			}

			categoryStatuses[category] = BuildCategoryStatus(category, categoryResults);
		}

		return new ComplianceStatus
		{
			OverallLevel = CalculateOverallLevel(categoryStatuses.Values),
			CategoryStatuses = categoryStatuses,
			CriterionStatuses = criterionStatuses,
			ActiveGaps = activeGaps,
			EvaluatedAt = DateTimeOffset.UtcNow,
			TenantId = tenantId
		};
	}

	/// <inheritdoc />
	public async Task<Soc2Report> GenerateTypeIReportAsync(
		DateTimeOffset asOfDate,
		ReportOptions options,
		CancellationToken cancellationToken)
	{
		var status = await GetComplianceStatusAsync(options.TenantId, cancellationToken).ConfigureAwait(false);
		var categories = options.Categories ?? _options.EnabledCategories;

		var controlSections = new List<ControlSection>();
		foreach (var category in categories)
		{
			foreach (var criterion in category.GetCriteria())
			{
				var controls = await GetControlDescriptionsAsync(criterion, cancellationToken).ConfigureAwait(false);
				controlSections.Add(new ControlSection
				{
					Criterion = criterion,
					Description = criterion.GetDisplayName(),
					Controls = controls,
					TestResults = null, // Type I doesn't include test results
					IsMet = status.CriterionStatuses.TryGetValue(criterion, out var cs) && cs.IsMet
				});
			}
		}

		return new Soc2Report
		{
			ReportId = Guid.NewGuid(),
			ReportType = Soc2ReportType.TypeI,
			Title = options.CustomTitle ?? $"SOC 2 Type I Report - {asOfDate:yyyy-MM-dd}",
			PeriodStart = asOfDate,
			PeriodEnd = asOfDate,
			CategoriesIncluded = categories.ToList(),
			System = _options.SystemDescription ?? CreateDefaultSystemDescription(),
			ControlSections = controlSections,
			Opinion = DetermineOpinion(status),
			Exceptions = MapGapsToExceptions(status.ActiveGaps),
			GeneratedAt = DateTimeOffset.UtcNow,
			TenantId = options.TenantId
		};
	}

	/// <inheritdoc />
	public async Task<Soc2Report> GenerateTypeIIReportAsync(
		DateTimeOffset periodStart,
		DateTimeOffset periodEnd,
		ReportOptions options,
		CancellationToken cancellationToken)
	{
		ValidateTypeIIPeriod(periodStart, periodEnd);

		var status = await GetComplianceStatusAsync(options.TenantId, cancellationToken).ConfigureAwait(false);
		var categories = options.Categories ?? _options.EnabledCategories;

		var controlSections = new List<ControlSection>();
		foreach (var category in categories)
		{
			foreach (var criterion in category.GetCriteria())
			{
				var controls = await GetControlDescriptionsAsync(criterion, cancellationToken).ConfigureAwait(false);
				var testResults = options.IncludeTestResults
					? await GetTestResultsAsync(criterion, periodStart, periodEnd, cancellationToken).ConfigureAwait(false)
					: null;

				controlSections.Add(new ControlSection
				{
					Criterion = criterion,
					Description = criterion.GetDisplayName(),
					Controls = controls,
					TestResults = testResults,
					IsMet = status.CriterionStatuses.TryGetValue(criterion, out var cs) && cs.IsMet
				});
			}
		}

		return new Soc2Report
		{
			ReportId = Guid.NewGuid(),
			ReportType = Soc2ReportType.TypeII,
			Title = options.CustomTitle ?? $"SOC 2 Type II Report - {periodStart:yyyy-MM-dd} to {periodEnd:yyyy-MM-dd}",
			PeriodStart = periodStart,
			PeriodEnd = periodEnd,
			CategoriesIncluded = categories.ToList(),
			System = _options.SystemDescription ?? CreateDefaultSystemDescription(),
			ControlSections = controlSections,
			Opinion = DetermineOpinion(status),
			Exceptions = MapGapsToExceptions(status.ActiveGaps),
			GeneratedAt = DateTimeOffset.UtcNow,
			TenantId = options.TenantId
		};
	}

	/// <inheritdoc />
	public async Task<ControlValidationResult> ValidateControlAsync(
		TrustServicesCriterion criterion,
		CancellationToken cancellationToken)
	{
		var controlIds = _controlValidation.GetControlsForCriterion(criterion);
		if (controlIds.Count == 0)
		{
			return new ControlValidationResult
			{
				ControlId = criterion.ToString(),
				IsConfigured = false,
				IsEffective = false,
				EffectivenessScore = 0,
				ConfigurationIssues = [$"No controls registered for criterion {criterion}"]
			};
		}

		// Validate the first control for this criterion
		return await _controlValidation.ValidateControlAsync(controlIds[0], cancellationToken).ConfigureAwait(false);
	}

	/// <inheritdoc />
	public Task<AuditEvidence> GetEvidenceAsync(
		TrustServicesCriterion criterion,
		DateTimeOffset periodStart,
		DateTimeOffset periodEnd,
		CancellationToken cancellationToken)
	{
		// In a real implementation, this would query the evidence store
		var evidence = new AuditEvidence
		{
			Criterion = criterion,
			PeriodStart = periodStart,
			PeriodEnd = periodEnd,
			Items = [],
			Summary = new EvidenceSummary
			{
				TotalItems = 0,
				ByType = new Dictionary<EvidenceType, int>(),
				AuditLogEntries = 0,
				ConfigurationSnapshots = 0,
				TestResults = 0
			},
			ChainOfCustodyHash = ComputeChainOfCustodyHash([])
		};

		return Task.FromResult(evidence);
	}

	/// <inheritdoc />
	public Task<byte[]> ExportForAuditorAsync(
		ExportFormat format,
		DateTimeOffset periodStart,
		DateTimeOffset periodEnd,
		CancellationToken cancellationToken)
	{
		// In a real implementation, this would generate the actual export
		// For now, return an empty array - the report export service handles this
		return Task.FromResult(Array.Empty<byte>());
	}

	/// <inheritdoc />
	public object? GetService(Type serviceType)
	{
		if (serviceType == typeof(ISoc2AuditExporter))
		{
			return this;
		}

		return null;
	}

	private static CriterionStatus BuildCriterionStatus(
		TrustServicesCriterion criterion,
		List<ControlValidationResult> results)
	{
		if (results.Count == 0)
		{
			return new CriterionStatus
			{
				Criterion = criterion,
				IsMet = false,
				EffectivenessScore = 0,
				LastValidated = DateTimeOffset.UtcNow,
				EvidenceCount = 0,
				Gaps = ["No controls configured for this criterion"]
			};
		}

		var avgScore = (int)results.Average(r => r.EffectivenessScore);
		var allEffective = results.All(r => r.IsEffective);
		var gaps = results
			.SelectMany(r => r.ConfigurationIssues)
			.ToList();

		return new CriterionStatus
		{
			Criterion = criterion,
			IsMet = allEffective && avgScore >= 80,
			EffectivenessScore = avgScore,
			LastValidated = results.Max(r => r.ValidatedAt),
			EvidenceCount = results.Sum(r => r.Evidence.Count),
			Gaps = gaps
		};
	}

	private static CategoryStatus BuildCategoryStatus(
		TrustServicesCategory category,
		List<CriterionStatus> criterionStatuses)
	{
		var metCount = criterionStatuses.Count(c => c.IsMet);
		var percentage = criterionStatuses.Count > 0
			? metCount * 100 / criterionStatuses.Count
			: 0;

		return new CategoryStatus
		{
			Category = category,
			Level = percentage >= 90 ? ComplianceLevel.FullyCompliant
				: percentage >= 70 ? ComplianceLevel.SubstantiallyCompliant
				: percentage >= 50 ? ComplianceLevel.PartiallyCompliant
				: ComplianceLevel.NonCompliant,
			CompliancePercentage = percentage,
			ActiveControls = criterionStatuses.Count,
			ControlsWithIssues = criterionStatuses.Count - metCount
		};
	}

	private static ComplianceLevel CalculateOverallLevel(IEnumerable<CategoryStatus> categoryStatuses)
	{
		var statuses = categoryStatuses.ToList();
		if (statuses.Count == 0)
		{
			return ComplianceLevel.Unknown;
		}

		if (statuses.All(s => s.Level == ComplianceLevel.FullyCompliant))
		{
			return ComplianceLevel.FullyCompliant;
		}

		if (statuses.Any(s => s.Level == ComplianceLevel.NonCompliant))
		{
			return ComplianceLevel.NonCompliant;
		}

		if (statuses.All(s => s.Level <= ComplianceLevel.SubstantiallyCompliant))
		{
			return ComplianceLevel.SubstantiallyCompliant;
		}

		return ComplianceLevel.PartiallyCompliant;
	}

	private static GapSeverity DetermineGapSeverity(int effectivenessScore) =>
		effectivenessScore switch
		{
			< 25 => GapSeverity.Critical,
			< 50 => GapSeverity.High,
			< 75 => GapSeverity.Medium,
			_ => GapSeverity.Low
		};

	private static AuditorOpinion DetermineOpinion(ComplianceStatus status) =>
		status.OverallLevel switch
		{
			ComplianceLevel.FullyCompliant => AuditorOpinion.Unqualified,
			ComplianceLevel.SubstantiallyCompliant => AuditorOpinion.Qualified,
			ComplianceLevel.PartiallyCompliant => AuditorOpinion.Qualified,
			ComplianceLevel.NonCompliant => AuditorOpinion.Adverse,
			_ => AuditorOpinion.Disclaimer
		};

	private static List<ReportException> MapGapsToExceptions(IReadOnlyList<ComplianceGap> gaps) =>
		[.. gaps.Select(g => new ReportException
		{
			ExceptionId = g.GapId,
			Criterion = g.Criterion,
			ControlId = "N/A",
			Description = g.Description,
			ManagementResponse = null,
			RemediationPlan = g.Remediation
		})];

	private static SystemDescription CreateDefaultSystemDescription() =>
		new()
		{
			Name = "Excalibur framework",
			Description = "Message dispatching and event sourcing framework for .NET",
			Services = ["Message Dispatching", "Event Sourcing", "Outbox Pattern"],
			Infrastructure = ["Cloud-agnostic", "Pluggable providers"],
			DataTypes = ["Domain Events", "Commands", "Queries"]
		};

	private static string ComputeChainOfCustodyHash(IReadOnlyList<EvidenceItem> items)
	{
		// In a real implementation, this would compute a cryptographic hash
		return Convert.ToBase64String(
			System.Security.Cryptography.SHA256.HashData(
				System.Text.Encoding.UTF8.GetBytes(
					string.Join("|", items.Select(i => i.EvidenceId)))));
	}

	private Task<IReadOnlyList<ControlDescription>> GetControlDescriptionsAsync(
		TrustServicesCriterion criterion,
		CancellationToken cancellationToken)
	{
		_ = cancellationToken; // Future use for async operations
		var controlIds = _controlValidation.GetControlsForCriterion(criterion);
		var descriptions = new List<ControlDescription>();

		foreach (var controlId in controlIds)
		{
			// This would normally come from a control catalog
			descriptions.Add(new ControlDescription
			{
				ControlId = controlId,
				Name = $"Control {controlId}",
				Description = $"Control implementation for {criterion.GetDisplayName()}",
				Implementation = "Automated control implementation",
				Type = ControlType.Preventive,
				Frequency = ControlFrequency.Continuous
			});
		}

		return Task.FromResult<IReadOnlyList<ControlDescription>>(descriptions);
	}

	private async Task<IReadOnlyList<TestResult>> GetTestResultsAsync(
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

			var testResult = await _controlValidation.RunControlTestAsync(controlId, parameters, cancellationToken).ConfigureAwait(false);
			results.Add(new TestResult
			{
				ControlId = controlId,
				TestProcedure = $"Automated test for {controlId}",
				SampleSize = testResult.ItemsTested,
				ExceptionsFound = testResult.ExceptionsFound,
				Outcome = testResult.Outcome,
				Notes = null
			});
		}

		return results;
	}

	private void ValidateTypeIIPeriod(DateTimeOffset periodStart, DateTimeOffset periodEnd)
	{
		var days = (periodEnd - periodStart).TotalDays;
		if (days < _options.MinimumTypeIIPeriodDays)
		{
			throw new ArgumentException(
				$"Type II report period must be at least {_options.MinimumTypeIIPeriodDays} days. " +
				$"Provided period is {days:F0} days.");
		}
	}
}
