// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Data.ElasticSearch.Projections;

/// <summary>
/// Defines schema compatibility levels.
/// </summary>
public enum CompatibilityLevel
{
	/// <summary>
	/// Fully backwards compatible.
	/// </summary>
	Full = 0,

	/// <summary>
	/// Backwards compatible with warnings.
	/// </summary>
	Partial = 1,

	/// <summary>
	/// Not backwards compatible.
	/// </summary>
	None = 2,
}
