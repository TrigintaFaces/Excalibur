// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics.CodeAnalysis;

namespace Excalibur.Dispatch.Compliance;

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
	/// Whether the control is operating effectively.
	/// </summary>
	public required bool IsEffective { get; init; }

	/// <summary>
	/// Effectiveness score (0-100).
	/// </summary>
	public required int EffectivenessScore { get; init; }

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
	/// Items tested.
	/// </summary>
	public required int ItemsTested { get; init; }

	/// <summary>
	/// Exceptions found.
	/// </summary>
	public required int ExceptionsFound { get; init; }

	/// <summary>
	/// Exception details.
	/// </summary>
	public IReadOnlyList<TestException> Exceptions { get; init; } = [];

	/// <summary>
	/// Overall test outcome.
	/// </summary>
	public required TestOutcome Outcome { get; init; }

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
