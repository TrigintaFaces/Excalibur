// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.A3.Audit;
using Excalibur.A3.Authorization.Requests;
using Excalibur.Application.Requests;

namespace Excalibur.A3.Authorization.Grants;

/// <summary>
/// Represents a command to revoke a specific grant from a user.
/// </summary>
/// <remarks>
/// This command is used to revoke a user's specific grant, identified by its type and qualifier, within a particular tenant and
/// correlation context.
/// </remarks>
[Activity("Revoke grant", "Revokes a user's permissions.")]
public sealed class RevokeGrantCommand(
	string userId,
	string grantType,
	string qualifier,
	Guid correlationId,
	string? tenantId = null)
	: AuthorizeCommandBase<AuditableResult<bool>>(correlationId, tenantId)
{
	/// <summary>
	/// Gets or sets the ID of the user whose grant is being revoked.
	/// </summary>
	/// <value>The ID of the user.</value>
	public string UserId { get; set; } = userId;

	/// <summary>
	/// Gets or sets the type of grant being revoked.
	/// </summary>
	/// <value>The type of grant.</value>
	/// <example> Examples of grant types include "ActivityGroup" or "Activity". </example>
	public string GrantType { get; set; } = grantType;

	/// <summary>
	/// Gets or sets the qualifier for the grant being revoked.
	/// </summary>
	/// <value>The qualifier for the grant.</value>
	/// <remarks> The qualifier provides additional context or details about the specific grant type being revoked. </remarks>
	public string Qualifier { get; set; } = qualifier;

}
