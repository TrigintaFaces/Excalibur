// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Transport.Kafka;

/// <summary>
/// Represents the result of validating a JSON Schema structure.
/// </summary>
public sealed class SchemaValidationResult
{
	private static readonly SchemaValidationResult SuccessInstance = new(true, []);

	private SchemaValidationResult(bool isValid, IReadOnlyList<string> errors)
	{
		IsValid = isValid;
		Errors = errors;
	}

	/// <summary>
	/// Gets a successful validation result.
	/// </summary>
	public static SchemaValidationResult Success => SuccessInstance;

	/// <summary>
	/// Gets a value indicating whether the schema is valid.
	/// </summary>
	public bool IsValid { get; }

	/// <summary>
	/// Gets the validation error messages.
	/// </summary>
	public IReadOnlyList<string> Errors { get; }

	/// <summary>
	/// Creates a failed validation result with the specified errors.
	/// </summary>
	/// <param name="errors">The validation error messages.</param>
	/// <returns>A failed validation result.</returns>
	public static SchemaValidationResult Failure(params string[] errors)
	{
		ArgumentNullException.ThrowIfNull(errors);
		return new SchemaValidationResult(false, errors);
	}

	/// <summary>
	/// Creates a failed validation result with the specified errors.
	/// </summary>
	/// <param name="errors">The validation error messages.</param>
	/// <returns>A failed validation result.</returns>
	public static SchemaValidationResult Failure(IReadOnlyList<string> errors)
	{
		ArgumentNullException.ThrowIfNull(errors);
		return new SchemaValidationResult(false, errors);
	}
}
