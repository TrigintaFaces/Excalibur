// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.A3.Audit;
using Excalibur.A3.Authorization.Requests;
using Excalibur.Application.Requests;

namespace Excalibur.A3.Authorization.Grants;

/// <summary>
/// Represents a command to revoke all grants assigned to a specific user.
/// </summary>
/// <remarks>
/// This command is used to remove all permissions or grants associated with a user within a specific tenant or globally if no tenant ID
/// is provided.
/// </remarks>
[Activity("Revoke all grants", "Revokes all grants from a user.")]
public sealed class RevokeAllGrantsCommand(
	string userId,
	string fullName,
	Guid correlationId,
	string? tenantId = null)
	: AuthorizeCommandBase<AuditableResult<bool>>(correlationId, tenantId)
{
	/// <summary>
	/// Gets or sets the ID of the user whose grants are being revoked.
	/// </summary>
	/// <value>The ID of the user.</value>
	public string UserId { get; set; } = userId;

	/// <summary>
	/// Gets or sets the full name of the user whose grants are being revoked.
	/// </summary>
	/// <value>The full name of the user.</value>
	public string FullName { get; set; } = fullName;

}
