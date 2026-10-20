// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0


using Excalibur.A3.Audit;
using Excalibur.A3.Authorization.Requests;
using Excalibur.Application.Requests;

namespace Excalibur.A3.Authorization.Grants;

/// <summary>
/// Represents a command to revoke all grants assigned to a specific user.
/// </summary>
/// <remarks>
/// Removes every grant type within the command's authorized ambient tenant. All-tenants scope is refused
/// by this command; explicitly authorized administrative tools use the atomic store capability directly.
/// </remarks>
[Activity("Revoke all grants", "Revokes all grants from a user.")]
public sealed class RevokeAllGrantsCommand(
	string userId,
	string fullName,
	Guid correlationId,
	string? tenantId = null)
	: AuthorizeCommandBase<AuditableResult<bool>>(correlationId, tenantId)
{
	/// <summary>Gets the command correlation identifier, preserved during serialization.</summary>
	public Guid CorrelationId { get; } = correlationId;

	/// <summary>
	/// Gets or sets the stable identity of this revocation. Preserve this value when retrying an unknown commit outcome.
	/// </summary>
	public Guid OperationId { get; set; } = Guid.NewGuid();

	/// <summary>
	/// Gets or sets the explicit revocation scope. This preserves all-tenants intent independently of ambient tenant normalization.
	/// </summary>
	public GrantRevocationScope RevocationScope { get; set; } = tenantId is null
		? GrantRevocationScope.AllTenants
		: GrantRevocationScope.ForTenant(tenantId);

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
