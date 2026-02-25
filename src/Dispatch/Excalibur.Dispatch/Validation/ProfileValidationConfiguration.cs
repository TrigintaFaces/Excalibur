// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Validation;

/// <summary>
/// Configuration for a specific profile's validation rules.
/// </summary>
public sealed class ProfileValidationConfiguration
{
	/// <summary>
	/// Gets or sets the validation level for this profile.
	/// </summary>
	/// <value> The validation strictness applied to the profile. </value>
	public ValidationLevel ValidationLevel { get; set; } = ValidationLevel.Standard;

	/// <summary>
	/// Gets or sets the maximum allowed message size in bytes.
	/// </summary>
	/// <value> The maximum payload size permitted for the profile. </value>
	public int MaxMessageSize { get; set; } = 1_048_576; // 1MB default

	/// <summary>
	/// Gets or sets the list of required fields for this profile.
	/// </summary>
	/// <value> The collection of fields that must be present. </value>
	public ICollection<string> RequiredFields { get; set; } = [];

	/// <summary>
	/// Gets or sets the list of forbidden fields for this profile.
	/// </summary>
	/// <value> The collection of fields that are disallowed. </value>
	public ICollection<string> ForbiddenFields { get; set; } = [];

	/// <summary>
	/// Gets or sets the dictionary of field constraints.
	/// </summary>
	/// <value> The constraint definitions keyed by field name. </value>
	public IDictionary<string, object> FieldConstraints { get; set; } = new Dictionary<string, object>(StringComparer.Ordinal);
}
