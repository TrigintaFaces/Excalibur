// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0


namespace Excalibur.Compliance;

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
	/// <remarks>
	/// <para>
	/// <b>This operation is total: it has no precondition on <paramref name="controlId"/>.</b> An
	/// identifier outside <see cref="SupportedControls"/> is an ordinary input, not a caller error,
	/// and MUST produce a result saying the control was not assessed here -- never a thrown
	/// exception, and never a verdict about a control this validator did not examine.
	/// </para>
	/// <para>
	/// This was already the contract every implementation and caller depended on, and it was written
	/// down nowhere. An implementor reading only the signature would have been entitled to throw
	/// <see cref="ArgumentException"/>, which breaks the dispatching service -- it forwards whatever
	/// identifier it is given -- and the shipped conformance kit, which calls this method with an
	/// unsupported identifier and requires a result.
	/// </para>
	/// <para>
	/// The only exception an implementation may propagate is
	/// <see cref="OperationCanceledException"/>, when <paramref name="cancellationToken"/> is
	/// signalled.
	/// </para>
	/// </remarks>
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
