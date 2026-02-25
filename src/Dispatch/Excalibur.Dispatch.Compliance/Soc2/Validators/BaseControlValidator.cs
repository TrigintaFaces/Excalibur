// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Compliance;

/// <summary>
/// Base class for SOC 2 control validators.
/// </summary>
public abstract class BaseControlValidator : IControlValidator
{
	/// <inheritdoc />
	public abstract IReadOnlyList<string> SupportedControls { get; }

	/// <inheritdoc />
	public abstract IReadOnlyList<TrustServicesCriterion> SupportedCriteria { get; }

	/// <inheritdoc />
	public abstract Task<ControlValidationResult> ValidateAsync(
		string controlId,
		CancellationToken cancellationToken);

	/// <inheritdoc />
	public virtual async Task<ControlTestResult> RunTestAsync(
		string controlId,
		ControlTestParameters parameters,
		CancellationToken cancellationToken)
	{
		var validation = await ValidateAsync(controlId, cancellationToken).ConfigureAwait(false);

		return new ControlTestResult
		{
			ControlId = controlId,
			Parameters = parameters,
			ItemsTested = parameters.SampleSize,
			ExceptionsFound = validation.IsEffective ? 0 : 1,
			Outcome = validation.IsEffective ? TestOutcome.NoExceptions : TestOutcome.SignificantExceptions,
			Evidence = validation.Evidence,
			Exceptions = validation.IsEffective
				? []
				: [new TestException
				{
					ItemId = controlId,
					Description = string.Join("; ", validation.ConfigurationIssues),
					Severity = GapSeverity.High,
					OccurredAt = DateTimeOffset.UtcNow
				}]
		};
	}

	/// <inheritdoc />
	public abstract ControlDescription? GetControlDescription(string controlId);

	/// <summary>
	/// Creates a successful validation result.
	/// </summary>
	protected static ControlValidationResult CreateSuccessResult(
		string controlId,
		IReadOnlyList<EvidenceItem>? evidence = null)
	{
		return new ControlValidationResult
		{
			ControlId = controlId,
			IsConfigured = true,
			IsEffective = true,
			EffectivenessScore = 100,
			ConfigurationIssues = [],
			Evidence = evidence ?? [],
			ValidatedAt = DateTimeOffset.UtcNow
		};
	}

	/// <summary>
	/// Creates a failed validation result.
	/// </summary>
	protected static ControlValidationResult CreateFailureResult(
		string controlId,
		IReadOnlyList<string> issues,
		int effectivenessScore = 0,
		IReadOnlyList<EvidenceItem>? evidence = null)
	{
		return new ControlValidationResult
		{
			ControlId = controlId,
			IsConfigured = issues.Count == 0,
			IsEffective = false,
			EffectivenessScore = effectivenessScore,
			ConfigurationIssues = issues,
			Evidence = evidence ?? [],
			ValidatedAt = DateTimeOffset.UtcNow
		};
	}

	/// <summary>
	/// Creates an evidence item.
	/// </summary>
	protected static EvidenceItem CreateEvidence(
		EvidenceType type,
		string description,
		string source,
		string? dataReference = null)
	{
		return new EvidenceItem
		{
			EvidenceId = Guid.NewGuid().ToString("N"),
			Type = type,
			Description = description,
			Source = source,
			CollectedAt = DateTimeOffset.UtcNow,
			DataReference = dataReference
		};
	}
}
