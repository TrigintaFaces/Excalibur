// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.CloudNative;

/// <summary>
/// Impact level of an adaptation.
/// </summary>
public enum AdaptationImpact
{
	/// <summary>
	/// Small adjustment with minimal impact on behavior.
	/// </summary>
	Minor = 0,

	/// <summary>
	/// Significant change with moderate impact on behavior.
	/// </summary>
	Moderate = 1,

	/// <summary>
	/// Large impact on behavior requiring careful consideration.
	/// </summary>
	Major = 2,
}
