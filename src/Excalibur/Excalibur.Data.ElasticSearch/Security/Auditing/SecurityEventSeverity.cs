// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Data.ElasticSearch.Security;

/// <summary>
/// Defines security event severity levels.
/// </summary>
public enum SecurityEventSeverity
{
	/// <summary>
	/// Low severity security event with minimal impact.
	/// </summary>
	Low = 0,

	/// <summary>
	/// Medium severity security event requiring monitoring.
	/// </summary>
	Medium = 1,

	/// <summary>
	/// High severity security event requiring immediate attention.
	/// </summary>
	High = 2,

	/// <summary>
	/// Critical severity security event requiring urgent response.
	/// </summary>
	Critical = 3,
}
