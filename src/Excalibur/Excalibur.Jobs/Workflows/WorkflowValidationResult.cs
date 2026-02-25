// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Jobs.Workflows;

/// <summary>
/// Represents the result of a workflow validation.
/// </summary>
public sealed class WorkflowValidationResult
{
	/// <summary>
	/// Gets or sets a value indicating whether the workflow is valid.
	/// </summary>
	/// <value>
	/// A value indicating whether the workflow is valid.
	/// </value>
	public bool IsValid { get; init; }

	/// <summary>
	/// Gets the error message if the workflow is invalid.
	/// </summary>
	/// <value>
	/// The error message if the workflow is invalid.
	/// </value>
	public string? ErrorMessage { get; init; }

	/// <summary>
	/// Gets the validation errors if the workflow is invalid.
	/// </summary>
	/// <value>
	/// The validation errors if the workflow is invalid.
	/// </value>
	public IReadOnlyCollection<string>? ValidationErrors { get; init; }

	/// <summary>
	/// Creates a successful validation result.
	/// </summary>
	/// <returns> A successful validation result. </returns>
	public static WorkflowValidationResult Success() => new() { IsValid = true };

	/// <summary>
	/// Creates a failed validation result.
	/// </summary>
	/// <param name="errorMessage"> The error message. </param>
	/// <returns> A failed validation result. </returns>
	public static WorkflowValidationResult Failure(string errorMessage) => new() { IsValid = false, ErrorMessage = errorMessage };

	/// <summary>
	/// Creates a failed validation result with multiple errors.
	/// </summary>
	/// <param name="errors"> The validation errors. </param>
	/// <returns> A failed validation result. </returns>
	public static WorkflowValidationResult Failure(params string[] errors) => new()
	{
		IsValid = false,
		ValidationErrors = [.. errors],
		ErrorMessage = string.Join("; ", errors),
	};
}
