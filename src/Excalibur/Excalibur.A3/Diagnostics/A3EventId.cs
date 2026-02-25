// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.A3.Diagnostics;

/// <summary>
/// Event IDs for A3 (Authentication, Authorization, Auditing) infrastructure (180000-182999).
/// </summary>
/// <remarks>
/// <para>Subcategory ranges:</para>
/// <list type="bullet">
/// <item>180000-180499: Authentication</item>
/// <item>180500-180999: Authorization</item>
/// <item>181000-181499: Auditing</item>
/// <item>181500-181999: Activity Groups</item>
/// <item>182000-182499: Permissions</item>
/// <item>182500-182999: Reserved</item>
/// </list>
/// </remarks>
public static class A3EventId
{
	// ========================================
	// 180000-180099: Authentication Core
	// ========================================

	/// <summary>Authentication started.</summary>
	public const int AuthenticationStarted = 180000;

	/// <summary>Authentication succeeded.</summary>
	public const int AuthenticationSucceeded = 180001;

	/// <summary>Authentication failed.</summary>
	public const int AuthenticationFailed = 180002;

	/// <summary>Token validated.</summary>
	public const int TokenValidated = 180003;

	/// <summary>Token expired.</summary>
	public const int TokenExpired = 180004;

	// ========================================
	// 180500-180599: Authorization Core
	// ========================================

	/// <summary>Authorization check started.</summary>
	public const int AuthorizationCheckStarted = 180500;

	/// <summary>Authorization granted.</summary>
	public const int AuthorizationGranted = 180501;

	/// <summary>Authorization denied.</summary>
	public const int AuthorizationDenied = 180502;

	/// <summary>Permission evaluated.</summary>
	public const int PermissionEvaluated = 180503;

	// ========================================
	// 181500-181599: Activity Groups
	// ========================================

	/// <summary>Activity groups retrieval failed.</summary>
	public const int ActivityGroupsError = 181500;

	/// <summary>Activity grants retrieval failed.</summary>
	public const int ActivityGrantsError = 181501;

	/// <summary>Activity groups retrieved.</summary>
	public const int ActivityGroupsRetrieved = 181502;

	/// <summary>Activity grants retrieved.</summary>
	public const int ActivityGrantsRetrieved = 181503;

	// ========================================
	// 180600-180699: Grant Repository
	// ========================================

	/// <summary>Error saving grant.</summary>
	public const int GrantSaveError = 180600;

	// ========================================
	// 181100-181199: Audit Middleware
	// ========================================

	/// <summary>Failed to publish audit event.</summary>
	public const int AuditPublishFailure = 181100;
}
