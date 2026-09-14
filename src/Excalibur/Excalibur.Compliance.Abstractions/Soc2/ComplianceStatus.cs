// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Compliance;

/// <summary>
/// Overall SOC 2 compliance status.
/// </summary>
public record ComplianceStatus
{
	/// <summary>
	/// Overall compliance level.
	/// </summary>
	public required ComplianceLevel OverallLevel { get; init; }

	/// <summary>
	/// Status for each Trust Services category.
	/// </summary>
	public required IReadOnlyDictionary<TrustServicesCategory, CategoryStatus> CategoryStatuses { get; init; }

	/// <summary>
	/// Detailed status for each criterion.
	/// </summary>
	public required IReadOnlyDictionary<TrustServicesCriterion, CriterionStatus> CriterionStatuses { get; init; }

	/// <summary>
	/// Active compliance gaps requiring attention.
	/// </summary>
	public IReadOnlyList<ComplianceGap> ActiveGaps { get; init; } = [];

	/// <summary>
	/// When the status was last evaluated.
	/// </summary>
	public DateTimeOffset EvaluatedAt { get; init; } = DateTimeOffset.UtcNow;

	/// <summary>
	/// Tenant context (null for system-wide).
	/// </summary>
	public string? TenantId { get; init; }
}

/// <summary>
/// Compliance levels.
/// </summary>
public enum ComplianceLevel
{
	/// <summary>All controls effective, no gaps.</summary>
	FullyCompliant,

	/// <summary>Minor gaps, controls generally effective.</summary>
	SubstantiallyCompliant,

	/// <summary>Significant gaps requiring remediation.</summary>
	PartiallyCompliant,

	/// <summary>Critical gaps, immediate action required.</summary>
	NonCompliant,

	/// <summary>Status unknown, assessment needed.</summary>
	Unknown
}

/// <summary>
/// Status for a Trust Services category.
/// </summary>
public record CategoryStatus
{
	/// <summary>
	/// Category being evaluated.
	/// </summary>
	public required TrustServicesCategory Category { get; init; }

	/// <summary>
	/// Compliance level for this category.
	/// </summary>
	public required ComplianceLevel Level { get; init; }

	/// <summary>
	/// Percentage of ASSESSED criteria that were met (0-100).
	/// </summary>
	/// <remarks>
	/// Criteria no registered validator assessed are excluded from this figure entirely -- from the
	/// numerator and the denominator both. That is the only honest treatment (counting them as
	/// failures understates the consumer's posture on evidence nobody gathered; dropping them from
	/// the numerator alone overstates it), but it means <b>this percentage cannot be read without
	/// <see cref="CriteriaAssessed"/> and <see cref="CriteriaEnabled"/> beside it</b>: 100% over one
	/// criterion of nine is not 100% compliance, and a reader given only the percentage has no way
	/// to tell the two apart. The coverage travels in the same type for that reason.
	/// </remarks>
	public required int CompliancePercentage { get; init; }

	/// <summary>
	/// How many of this category's criteria a registered validator actually assessed -- the
	/// denominator of <see cref="CompliancePercentage"/>.
	/// </summary>
	/// <remarks>
	/// Named for criteria because it counts criteria. It was <c>ActiveControls</c>, which named a
	/// different unit than it held and invited a reader to compare it against a control count.
	/// </remarks>
	public required int CriteriaAssessed { get; init; }

	/// <summary>
	/// How many criteria this category contains, assessed or not.
	/// </summary>
	/// <remarks>
	/// The coverage denominator. Where this exceeds <see cref="CriteriaAssessed"/> the difference is
	/// criteria this framework did not assess, and the percentage says nothing about them.
	/// </remarks>
	public required int CriteriaEnabled { get; init; }

	/// <summary>
	/// How many ASSESSED criteria were not met.
	/// </summary>
	public required int CriteriaWithIssues { get; init; }
}

/// <summary>
/// Whether a criterion was assessed, and if so what the assessment concluded.
/// </summary>
public enum CriterionOutcome
{
	/// <summary>
	/// No control supporting this criterion was assessed. <b>This is not a failure</b>: nothing is
	/// known about the criterion either way, and it must not be counted as met or as not met.
	/// </summary>
	NotAssessed = 0,

	/// <summary>
	/// The criterion was assessed and its controls were found effective.
	/// </summary>
	Met = 1,

	/// <summary>
	/// The criterion was assessed and its controls were not found effective.
	/// </summary>
	NotMet = 2
}

/// <summary>
/// Status for a specific criterion.
/// </summary>
/// <remarks>
/// <para>
/// Construct through <see cref="Assessed"/> or <see cref="NotAssessed"/>. The two states carry
/// different evidence and an object initialiser cannot enforce that: an unassessed criterion bearing
/// a validation timestamp would compile, and a reader of the resulting attestation could not tell it
/// from a real one.
/// </para>
/// <para>
/// <b>A private constructor alone did not achieve that, and it was measured rather than assumed.</b>
/// It blocks <c>new</c>; it does not block <c>with</c>, whose copy constructor is compiler-generated
/// and reachable wherever the members are settable. With public <c>init</c> accessors this compiled
/// clean:
/// <code>
/// var honest = CriterionStatus.NotAssessed(criterion, "nobody looked");
/// var forged = honest with { Outcome = CriterionOutcome.Met, EffectivenessScore = 100 };
/// </code>
/// The accessors are therefore <c>private init</c>. The factories inside this type still build every
/// legitimate value; the forgery above is now a compile error rather than a discouraged habit.
/// </para>
/// <para>
/// This type is read by an external auditor assessing the deploying organisation, so a value that reads
/// as a measurement but was never measured is a finding against that organisation. That is why the
/// absent cases are absent here rather than defaulted.
/// </para>
/// </remarks>
public sealed record CriterionStatus
{
	private CriterionStatus()
	{
	}

	/// <summary>
	/// Criterion being evaluated.
	/// </summary>
	public TrustServicesCriterion Criterion { get; private init; }

	/// <summary>
	/// Whether the criterion was assessed, and what was concluded.
	/// </summary>
	public CriterionOutcome Outcome { get; private init; }

	/// <summary>
	/// Control effectiveness (0-100), or <see langword="null"/> when the criterion was not assessed.
	/// </summary>
	public int? EffectivenessScore { get; private init; }

	/// <summary>
	/// When the assessment happened, or <see langword="null"/> when it never did.
	/// </summary>
	public DateTimeOffset? LastValidated { get; private init; }

	/// <summary>
	/// How many controls supporting this criterion were assessed.
	/// </summary>
	/// <remarks>
	/// Travels with <see cref="EffectivenessScore"/> deliberately: a mean over an unstated denominator
	/// is the same defect one layer out, a definite-looking number whose basis the reader cannot see.
	/// </remarks>
	public int ControlsAssessed { get; private init; }

	/// <summary>
	/// Evidence count for this criterion.
	/// </summary>
	public int EvidenceCount { get; private init; }

	/// <summary>
	/// Any gaps identified.
	/// </summary>
	public IReadOnlyList<string> Gaps { get; private init; } = [];

	/// <summary>
	/// Records the result of an assessment that actually took place.
	/// </summary>
	/// <param name="criterion">The criterion assessed.</param>
	/// <param name="met">Whether the assessed controls were found effective.</param>
	/// <param name="effectivenessScore">Mean control effectiveness, 0-100.</param>
	/// <param name="lastValidated">When the assessment happened.</param>
	/// <param name="controlsAssessed">How many controls the score was computed over.</param>
	/// <param name="evidenceCount">Evidence items collected.</param>
	/// <param name="gaps">Gaps identified during the assessment.</param>
	/// <returns>An assessed criterion status.</returns>
	public static CriterionStatus Assessed(
		TrustServicesCriterion criterion,
		bool met,
		int effectivenessScore,
		DateTimeOffset lastValidated,
		int controlsAssessed,
		int evidenceCount,
		IReadOnlyList<string>? gaps = null) =>
		new()
		{
			Criterion = criterion,
			Outcome = met ? CriterionOutcome.Met : CriterionOutcome.NotMet,
			EffectivenessScore = effectivenessScore,
			LastValidated = lastValidated,
			ControlsAssessed = controlsAssessed,
			EvidenceCount = evidenceCount,
			Gaps = gaps ?? []
		};

	/// <summary>
	/// Records that a criterion was not assessed at all.
	/// </summary>
	/// <param name="criterion">The criterion that was not assessed.</param>
	/// <param name="reason">Why it was not assessed, for the reader of the attestation.</param>
	/// <returns>An unassessed criterion status, carrying no score and no timestamp.</returns>
	/// <remarks>
	/// There is deliberately no parameter for a score or a timestamp. Neither exists, and a factory that
	/// accepted one would put the fabrication back within reach of the next author.
	/// </remarks>
	public static CriterionStatus NotAssessed(TrustServicesCriterion criterion, string reason) =>
		new()
		{
			Criterion = criterion,
			Outcome = CriterionOutcome.NotAssessed,
			EffectivenessScore = null,
			LastValidated = null,
			ControlsAssessed = 0,
			EvidenceCount = 0,
			Gaps = [reason]
		};
}

/// <summary>
/// A compliance gap requiring remediation.
/// </summary>
public record ComplianceGap
{
	/// <summary>
	/// Gap identifier.
	/// </summary>
	public required string GapId { get; init; }

	/// <summary>
	/// Affected criterion.
	/// </summary>
	public required TrustServicesCriterion Criterion { get; init; }

	/// <summary>
	/// Description of the gap.
	/// </summary>
	public required string Description { get; init; }

	/// <summary>
	/// Severity level.
	/// </summary>
	public required GapSeverity Severity { get; init; }

	/// <summary>
	/// Recommended remediation.
	/// </summary>
	public required string Remediation { get; init; }

	/// <summary>
	/// When the gap was identified.
	/// </summary>
	public required DateTimeOffset IdentifiedAt { get; init; }

	/// <summary>
	/// Target remediation date.
	/// </summary>
	public DateTimeOffset? TargetRemediationDate { get; init; }
}

/// <summary>
/// Gap severity levels.
/// </summary>
public enum GapSeverity
{
	/// <summary>Minor issue, low risk.</summary>
	Low,

	/// <summary>Moderate issue, should be addressed.</summary>
	Medium,

	/// <summary>Significant issue, priority remediation.</summary>
	High,

	/// <summary>Critical issue, immediate action required.</summary>
	Critical
}
