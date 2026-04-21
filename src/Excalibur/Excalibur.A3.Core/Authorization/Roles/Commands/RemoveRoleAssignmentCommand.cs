// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.A3.Authorization.Roles.Commands;

/// <summary>
/// Command to remove a role assignment from a user by revoking the role grant.
/// </summary>
/// <remarks>
/// <para>
/// Revokes the grant with <c>GrantType = "Role"</c> and <c>Qualifier = {RoleName}</c>
/// for the specified user and tenant.
/// </para>
/// </remarks>
/// <param name="UserId">The user to remove the role from.</param>
/// <param name="RoleName">The name of the role to remove.</param>
/// <param name="TenantId">The tenant scope for the assignment.</param>
/// <param name="RevokedBy">The actor performing the revocation.</param>
internal sealed record RemoveRoleAssignmentCommand(
	string UserId,
	string RoleName,
	string TenantId,
	string RevokedBy);
