// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.AuditLogging;

/// <summary>
/// Defines the access levels for audit log operations.
/// </summary>
/// <remarks>
/// <para>
/// Audit log access is role-based with the following hierarchy:
/// - None: No audit log access
/// - Developer: No audit log access (development role)
/// - SecurityAnalyst: Read security events only
/// - ComplianceOfficer: Read all events (compliance reporting)
/// - Administrator: Full access including export
/// </para>
/// <para>
/// Roles are ordered by increasing privilege level for comparison operations.
/// </para>
/// </remarks>
public enum AuditLogRole
{
	/// <summary>
	/// No audit log access.
	/// </summary>
	None = 0,

	/// <summary>
	/// Developer role with no audit log access.
	/// </summary>
	/// <remarks>
	/// Developers should not have access to audit logs in production
	/// to maintain segregation of duties.
	/// </remarks>
	Developer = 1,

	/// <summary>
	/// Security analyst with read access to security-related events only.
	/// </summary>
	/// <remarks>
	/// Can view: Authentication, Authorization, Security event types.
	/// Cannot view: DataAccess, DataModification, Compliance events.
	/// </remarks>
	SecurityAnalyst = 2,

	/// <summary>
	/// Compliance officer with read access to all events.
	/// </summary>
	/// <remarks>
	/// Read-only access to the complete audit trail for compliance
	/// reporting and SOC2 evidence collection.
	/// </remarks>
	ComplianceOfficer = 3,

	/// <summary>
	/// Administrator with full access including export capabilities.
	/// </summary>
	/// <remarks>
	/// Full access to all audit operations including export and
	/// integrity verification. Should be restricted to a minimal
	/// number of users.
	/// </remarks>
	Administrator = 4
}
