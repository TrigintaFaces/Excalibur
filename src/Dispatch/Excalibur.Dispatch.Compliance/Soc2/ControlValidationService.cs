// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Compliance;

/// <summary>
/// Default implementation of <see cref="IControlValidationService"/>.
/// </summary>
public sealed class ControlValidationService : IControlValidationService
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
				IsEffective = false,
				EffectivenessScore = 0,
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
				ExceptionsFound = 0,
				Outcome = TestOutcome.ControlFailure,
				Exceptions = [new TestException
				{
					ItemId = "N/A",
					Description = $"No validator registered for control: {controlId}",
					Severity = GapSeverity.Critical,
					OccurredAt = DateTimeOffset.UtcNow
				}]
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
			? controls
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
