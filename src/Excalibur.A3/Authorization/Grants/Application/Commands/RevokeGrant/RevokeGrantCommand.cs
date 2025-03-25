using Excalibur.A3.Audit;
using Excalibur.A3.Authorization.Requests.Commands;

namespace Excalibur.A3.Authorization.Grants.Application.Commands.RevokeGrant;

/// <summary>
///     Represents a command to revoke a specific grant from a user.
/// </summary>
/// <remarks>
///     This command is used to revoke a user's specific grant, identified by its type and qualifier, within a particular tenant and
///     correlation context.
/// </remarks>
public class RevokeGrantCommand(
	string userId,
	string grantType,
	string qualifier,
	Guid correlationId,
	string? tenantId = null)
	: AuthorizeCommandBase<AuditableResult<bool>>(correlationId, tenantId)
{
	/// <summary>
	///     Gets or sets the ID of the user whose grant is being revoked.
	/// </summary>
	public string UserId { get; set; } = userId;

	/// <summary>
	///     Gets or sets the type of grant being revoked.
	/// </summary>
	/// <example> Examples of grant types include "ActivityGroup" or "Activity". </example>
	public string GrantType { get; set; } = grantType;

	/// <summary>
	///     Gets or sets the qualifier for the grant being revoked.
	/// </summary>
	/// <remarks> The qualifier provides additional context or details about the specific grant type being revoked. </remarks>
	public string Qualifier { get; set; } = qualifier;

	/// <inheritdoc />
	public override string ActivityDescription => "Revokes a user's permissions.";

	/// <inheritdoc />
	public override string ActivityDisplayName => "Revoke grant";
}
