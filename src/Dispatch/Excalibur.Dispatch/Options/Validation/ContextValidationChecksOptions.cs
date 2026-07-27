// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Options.Validation;

/// <summary>
/// Toggles controlling which context validation checks are performed.
/// </summary>
public sealed class ContextValidationChecksOptions
{
	/// <summary>
	/// Gets or sets a value indicating whether to validate required fields.
	/// </summary>
	/// <value>The current <see cref="ValidateRequiredFields"/> value.</value>
	public bool ValidateRequiredFields { get; set; } = true;

	/// <summary>
	/// Gets or sets a value indicating whether to validate multi-tenancy fields.
	/// </summary>
	/// <value>The current <see cref="ValidateMultiTenancy"/> value.</value>
	public bool ValidateMultiTenancy { get; set; } = true;

	/// <summary>
	/// Gets or sets a value indicating whether to validate authentication fields.
	/// </summary>
	/// <value>The current <see cref="ValidateAuthentication"/> value.</value>
	public bool ValidateAuthentication { get; set; } = true;

	/// <summary>
	/// Gets or sets a value indicating whether to validate distributed tracing context.
	/// </summary>
	/// <value>The current <see cref="ValidateTracing"/> value.</value>
	public bool ValidateTracing { get; set; } = true;

	/// <summary>
	/// Gets or sets a value indicating whether to validate message versioning.
	/// </summary>
	/// <value>The current <see cref="ValidateVersioning"/> value.</value>
	public bool ValidateVersioning { get; set; } = true;

	/// <summary>
	/// Gets or sets a value indicating whether to validate collection integrity.
	/// </summary>
	/// <value>The current <see cref="ValidateCollections"/> value.</value>
	public bool ValidateCollections { get; set; } = true;

	/// <summary>
	/// Gets or sets a value indicating whether to validate correlation chain integrity.
	/// </summary>
	/// <value>The current <see cref="ValidateCorrelationChain"/> value.</value>
	public bool ValidateCorrelationChain { get; set; } = true;
}
