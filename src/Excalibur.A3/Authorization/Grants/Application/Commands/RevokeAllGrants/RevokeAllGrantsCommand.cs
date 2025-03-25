using Excalibur.A3.Audit;
using Excalibur.A3.Authorization.Requests.Commands;

namespace Excalibur.A3.Authorization.Grants.Application.Commands.RevokeAllGrants;

/// <summary>
///     Represents a command to revoke all grants assigned to a specific user.
/// </summary>
/// <remarks>
///     This command is used to remove all permissions or grants associated with a user within a specific tenant or globally if no tenant ID
///     is provided.
/// </remarks>
public class RevokeAllGrantsCommand(
	string userId,
	string fullName,
	Guid correlationId,
	string? tenantId = null)
	: AuthorizeCommandBase<AuditableResult<bool>>(correlationId, tenantId)
{
	/// <summary>
	///     Gets or sets the ID of the user whose grants are being revoked.
	/// </summary>
	public string UserId { get; set; } = userId;

	/// <summary>
	///     Gets or sets the full name of the user whose grants are being revoked.
	/// </summary>
	public string FullName { get; set; } = fullName;

	/// <inheritdoc />
	public override string ActivityDescription => "Revokes all grants from a user.";

	/// <inheritdoc />
	public override string ActivityDisplayName => "Revokes all grants";
}
