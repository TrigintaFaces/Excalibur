// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Options.Middleware;

/// <summary>
/// Configuration options for validation middleware.
/// </summary>
public sealed class ValidationOptions
{
	/// <summary>
	/// Gets or sets a value indicating whether validation is enabled.
	/// </summary>
	/// <value> Default is true. </value>
	public bool Enabled { get; set; } = true;

	/// <summary>
	/// Gets or sets a value indicating whether to use Data Annotations for validation.
	/// </summary>
	/// <value> Default is true. </value>
	public bool UseDataAnnotations { get; set; } = true;

	/// <summary>
	/// Gets or sets a value indicating whether to use custom validation via IValidationService.
	/// </summary>
	/// <value> Default is true. </value>
	public bool UseCustomValidation { get; set; } = true;

	/// <summary>
	/// Gets or sets a value indicating whether to stop validation on the first error.
	/// </summary>
	/// <value> Default is false (collect all errors). </value>
	public bool StopOnFirstError { get; set; }

	/// <summary>
	/// Gets or sets message types that bypass validation.
	/// </summary>
	/// <value>The current <see cref="BypassValidationForTypes"/> value.</value>
	public string[]? BypassValidationForTypes { get; set; }
}
