// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Middleware;

/// <summary>
/// Represents a validation error for a specific property or field.
/// </summary>
/// <remarks> Creates a new validation error. </remarks>
public sealed class ValidationError(string propertyName, string errorMessage)
{
	/// <summary>
	/// Gets the name of the property that failed validation.
	/// </summary>
	/// <value>
	/// The name of the property that failed validation.
	/// </value>
	public string PropertyName { get; } = propertyName ?? throw new ArgumentNullException(nameof(propertyName));

	/// <summary>
	/// Gets the validation error message.
	/// </summary>
	/// <value>
	/// The validation error message.
	/// </value>
	public string ErrorMessage { get; } = errorMessage ?? throw new ArgumentNullException(nameof(errorMessage));
}
