// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Data.ElasticSearch.Security;

/// <summary>
/// Security event types for audit logging.
/// </summary>
public enum SecurityEventType
{
	/// <summary>
	/// Authentication-related security events including login, logout, and authentication failures.
	/// </summary>
	Authentication = 0,

	/// <summary>
	/// Data access security events including read, write, and modification operations.
	/// </summary>
	DataAccess = 1,

	/// <summary>
	/// Configuration change security events including system and security setting modifications.
	/// </summary>
	ConfigurationChange = 2,

	/// <summary>
	/// Security incident events including detected threats and security violations.
	/// </summary>
	SecurityIncident = 3,

	/// <summary>
	/// Access control security events including permission changes and access violations.
	/// </summary>
	AccessControl = 4,

	/// <summary>
	/// Other security events that do not fit into specific categories.
	/// </summary>
	Other = 5,
}
