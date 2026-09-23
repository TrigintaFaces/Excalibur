// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0


using System.Diagnostics.CodeAnalysis;

namespace Excalibur.Compliance;

/// <summary>
/// Result of control validation.
/// </summary>
public record ControlValidationResult
{
	/// <summary>
	/// Control identifier.
	/// </summary>
	public required string ControlId { get; init; }

	/// <summary>
	/// Whether the control is properly configured.
	/// </summary>
	public required bool IsConfigured { get; init; }

	/// <summary>
	/// What this validation established about the control.
	/// </summary>
	/// <remarks>
	/// <para>
	/// <b>Derived from <see cref="EffectivenessScore" />, never stated separately.</b> A result whose
	/// verdict contradicts its own band is not rejected here — it cannot be written. The band is the
	/// finer fact and it already determines the verdict, so offering a second place to say so would
	/// only offer a second place to say something different.
	/// </para>
	/// <para>
	/// <b>Three states, because there are three facts.</b> A boolean verdict can say that a control is
	/// operating and that it is not; it has nowhere to put "nobody looked". A validator that could not
	/// examine a control is then forced to pick one of the two, and both choices are false statements
	/// to an external auditor. See <see cref="ControlOutcome" /> for why the distinction is
	/// load-bearing.
	/// </para>
	/// </remarks>
	public ControlOutcome Outcome => OutcomeFor(EffectivenessScore);

	/// <summary>
	/// What this validation established about the control's effectiveness, as one of the reportable
	/// bands.
	/// </summary>
	/// <remarks>
	/// Finer-grained than <see cref="Outcome" />, which it determines: the band separates a control
	/// whose mechanism is absent from one that was examined and found broken, which
	/// <see cref="ControlOutcome.Deficient" /> covers jointly. Only a declared band may be supplied —
	/// a value outside <see cref="ControlEffectiveness" /> is rejected at construction, because the
	/// ordering the report relies on is defined only over the declared members.
	/// </remarks>
	/// <exception cref="ArgumentOutOfRangeException">
	/// The supplied value is not a declared <see cref="ControlEffectiveness" /> member.
	/// </exception>
	public required ControlEffectiveness EffectivenessScore
	{
		get => _effectiveness;
		init => _effectiveness = Enum.IsDefined(value)
			? value
			: throw new ArgumentOutOfRangeException(
				nameof(value),
				value,
				"Only a declared ControlEffectiveness band may be reported. The bands are a closed, "
				+ "ordered set and an undeclared value has no place in that order, so nothing "
				+ "downstream — the verdict, the criterion aggregate, the assessor-facing report — can "
				+ "say what it means.");
	}

	private readonly ControlEffectiveness _effectiveness;

	/// <summary>
	/// Maps a reportable band to the verdict it asserts.
	/// </summary>
	/// <remarks>
	/// Only <see cref="ControlEffectiveness.Unverified" /> means "not examined". Every lower band is a
	/// finding the validator actually made, and <see cref="ControlEffectiveness.Effective" /> is the
	/// only band reporting assurance.
	/// </remarks>
	private static ControlOutcome OutcomeFor(ControlEffectiveness effectiveness) => effectiveness switch
	{
		ControlEffectiveness.Effective => ControlOutcome.Effective,
		ControlEffectiveness.Unverified => ControlOutcome.NotVerified,
		_ => ControlOutcome.Deficient,
	};

	/// <summary>
	/// Configuration issues found.
	/// </summary>
	public IReadOnlyList<string> ConfigurationIssues { get; init; } = [];

	/// <summary>
	/// Evidence collected during validation.
	/// </summary>
	public IReadOnlyList<EvidenceItem> Evidence { get; init; } = [];

	/// <summary>
	/// Validation timestamp.
	/// </summary>
	public DateTimeOffset ValidatedAt { get; init; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// Parameters for control testing.
/// </summary>
public record ControlTestParameters
{
	/// <summary>
	/// Sample size for testing.
	/// </summary>
	public int SampleSize { get; init; } = 25;

	/// <summary>
	/// Test period start.
	/// </summary>
	public DateTimeOffset PeriodStart { get; init; }

	/// <summary>
	/// Test period end.
	/// </summary>
	public DateTimeOffset PeriodEnd { get; init; }

	/// <summary>
	/// Whether to include detailed evidence.
	/// </summary>
	public bool IncludeDetailedEvidence { get; init; } = true;
}

/// <summary>
/// Result of a control test.
/// </summary>
public record ControlTestResult
{
	/// <summary>
	/// Control tested.
	/// </summary>
	public required string ControlId { get; init; }

	/// <summary>
	/// Test parameters used.
	/// </summary>
	public required ControlTestParameters Parameters { get; init; }

	/// <summary>
	/// How many items were actually examined.
	/// </summary>
	/// <remarks>
	/// <b>Zero is a true statement and the type needs no other state</b>: no items were examined.
	/// This must never be populated from the sample size that was REQUESTED — that number is in
	/// <see cref="Parameters"/>, and echoing it here reports a sample that was never drawn.
	/// </remarks>
	public required int ItemsTested { get; init; }

	/// <summary>
	/// How many exceptions the test found, or <see langword="null"/> when no test ran.
	/// </summary>
	/// <remarks>
	/// <b>Zero here is not the same kind of statement as zero items tested.</b> A count of findings
	/// asserts that a search happened and returned nothing, which is the most favourable reading
	/// available and the one an auditor is most likely to act on. Where no test ran there is no
	/// such number, and the honest value is absent rather than zero.
	/// </remarks>
	public int? ExceptionsFound { get; init; }

	/// <summary>
	/// Exception details.
	/// </summary>
	public IReadOnlyList<TestException> Exceptions { get; init; } = [];

	/// <summary>
	/// Overall test outcome.
	/// </summary>
	public required TestOutcome Outcome { get; init; }

	/// <summary>
	/// Why the test produced this outcome, when the outcome alone does not say.
	/// </summary>
	/// <value>
	/// Required reading for <see cref="TestOutcome.NotTested"/>, which states that no test ran but
	/// not why; <see langword="null"/> when the outcome is self-explanatory.
	/// </value>
	public string? Notes { get; init; }

	/// <summary>
	/// Evidence collected during testing.
	/// </summary>
	public IReadOnlyList<EvidenceItem> Evidence { get; init; } = [];
}

/// <summary>
/// An exception found during testing.
/// </summary>
[SuppressMessage("Naming", "CA1711:Identifiers should not have incorrect suffix", Justification = "Represents a SOC2 test exception (audit finding), not a runtime exception.")]
public record TestException
{
	/// <summary>
	/// Item identifier where exception occurred.
	/// </summary>
	public required string ItemId { get; init; }

	/// <summary>
	/// Exception description.
	/// </summary>
	public required string Description { get; init; }

	/// <summary>
	/// Severity of the exception.
	/// </summary>
	public required GapSeverity Severity { get; init; }

	/// <summary>
	/// When the exception occurred.
	/// </summary>
	public required DateTimeOffset OccurredAt { get; init; }
}
