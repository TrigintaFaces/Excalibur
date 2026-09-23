// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0


using Excalibur.Compliance.Soc2.Validators;

namespace Excalibur.Compliance.Soc2;

/// <summary>
/// Default implementation of <see cref="IControlValidationService"/>.
/// </summary>
internal sealed class ControlValidationService : IControlValidationService
{
	private readonly IEnumerable<IControlValidator> _validators;
	private readonly Dictionary<string, IControlValidator> _controlToValidator;
	private readonly Dictionary<TrustServicesCriterion, List<string>> _criterionToControls;

	/// <summary>
	/// Initializes a new instance of the <see cref="ControlValidationService"/> class.
	/// </summary>
	/// <param name="validators">Registered control validators.</param>
	public ControlValidationService(IEnumerable<IControlValidator> validators)
	{
		_validators = validators;
		_controlToValidator = new Dictionary<string, IControlValidator>(StringComparer.OrdinalIgnoreCase);
		_criterionToControls = new Dictionary<TrustServicesCriterion, List<string>>();

		BuildControlMappings();
	}

	/// <inheritdoc />
	public async Task<ControlValidationResult> ValidateControlAsync(
		string controlId,
		CancellationToken cancellationToken)
	{
		if (!_controlToValidator.TryGetValue(controlId, out var validator))
		{
			return new ControlValidationResult
			{
				ControlId = controlId,
				IsConfigured = false,
				// NOT a deficiency. No validator registered means this framework did not examine the
				// control -- it says nothing about whether the consumer operates it by some other means.
				// Reporting it as deficient states a finding nobody made and sends an auditor looking for
				// a defect that may not exist. Outcome follows from the band and is not set here.
				EffectivenessScore = ControlEffectiveness.Unverified,
				ConfigurationIssues = [$"No validator registered for control: {controlId}"]
			};
		}

		return await validator.ValidateAsync(controlId, cancellationToken).ConfigureAwait(false);
	}

	/// <inheritdoc />
	public async Task<IReadOnlyList<ControlValidationResult>> ValidateCriterionAsync(
		TrustServicesCriterion criterion,
		CancellationToken cancellationToken)
	{
		var controlIds = GetControlsForCriterion(criterion);
		var results = new List<ControlValidationResult>();

		foreach (var controlId in controlIds)
		{
			var result = await ValidateControlAsync(controlId, cancellationToken).ConfigureAwait(false);
			results.Add(result);
		}

		return results;
	}

	/// <inheritdoc />
	public async Task<ControlTestResult> RunControlTestAsync(
		string controlId,
		ControlTestParameters parameters,
		CancellationToken cancellationToken)
	{
		if (!_controlToValidator.TryGetValue(controlId, out var validator))
		{
			return new ControlTestResult
			{
				ControlId = controlId,
				Parameters = parameters,
				ItemsTested = 0,
				// Zero here asserted a search that returned nothing. No validator ran, so there is no such
				// number — the same distinction the type now carries for every other did-not-run path.
				ExceptionsFound = null,
				// No validator means no test ran. Reporting ControlFailure here told an assessor the
				// control had been tested and failed, and the empty Exceptions list beside it made the
				// claim self-contradictory. NotTested says only what is true; Notes says why.
				Outcome = TestOutcome.NotTested,
				Exceptions = [],
				Notes = $"No validator registered for control: {controlId}"
			};
		}

		return await validator.RunTestAsync(controlId, parameters, cancellationToken).ConfigureAwait(false);
	}

	/// <inheritdoc />
	public IReadOnlyList<string> GetAvailableControls() =>
		_controlToValidator.Keys.ToList();

	/// <inheritdoc />
	public IReadOnlyList<string> GetControlsForCriterion(TrustServicesCriterion criterion) =>
		_criterionToControls.TryGetValue(criterion, out var controls)
			? [.. controls]
			: [];

	private void BuildControlMappings()
	{
		foreach (var validator in _validators)
		{
			foreach (var controlId in validator.SupportedControls)
			{
				_controlToValidator[controlId] = validator;
			}

			foreach (var criterion in validator.SupportedCriteria)
			{
				if (!_criterionToControls.TryGetValue(criterion, out var controls))
				{
					controls = [];
					_criterionToControls[criterion] = controls;
				}

				controls.AddRange(validator.SupportedControls);
			}
		}
	}
}
