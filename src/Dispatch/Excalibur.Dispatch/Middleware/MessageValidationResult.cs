// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Middleware;

/// <summary>
/// Result of message validation.
/// </summary>
/// <remarks> Creates a new message validation result. </remarks>
public sealed class MessageValidationResult(bool isValid, IEnumerable<ValidationError> errors)
{
	/// <summary>
	/// Gets a value indicating whether validation passed.
	/// </summary>
	/// <value>The current <see cref="IsValid"/> value.</value>
	public bool IsValid { get; } = isValid;

	/// <summary>
	/// Gets the validation errors, if any.
	/// </summary>
	/// <value>
	/// The validation errors, if any.
	/// </value>
	public IReadOnlyList<ValidationError> Errors { get; } = errors?.ToList().AsReadOnly() ?? new List<ValidationError>().AsReadOnly();

	/// <summary>
	/// Creates a successful validation result.
	/// </summary>
	public static MessageValidationResult Success() => new(isValid: true, []);

	/// <summary>
	/// Creates a failed validation result with errors.
	/// </summary>
	public static MessageValidationResult Failure(params ValidationError[] errors) => new(isValid: false, errors);
}
