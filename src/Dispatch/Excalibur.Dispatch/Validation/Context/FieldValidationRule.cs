// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Validation.Context;

/// <summary>
/// Defines validation rules for a specific field.
/// </summary>
public sealed class FieldValidationRule
{
	/// <summary>
	/// Gets or sets a value indicating whether the field is required.
	/// </summary>
	/// <value> The current <see cref="Required" /> value. </value>
	public bool Required { get; set; }

	/// <summary>
	/// Gets or sets the expected type of the field value.
	/// </summary>
	/// <value> The current <see cref="ExpectedType" /> value. </value>
	public Type? ExpectedType { get; set; }

	/// <summary>
	/// Gets or sets a regular expression pattern for string fields.
	/// </summary>
	/// <value> The current <see cref="Pattern" /> value. </value>
	public string? Pattern { get; set; }

	/// <summary>
	/// Gets or sets the minimum length for string fields.
	/// </summary>
	/// <value> The current <see cref="MinLength" /> value. </value>
	public int? MinLength { get; set; }

	/// <summary>
	/// Gets or sets the maximum length for string fields.
	/// </summary>
	/// <value> The current <see cref="MaxLength" /> value. </value>
	public int? MaxLength { get; set; }

	/// <summary>
	/// Gets or sets custom validation logic.
	/// </summary>
	/// <value> The current <see cref="CustomValidator" /> value. </value>
	public Func<object?, bool>? CustomValidator { get; set; }

	/// <summary>
	/// Gets or sets the error message to use when validation fails.
	/// </summary>
	/// <value> The current <see cref="ErrorMessage" /> value. </value>
	public string? ErrorMessage { get; set; }
}
