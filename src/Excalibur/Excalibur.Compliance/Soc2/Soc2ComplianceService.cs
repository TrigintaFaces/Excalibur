// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Microsoft.Extensions.Options;

namespace Excalibur.Compliance.Soc2;

/// <summary>
/// Default implementation of <see cref="ISoc2ComplianceService"/>.
/// </summary>
internal sealed class Soc2ComplianceService : ISoc2ComplianceService, ISoc2AuditExporter
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
		ArgumentNullException.ThrowIfNull(options);
		ArgumentNullException.ThrowIfNull(controlValidation);

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
						// No score means the criterion was never assessed, so there is nothing to grade the
						// gap against. Critical is the safe reading: an unassessed criterion is the one a
						// reader most needs to look at, and understating it would bury it.
						Severity = criterionStatus.EffectivenessScore is { } score
							? DetermineGapSeverity(score)
							: GapSeverity.Critical,
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
					// The section now carries the same three states as the criterion it reports, so the
					// distinction no longer stops at CriterionStatus and fails to reach the report.
					Outcome = status.CriterionStatuses.TryGetValue(criterion, out var cs)
						? cs.Outcome
						: CriterionOutcome.NotAssessed
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
			OverallLevel = status.OverallLevel,
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
					// The section now carries the same three states as the criterion it reports, so the
					// distinction no longer stops at CriterionStatus and fails to reach the report.
					Outcome = status.CriterionStatuses.TryGetValue(criterion, out var cs)
						? cs.Outcome
						: CriterionOutcome.NotAssessed
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
			OverallLevel = status.OverallLevel,
			Exceptions = MapGapsToExceptions(status.ActiveGaps),
			GeneratedAt = DateTimeOffset.UtcNow,
			TenantId = options.TenantId
		};
	}

	/// <inheritdoc />
	public Task<IReadOnlyList<ControlValidationResult>> ValidateCriterionAsync(
		TrustServicesCriterion criterion,
		CancellationToken cancellationToken) =>
		_controlValidation.ValidateCriterionAsync(criterion, cancellationToken);

	/// <inheritdoc />
	public Task<AuditEvidence> GetEvidenceAsync(
		TrustServicesCriterion criterion,
		DateTimeOffset periodStart,
		DateTimeOffset periodEnd,
		CancellationToken cancellationToken)
	{
		// This build collects no SOC 2 evidence: there is no evidence store behind this member, and
		// returning an empty AuditEvidence was worse than returning nothing. It carried a real SHA-256
		// chain-of-custody hash over the empty set, so an auditor received a well-formed, cryptographically
		// signed artifact attesting to evidence that was never collected -- and nothing in it said so.
		// Failing is the only answer that cannot be mistaken for evidence.
		_ = criterion;
		_ = periodStart;
		_ = periodEnd;
		_ = cancellationToken;

		throw new NotSupportedException(
			"No audit-evidence store is configured, so no evidence can be produced for this criterion. "
			+ "Collecting and retaining SOC 2 evidence is the deploying organisation's responsibility; this "
			+ "framework does not gather it. Obtain evidence from the system that retains it.");
	}

	/// <inheritdoc />
	public Task<byte[]> ExportForAuditorAsync(
		ExportFormat format,
		DateTimeOffset periodStart,
		DateTimeOffset periodEnd,
		CancellationToken cancellationToken)
	{
		// An empty byte[] is indistinguishable from a successful export of an empty period, which is why
		// this cannot stay: the caller is handing the result to an auditor. See GetEvidenceAsync above.
		_ = format;
		_ = periodStart;
		_ = periodEnd;
		_ = cancellationToken;

		throw new NotSupportedException(
			"No audit-evidence store is configured, so there is nothing to export. Produce the auditor "
			+ "package from the system that retains the evidence.");
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
			// Nothing supporting this criterion was assessed. The previous shape had no way to say that:
			// it reported met=false with LastValidated = UtcNow, so "never checked" reached the auditor as
			// "checked just now, and failed".
			return CriterionStatus.NotAssessed(criterion, "No controls configured for this criterion");
		}

		// The WORST band established, not a mean. A mean over control bands is monotone in the wrong
		// direction: the bands deliberately place a proven violation BELOW a control nobody could
		// verify -- right at the control level, because a finding is knowledge and an open question is
		// not -- and averaging inverts that. Measured: two violating controls score 20 (Critical),
		// while replacing one of them with an unverified control scores 30 (High). Examining less
		// produced the friendlier number and the softer gap severity beside it.
		//
		// A criterion is no stronger than its weakest control, so Min says what the mean could not.
		// Min over the BAND, which is an ordered enum, so this is the worst fact established and not the
		// smallest number. The criterion percentage keeps its integer form because that is what the
		// report and the monitoring threshold consume; the band is what the controls actually reported.
		var worstBand = results.Min(r => r.EffectivenessScore);
		var allEffective = results.All(r => r.Outcome == ControlOutcome.Effective);
		var gaps = results
			.SelectMany(r => r.ConfigurationIssues)
			.ToList();

		return CriterionStatus.Assessed(
			criterion,
			// The threshold comparison that used to sit beside this is gone, and its removal is the
			// point rather than a tidy-up: the outcome is now DERIVED from the band, so every control
			// being Effective already means the worst band IS Effective. A clause that cannot
			// independently fail is not a second check, and keeping it would suggest the two could
			// disagree -- which is exactly the state this change made unconstructible.
			met: allEffective,
			effectivenessScore: (int)worstBand,
			lastValidated: results.Max(r => r.ValidatedAt),
			controlsAssessed: results.Count,
			evidenceCount: results.Sum(r => r.Evidence.Count),
			gaps: gaps);
	}

	private static CategoryStatus BuildCategoryStatus(
		TrustServicesCategory category,
		List<CriterionStatus> criterionStatuses)
	{
		// Unassessed criteria leave the percentage entirely. Counting them as failures understated
		// compliance; dropping them silently would overstate it, so the count of what was actually
		// assessed travels in the result rather than being implied by the denominator.
		var assessed = criterionStatuses.Where(c => c.Outcome != CriterionOutcome.NotAssessed).ToList();
		var metCount = assessed.Count(c => c.Outcome == CriterionOutcome.Met);

		// NOTHING ASSESSED IS NOT ZERO PER CENT, and the difference reaches an external auditor.
		// The percentage above is undefined when the denominator is empty, and substituting 0 fed the
		// ladder below its lowest rung: a consumer who had registered no validators - the DEFAULT, since
		// validators are opt-in - was reported NonCompliant, which is the level a consumer sees
		// .Adverse. That is the worst verdict available, asserted on evidence that does not exist.
		//
		// Unknown is the honest level and it already exists; DetermineOpinion maps it to Disclaimer,
		// which is exactly what an auditor says when the evidence was never gathered. CriteriaAssessed
		// and CriteriaEnabled carry the coverage, so a reader can tell "nothing was looked at" from
		// "everything was looked at and failed" - two facts the single percentage could not separate.
		if (assessed.Count == 0)
		{
			return new CategoryStatus
			{
				Category = category,
				Level = ComplianceLevel.Unknown,
				CompliancePercentage = 0,
				CriteriaAssessed = 0,
				CriteriaEnabled = criterionStatuses.Count,
				CriteriaWithIssues = 0
			};
		}

		var percentage = metCount * 100 / assessed.Count;

		return new CategoryStatus
		{
			Category = category,
			Level = percentage >= 90 ? ComplianceLevel.FullyCompliant
				: percentage >= 70 ? ComplianceLevel.SubstantiallyCompliant
				: percentage >= 50 ? ComplianceLevel.PartiallyCompliant
				: ComplianceLevel.NonCompliant,
			CompliancePercentage = percentage,
			// Both counts are over what was ASSESSED. Subtracting metCount from the full list would put
			// every unassessed criterion in the with-issues column, which is the old conflation moved.
			CriteriaAssessed = assessed.Count,
			CriteriaEnabled = criterionStatuses.Count,
			CriteriaWithIssues = assessed.Count - metCount
		};
	}

	private static ComplianceLevel CalculateOverallLevel(IEnumerable<CategoryStatus> categoryStatuses)
	{
		var statuses = categoryStatuses.ToList();
		if (statuses.Count == 0)
		{
			return ComplianceLevel.Unknown;
		}

		// A CATEGORY NOBODY ASSESSED MUST NOT VOTE, and leaving it in the ladder was wrong in BOTH
		// directions. ComplianceLevel.Unknown is the LAST enum member, so `All(s => s.Level <=
		// SubstantiallyCompliant)` is false when any category is Unknown and the method fell through to
		// PartiallyCompliant -- a QUALIFIED audit opinion on a report where nothing was examined. The
		// other direction is the one this bead names: before Unknown existed here, an unassessed
		// category arrived as NonCompliant and forced Adverse.
		//
		// Excluding them is the same rule BuildCategoryStatus already applies one level down, where
		// unassessed criteria leave the percentage. Coverage is not discarded: it travels in each
		// category's CriteriaAssessed and CriteriaEnabled.
		statuses = statuses.Where(s => s.Level != ComplianceLevel.Unknown).ToList();

		if (statuses.Count == 0)
		{
			// Every category was enabled and none was assessed. Unknown -> Disclaimer, which is an
			// auditor declining to give an opinion rather than giving a bad one.
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

	private static List<ReportException> MapGapsToExceptions(IReadOnlyList<ComplianceGap> gaps) =>
		[.. gaps.Select(g => new ReportException
		{
			ExceptionId = g.GapId,
			Criterion = g.Criterion,
			// A gap is raised against the criterion; it names a control only when one is at fault.
			ControlId = null,
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
