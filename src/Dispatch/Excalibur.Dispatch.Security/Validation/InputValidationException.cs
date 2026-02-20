// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Security;

/// <summary>
/// Exception thrown when input validation fails.
/// </summary>
public sealed class InputValidationException : Exception
{
	/// <summary>
	/// Initializes a new instance of the <see cref="InputValidationException"/> class with a specified error message and validation errors.
	/// </summary>
	/// <param name="message">The exception message.</param>
	/// <param name="errors">The validation errors that occurred.</param>
	public InputValidationException(string message, IEnumerable<string> errors)
		: base(message) =>
		ValidationErrors = errors?.ToList() ?? [];

	/// <summary>
	/// Initializes a new instance of the <see cref="InputValidationException"/> class with a default error message.
	/// </summary>
	public InputValidationException()
			: this(Resources.InputValidationException_ValidationFailed, [])
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="InputValidationException"/> class with a specified error message.
	/// </summary>
	/// <param name="message">The exception message.</param>
	public InputValidationException(string message)
		: this(message, [])
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="InputValidationException"/> class with a specified error message and inner exception.
	/// </summary>
	/// <param name="message">The exception message.</param>
	/// <param name="innerException">The inner exception that caused this exception.</param>
	public InputValidationException(string message, Exception innerException)
		: base(message, innerException) =>
		ValidationErrors = [];

	/// <summary>
	/// Gets the list of validation errors that caused this exception.
	/// </summary>
	/// <value>
	/// The list of validation errors that caused this exception, or an empty list if no errors occurred.
	/// </value>
	public IReadOnlyList<string> ValidationErrors { get; }
}
