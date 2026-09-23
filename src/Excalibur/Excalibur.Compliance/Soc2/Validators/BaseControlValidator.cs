// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0


namespace Excalibur.Compliance.Soc2.Validators;

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
			// This method does not sample anything — it forwards a ValidateAsync verdict. ItemsTested
			// used to be parameters.SampleSize, the number REQUESTED, so every Type II report told an
			// auditor that 25 items had been examined when none had. Zero is the true answer and needs
			// no other state; the requested size remains available on Parameters.
			ItemsTested = 0,

			// A count of findings asserts a search ran. None did, so there is no number — and that is a
			// different fact from "we looked and found none", which is what 0 said.
			ExceptionsFound = null,

			// NotTested is WRONG here, and it was mine: I set it unconditionally, so a control whose
			// validator RAN and found it effective reported "no test performed" -- which an auditor reads
			// as a coverage gap the customer does not have. Worse, Soc2ReportGenerator collects every
			// outcome that is not NoExceptions into the report's findings, so it turned all fourteen
			// controls into exceptions against the consumer.
			//
			// A validator ALWAYS ran by the time we are here -- this method is on the validator. So the
			// genuine no-validator case is not ours to report; ControlValidationService owns it and
			// already returns NotTested for an unroutable control. What is true here is that the control
			// was ASSESSED but not SAMPLED, and that is carried by ItemsTested = 0 and the note below
			// rather than by overwriting the verdict.
			// RESOLVED, and the enum did not need a new member. The deadlock was that one field was being
			// asked to carry two facts: what a TEST found, and whether the CONTROL is effective. NotTested
			// is the honest answer to the first -- no sample was drawn -- and it only understated anything
			// while it was also the sole channel for the second.
			//
			// The verdict now travels on ControlSection.ValidationResults, which is where the report builds
			// its findings from, so this field is free to say the true thing about the test.
			Outcome = TestOutcome.NotTested,
			Notes =
				"No sampling was performed. This outcome is the control validation verdict; the item and "
				+ "exception counts are absent rather than zero because no population was examined.",
			Evidence = validation.Evidence,
			Exceptions = validation.Outcome == ControlOutcome.Effective
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
			EffectivenessScore = ControlEffectiveness.Effective,
			ConfigurationIssues = [],
			Evidence = evidence ?? [],
			ValidatedAt = DateTimeOffset.UtcNow
		};
	}

	/// <summary>
	/// Creates a failed validation result.
	/// </summary>
	/// <param name="controlId">The control this verdict is about.</param>
	/// <param name="issues">What the validator found, in the consumer's terms.</param>
	/// <param name="effectivenessScore">
	/// The band this validation established. <b>Deliberately has no default.</b> The default used to be
	/// <see cref="ControlEffectiveness.MechanismAbsent" />, so the shortest call a caller could write —
	/// two arguments — silently asserted that the control's mechanism does not exist, when the caller
	/// meant only to record an issue. That assertion travels into the document a consumer hands to an
	/// external assessor, so there is no safe value to assume and the caller states it.
	/// </param>
	/// <param name="evidence">Evidence collected while validating, if any.</param>
	/// <param name="isConfigured">
	/// Whether the mechanism is configured. Independent of the band: a control may be configured and
	/// still unverifiable here, and one may be absent locally yet satisfied by an external arrangement.
	/// </param>
	protected static ControlValidationResult CreateFailureResult(
		string controlId,
		IReadOnlyList<string> issues,
		ControlEffectiveness effectivenessScore,
		IReadOnlyList<EvidenceItem>? evidence = null,
		bool isConfigured = false)
	{
		return new ControlValidationResult
		{
			ControlId = controlId,
			// Was issues.Count == 0, which in a FAILURE result is false almost by construction: it made
			// every control we could not verify additionally assert that its mechanism is absent, while
			// the same method's evidence said the mechanism ships. One validator hand-built its result
			// inline to escape exactly that, which was the signal this belonged here.
			//
			// Deriving it from the score band was tried and is WRONG, which a test caught: AVL-002 and
			// AVL-003 report Unverified for a declared control that is ABSENT here but may exist as an
			// external arrangement, while SEC-004 reports Unverified for a mechanism demonstrably
			// present. Configured-ness and the band are genuinely independent, so the caller states it.
			IsConfigured = isConfigured,
			// Outcome is NOT set here and cannot be: it is a derived, get-only property of
			// ControlValidationResult, computed from the band. It used to be derived at this one call
			// site, which left a consumer's own validator free to state a verdict its own band
			// contradicts -- and a shipped conformance kit certified exactly that. Deriving it on the
			// type moved the guarantee from "our factory is careful" to "the pair cannot be written".
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
