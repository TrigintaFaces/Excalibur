// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Data.ElasticSearch.Security;

/// <summary>
/// Defines threat types.
/// </summary>
public enum ThreatType
{
	/// <summary>
	/// No threat detected.
	/// </summary>
	None = 0,

	/// <summary>
	/// Malicious software threat.
	/// </summary>
	Malware = 1,

	/// <summary>
	/// Unauthorized data extraction threat.
	/// </summary>
	DataExfiltration = 2,

	/// <summary>
	/// Unauthorized system access threat.
	/// </summary>
	UnauthorizedAccess = 3,

	/// <summary>
	/// Service availability disruption threat.
	/// </summary>
	DenialOfService = 4,

	/// <summary>
	/// Privilege escalation threat.
	/// </summary>
	PrivilegeEscalation = 5,

	/// <summary>
	/// Other unspecified threat type.
	/// </summary>
	Other = 6,
}
