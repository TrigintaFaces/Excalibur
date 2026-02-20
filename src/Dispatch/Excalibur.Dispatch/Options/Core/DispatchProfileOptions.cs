// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Options.Core;

/// <summary>
/// Configuration options for dispatch profiles.
/// </summary>
public sealed class DispatchProfileOptions
{
	/// <summary>
	/// Gets or sets the name of the active profile.
	/// </summary>
	/// <value> The active dispatch profile identifier. </value>
	public string ProfileName { get; set; } = string.Empty;
}
