// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Data.ElasticSearch.Security;

/// <summary>
/// Represents the severity level of a security incident.
/// </summary>
public enum SecurityIncidentSeverity
{
	/// <summary>
	/// Low severity incident with minimal impact.
	/// </summary>
	Low = 0,

	/// <summary>
	/// Medium severity incident with moderate impact.
	/// </summary>
	Medium = 1,

	/// <summary>
	/// High severity incident with significant impact.
	/// </summary>
	High = 2,

	/// <summary>
	/// Critical severity incident requiring immediate attention.
	/// </summary>
	Critical = 3,
}
