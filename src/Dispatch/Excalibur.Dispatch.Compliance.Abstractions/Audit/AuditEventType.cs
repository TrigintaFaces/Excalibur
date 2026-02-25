// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Compliance;

/// <summary>
/// Categorizes audit events for filtering and reporting.
/// </summary>
/// <remarks> Audit events are categorized to support SOC2 reporting and security monitoring. </remarks>
public enum AuditEventType
{
	/// <summary>
	/// General system events.
	/// </summary>
	System = 0,

	/// <summary>
	/// Authentication events (login, logout, MFA).
	/// </summary>
	Authentication = 1,

	/// <summary>
	/// Authorization events (permission checks, access grants).
	/// </summary>
	Authorization = 2,

	/// <summary>
	/// Data access events (read, query).
	/// </summary>
	DataAccess = 3,

	/// <summary>
	/// Data modification events (create, update, delete).
	/// </summary>
	DataModification = 4,

	/// <summary>
	/// Configuration change events.
	/// </summary>
	ConfigurationChange = 5,

	/// <summary>
	/// Security events (key rotation, encryption operations).
	/// </summary>
	Security = 6,

	/// <summary>
	/// Compliance events (data export, erasure requests).
	/// </summary>
	Compliance = 7,

	/// <summary>
	/// Administrative actions.
	/// </summary>
	Administrative = 8,

	/// <summary>
	/// Integration events (API calls, external system interactions).
	/// </summary>
	Integration = 9
}

/// <summary>
/// Represents the outcome of an audited operation.
/// </summary>
public enum AuditOutcome
{
	/// <summary>
	/// The operation completed successfully.
	/// </summary>
	Success = 0,

	/// <summary>
	/// The operation failed.
	/// </summary>
	Failure = 1,

	/// <summary>
	/// The operation was denied due to authorization.
	/// </summary>
	Denied = 2,

	/// <summary>
	/// The operation resulted in an error.
	/// </summary>
	Error = 3,

	/// <summary>
	/// The operation is pending or in progress.
	/// </summary>
	Pending = 4
}
