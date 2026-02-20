// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Abstractions.Validation;

/// <summary>
/// Represents a validation error.
/// </summary>
public sealed class ValidationError
{
	/// <summary>
	/// Initializes a new instance of the <see cref="ValidationError" /> class.
	/// </summary>
	/// <param name="message"> The error message. </param>
	public ValidationError(string message) => Message = message ?? throw new ArgumentNullException(nameof(message));

	/// <summary>
	/// Initializes a new instance of the <see cref="ValidationError" /> class.
	/// </summary>
	/// <param name="propertyName"> The property name. </param>
	/// <param name="message"> The error message. </param>
	public ValidationError(string propertyName, string message)
	{
		PropertyName = propertyName;
		Message = message ?? throw new ArgumentNullException(nameof(message));
	}

	/// <summary>
	/// Gets the property name associated with the error.
	/// </summary>
	/// <value> The name of the property in error, when applicable. </value>
	public string? PropertyName { get; }

	/// <summary>
	/// Gets the error message.
	/// </summary>
	/// <value> The user-facing error message. </value>
	public string Message { get; }

	/// <summary>
	/// Gets or sets the error code.
	/// </summary>
	/// <value> The error code used for categorization. </value>
	public string? ErrorCode { get; set; }

	/// <summary>
	/// Gets additional metadata for the error.
	/// </summary>
	/// <value> Optional metadata associated with the error. </value>
	public IDictionary<string, object>? Metadata { get; init; }
}
