// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.A3.Authorization.Roles.Commands;

/// <summary>
/// Command to assign a role to a user by creating a grant with <c>GrantType = "Role"</c>.
/// </summary>
/// <remarks>
/// <para>
/// A role assignment creates a single grant. The authorization evaluator resolves
/// the role's activity groups at evaluation time, matching the ASP.NET Core Identity
/// pattern (role claim, not expanded permission claims).
/// </para>
/// <para>
/// Grant scope format: <c>{TenantId}:Role:{RoleName}</c>.
/// </para>
/// </remarks>
/// <param name="UserId">The user to assign the role to.</param>
/// <param name="FullName">The full name of the user.</param>
/// <param name="RoleName">The name of the role to assign.</param>
/// <param name="TenantId">The tenant scope for the assignment.</param>
/// <param name="ExpiresOn">Optional expiration for the role assignment.</param>
/// <param name="AssignedBy">The actor performing the assignment.</param>
internal sealed record AddRoleAssignmentCommand(
	string UserId,
	string FullName,
	string RoleName,
	string TenantId,
	DateTimeOffset? ExpiresOn,
	string AssignedBy);
