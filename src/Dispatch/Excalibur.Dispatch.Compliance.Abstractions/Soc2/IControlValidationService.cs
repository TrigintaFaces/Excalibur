// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Compliance;

/// <summary>
/// Service for validating individual SOC 2 controls.
/// </summary>
public interface IControlValidationService
{
	/// <summary>
	/// Validates a specific control.
	/// </summary>
	/// <param name="controlId">The control identifier to validate.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>The validation result.</returns>
	Task<ControlValidationResult> ValidateControlAsync(
		string controlId,
		CancellationToken cancellationToken);

	/// <summary>
	/// Validates all controls for a criterion.
	/// </summary>
	/// <param name="criterion">The criterion to validate controls for.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>Validation results for all controls.</returns>
	Task<IReadOnlyList<ControlValidationResult>> ValidateCriterionAsync(
		TrustServicesCriterion criterion,
		CancellationToken cancellationToken);

	/// <summary>
	/// Runs automated control tests.
	/// </summary>
	/// <param name="controlId">The control identifier to test.</param>
	/// <param name="parameters">Test parameters.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>The test result.</returns>
	Task<ControlTestResult> RunControlTestAsync(
		string controlId,
		ControlTestParameters parameters,
		CancellationToken cancellationToken);

	/// <summary>
	/// Gets all available control identifiers.
	/// </summary>
	/// <returns>All registered control identifiers.</returns>
	IReadOnlyList<string> GetAvailableControls();

	/// <summary>
	/// Gets controls for a specific criterion.
	/// </summary>
	/// <param name="criterion">The criterion to get controls for.</param>
	/// <returns>Control identifiers mapped to the criterion.</returns>
	IReadOnlyList<string> GetControlsForCriterion(TrustServicesCriterion criterion);
}
