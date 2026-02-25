// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Abstractions.Validation;

/// <summary>
/// Represents the result of a validation operation on a message.
/// </summary>
/// <remarks>
/// Validation results provide structured feedback about the validity of a message, including specific error details when validation fails.
/// The result is used by the validation middleware to determine whether message processing should continue or be rejected. Key features include:
/// <list type="bullet">
/// <item> Boolean validation status indicator </item>
/// <item> Collection of detailed error information </item>
/// <item> Factory methods for creating success and failure results </item>
/// <item> Immutable result state for thread safety </item>
/// </list>
/// Implementations should provide meaningful error messages that help developers understand validation failures.
/// </remarks>
public interface IValidationResult
{
	/// <summary>
	/// Gets the collection of validation errors, if any.
	/// </summary>
	/// <remarks>
	/// Contains detailed information about validation failures. Error objects can be strings, ValidationError instances, or any other type
	/// that provides meaningful error context. Empty when IsValid is true.
	/// </remarks>
	IReadOnlyCollection<object> Errors { get; }

	/// <summary>
	/// Gets or sets a value indicating whether the validation passed.
	/// </summary>
	/// <remarks>
	/// When true, the message passed all validation rules and processing can continue. When false, the Errors collection should contain
	/// details about the validation failures.
	/// </remarks>
	bool IsValid { get; }

	/// <summary>
	/// Creates a failed validation result with the specified errors.
	/// </summary>
	/// <param name="errors"> The validation errors that occurred. </param>
	/// <returns> A validation result indicating failure with the provided errors. </returns>
	/// <remarks>
	/// Factory method for creating validation failure results. Implementations should ensure the returned result has IsValid set to false
	/// and the Errors collection populated with the provided errors.
	/// </remarks>
	static abstract IValidationResult Failed(params object[] errors);

	/// <summary>
	/// Creates a successful validation result.
	/// </summary>
	/// <returns> A validation result indicating success with no errors. </returns>
	/// <remarks>
	/// Factory method for creating validation success results. Implementations should ensure the returned result has IsValid set to true
	/// and an empty Errors collection.
	/// </remarks>
	static abstract IValidationResult Success();
}
