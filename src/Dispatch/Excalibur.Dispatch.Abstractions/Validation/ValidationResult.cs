// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Abstractions.Validation;

/// <summary>
/// Represents the result of a validation operation. This is the canonical ValidationResult
/// consolidating versions from Validation, Transport, Subscription, and Patterns.
/// </summary>
public sealed class ValidationResult
{
	/// <summary>
	/// Gets or sets a value indicating whether the validation was successful.
	/// </summary>
	/// <value><see langword="true"/> when the validation succeeded; otherwise, <see langword="false"/>.</value>
	public bool IsValid { get; init; }

	/// <summary>
	/// Gets the validation errors.
	/// </summary>
	/// <value>The collection of validation errors.</value>
	public IReadOnlyList<ValidationError> Errors { get; init; } = [];

	/// <summary>
	/// Gets the validation warnings (non-fatal issues).
	/// </summary>
	/// <value>The collection of warning messages.</value>
	public IReadOnlyList<string> Warnings { get; init; } = [];

	/// <summary>
	/// Creates a successful validation result.
	/// </summary>
	/// <returns>A successful validation result.</returns>
	public static ValidationResult Success() => new() { IsValid = true };

	/// <summary>
	/// Creates a successful validation result with warnings.
	/// </summary>
	/// <param name="warnings">The warning messages.</param>
	/// <returns>A successful validation result with warnings.</returns>
	public static ValidationResult SuccessWithWarnings(params string[] warnings) =>
		new() { IsValid = true, Warnings = warnings };

	/// <summary>
	/// Creates a failed validation result with the specified errors.
	/// </summary>
	/// <param name="errors">The validation errors.</param>
	/// <returns>A failed validation result.</returns>
	public static ValidationResult Failure(params ValidationError[] errors) =>
		new() { IsValid = false, Errors = errors };

	/// <summary>
	/// Creates a failed validation result with the specified error messages.
	/// </summary>
	/// <param name="errorMessages">The error messages.</param>
	/// <returns>A failed validation result.</returns>
	public static ValidationResult Failure(params string[] errorMessages)
	{
		var list = new List<ValidationError>(errorMessages?.Length ?? 0);
		if (errorMessages is not null)
		{
			for (var i = 0; i < errorMessages.Length; i++)
			{
				list.Add(new ValidationError(errorMessages[i]));
			}
		}

		return new ValidationResult { IsValid = false, Errors = list.Count == 0 ? [] : list.ToArray() };
	}

	/// <summary>
	/// Creates a failed validation result with errors and warnings.
	/// </summary>
	/// <param name="errors">The validation errors.</param>
	/// <param name="warnings">The warning messages.</param>
	/// <returns>A failed validation result with warnings.</returns>
	public static ValidationResult FailureWithWarnings(ValidationError[] errors, string[] warnings) =>
		new() { IsValid = false, Errors = errors, Warnings = warnings };
}
