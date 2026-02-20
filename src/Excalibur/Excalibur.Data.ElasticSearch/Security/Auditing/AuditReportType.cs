// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Data.ElasticSearch.Security;

/// <summary>
/// Defines audit report types.
/// </summary>
public enum AuditReportType
{
	/// <summary>
	/// Comprehensive audit report including all audit events and security activities.
	/// </summary>
	Comprehensive = 0,

	/// <summary>
	/// Authentication-focused audit report covering login events and authentication failures.
	/// </summary>
	Authentication = 1,

	/// <summary>
	/// Data access audit report covering all data access operations and permissions.
	/// </summary>
	DataAccess = 2,

	/// <summary>
	/// Configuration changes audit report covering system and security configuration modifications.
	/// </summary>
	ConfigurationChanges = 3,

	/// <summary>
	/// Security incidents audit report covering detected threats and security violations.
	/// </summary>
	SecurityIncidents = 4,

	/// <summary>
	/// Compliance audit report covering regulatory compliance and governance requirements.
	/// </summary>
	Compliance = 5,
}
