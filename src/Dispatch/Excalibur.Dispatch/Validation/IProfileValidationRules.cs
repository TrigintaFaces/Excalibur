// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Validation;

/// <summary>
/// Interface for profile-specific validation rules.
/// </summary>
public interface IProfileValidationRules
{
	/// <summary>
	/// Gets the name of the validation profile.
	/// </summary>
	/// <value> The unique profile identifier. </value>
	string ProfileName { get; }

	/// <summary>
	/// Gets the validation level for this profile.
	/// </summary>
	/// <value> The validation strictness applied to the profile. </value>
	ValidationLevel ValidationLevel { get; }

	/// <summary>
	/// Gets the maximum allowed message size in bytes.
	/// </summary>
	/// <value> The maximum payload size permitted by the profile. </value>
	int MaxMessageSize { get; }

	/// <summary>
	/// Gets the list of fields that are required for this profile.
	/// </summary>
	/// <value> The collection of field names that must be present. </value>
	IReadOnlyList<string> RequiredFields { get; }

	/// <summary>
	/// Gets the list of custom validators to apply for this profile.
	/// </summary>
	/// <value> The collection of custom validators executed for the profile. </value>
	IReadOnlyList<ICustomValidator> CustomValidators { get; }

	/// <summary>
	/// Gets the list of field constraints to enforce for this profile.
	/// </summary>
	/// <value> The collection of constraints applied to individual fields. </value>
	IReadOnlyList<IFieldConstraint> FieldConstraints { get; }
}
