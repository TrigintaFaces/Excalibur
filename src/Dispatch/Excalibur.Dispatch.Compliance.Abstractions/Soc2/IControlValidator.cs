// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Compliance;

/// <summary>
/// Interface for individual control validators.
/// Implementations validate specific controls (e.g., encryption, audit logging).
/// </summary>
public interface IControlValidator
{
	/// <summary>
	/// Gets the control identifiers this validator handles.
	/// </summary>
	IReadOnlyList<string> SupportedControls { get; }

	/// <summary>
	/// Gets the Trust Services criteria this validator covers.
	/// </summary>
	IReadOnlyList<TrustServicesCriterion> SupportedCriteria { get; }

	/// <summary>
	/// Validates the specified control.
	/// </summary>
	/// <param name="controlId">The control identifier to validate.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>The validation result.</returns>
	Task<ControlValidationResult> ValidateAsync(
		string controlId,
		CancellationToken cancellationToken);

	/// <summary>
	/// Runs a control test with the specified parameters.
	/// </summary>
	/// <param name="controlId">The control identifier to test.</param>
	/// <param name="parameters">Test parameters.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>The test result.</returns>
	Task<ControlTestResult> RunTestAsync(
		string controlId,
		ControlTestParameters parameters,
		CancellationToken cancellationToken);

	/// <summary>
	/// Gets the control description for the specified control.
	/// </summary>
	/// <param name="controlId">The control identifier.</param>
	/// <returns>The control description, or null if not found.</returns>
	ControlDescription? GetControlDescription(string controlId);
}
