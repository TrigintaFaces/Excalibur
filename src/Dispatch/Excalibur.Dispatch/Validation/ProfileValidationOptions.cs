// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Validation;

/// <summary>
/// Configuration options for profile-specific validation.
/// </summary>
public sealed class ProfileValidationOptions
{
	/// <summary>
	/// Gets or sets the default validation profile name.
	/// </summary>
	/// <value> The profile name applied when no override is specified. </value>
	public string DefaultProfile { get; set; } = "default";

	/// <summary>
	/// Gets or sets the default validation level.
	/// </summary>
	/// <value> The validation level used when a profile does not define one. </value>
	public ValidationLevel DefaultValidationLevel { get; set; } = ValidationLevel.Standard;

	/// <summary>
	/// Gets or sets the dictionary of profile-specific configurations.
	/// </summary>
	/// <value> The map of profile names to their validation configuration. </value>
	public IDictionary<string, ProfileValidationConfiguration> Profiles { get; set; } =
		new Dictionary<string, ProfileValidationConfiguration>(StringComparer.Ordinal);
}
