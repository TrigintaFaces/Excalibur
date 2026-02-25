// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Validation;

/// <summary>
/// Validation levels for profile-specific validation.
/// </summary>
public enum ValidationLevel
{
	/// <summary>
	/// No validation is performed.
	/// </summary>
	None = 0,

	/// <summary>
	/// Basic validation including null checks only.
	/// </summary>
	Basic = 1,

	/// <summary>
	/// Standard validation including IValidate and DataAnnotations.
	/// </summary>
	Standard = 2,

	/// <summary>
	/// Strict validation including custom rules and additional constraints.
	/// </summary>
	Strict = 3,
}
