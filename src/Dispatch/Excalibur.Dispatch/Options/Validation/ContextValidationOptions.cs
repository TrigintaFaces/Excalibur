// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Dispatch.Validation.Context;

namespace Excalibur.Dispatch.Options.Validation;

/// <summary>
/// Configuration options for context validation middleware.
/// </summary>
public sealed class ContextValidationOptions
{
	/// <summary>
	/// Gets or sets the validation mode.
	/// </summary>
	/// <remarks> Strict mode rejects messages with invalid context. Lenient mode logs warnings but continues processing. </remarks>
	/// <value>The current <see cref="Mode"/> value.</value>
	public ValidationMode Mode { get; set; } = ValidationMode.Lenient;

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
	/// Gets the list of required field names.
	/// </summary>
	/// <remarks> These fields must be present and non-null for validation to pass. </remarks>
	/// <value>The current <see cref="RequiredFields"/> value.</value>
	public List<string> RequiredFields { get; init; } = ["MessageId", "MessageType"];

	/// <summary>
	/// Gets the list of fields that require special validation.
	/// </summary>
	/// <remarks> Maps field names to validation rules. </remarks>
	/// <value>
	/// The list of fields that require special validation.
	/// </value>
	public IDictionary<string, FieldValidationRule> FieldValidationRules { get; init; } = new Dictionary<string, FieldValidationRule>(StringComparer.Ordinal);

	/// <summary>
	/// Gets or sets a value indicating whether to enable detailed diagnostics.
	/// </summary>
	/// <remarks> When enabled, provides detailed information about validation failures. </remarks>
	/// <value>The current <see cref="EnableDetailedDiagnostics"/> value.</value>
	public bool EnableDetailedDiagnostics { get; set; } = true;

	/// <summary>
	/// Gets or sets the maximum allowed age for a message context.
	/// </summary>
	/// <remarks> Messages older than this are considered potentially corrupted. </remarks>
	/// <value>
	/// The maximum allowed age for a message context.
	/// </value>
	public TimeSpan? MaxMessageAge { get; set; } = TimeSpan.FromDays(1);

	/// <summary>
	/// Gets or sets a value indicating whether to validate correlation chain integrity.
	/// </summary>
	/// <value>The current <see cref="ValidateCorrelationChain"/> value.</value>
	public bool ValidateCorrelationChain { get; set; } = true;

	/// <summary>
	/// Gets custom validation extensions.
	/// </summary>
	/// <remarks> Allows adding custom validation logic through configuration. </remarks>
	/// <value>The current <see cref="CustomValidatorTypes"/> value.</value>
	public List<Type> CustomValidatorTypes { get; init; } = [];
}
