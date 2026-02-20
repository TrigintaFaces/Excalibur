// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Resilience.Polly;

/// <summary>
/// Degradation levels from normal to severely degraded.
/// </summary>
public enum DegradationLevel
{
	/// <summary>
	/// Normal operation - all features enabled.
	/// </summary>
	Normal = 0,

	/// <summary>
	/// Minor degradation - non-essential features disabled.
	/// </summary>
	Minor = 1,

	/// <summary>
	/// Moderate degradation - some core features limited.
	/// </summary>
	Moderate = 2,

	/// <summary>
	/// Major degradation - only critical features enabled.
	/// </summary>
	Major = 3,

	/// <summary>
	/// Severe degradation - minimal functionality.
	/// </summary>
	Severe = 4,

	/// <summary>
	/// Emergency mode - absolute minimum operations.
	/// </summary>
	Emergency = 5,
}
