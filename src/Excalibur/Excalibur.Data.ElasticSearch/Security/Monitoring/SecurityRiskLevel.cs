// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Data.ElasticSearch.Security;

/// <summary>
/// Defines security risk levels.
/// </summary>
public enum SecurityRiskLevel
{
	/// <summary>
	/// Low security risk with minimal threat potential.
	/// </summary>
	Low = 0,

	/// <summary>
	/// Medium security risk requiring monitoring.
	/// </summary>
	Medium = 1,

	/// <summary>
	/// High security risk requiring immediate attention.
	/// </summary>
	High = 2,

	/// <summary>
	/// Critical security risk requiring emergency response.
	/// </summary>
	Critical = 3,
}
