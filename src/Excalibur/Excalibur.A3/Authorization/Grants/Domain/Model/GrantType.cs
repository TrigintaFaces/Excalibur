// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.A3.Authorization.Grants;

/// <summary>
/// Provides predefined constants for grant types.
/// </summary>
public static class GrantType
{
	/// <summary>
	/// Represents a grant type associated with a specific activity.
	/// </summary>
	public static readonly string Activity = nameof(Activity);

	/// <summary>
	/// Represents a grant type associated with a group of activities.
	/// </summary>
	public static readonly string ActivityGroup = nameof(ActivityGroup);
}
