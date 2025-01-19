namespace Excalibur.A3.Authorization.Grants.Domain.Model;

/// <summary>
///     Represents data for a grant as retrieved from the database.
/// </summary>
public sealed record GrantData
{
	/// <summary>
	///     Gets or initializes the user ID associated with the grant.
	/// </summary>
	public required string UserId { get; init; }

	/// <summary>
	///     Gets or initializes the full name of the user or entity associated with the grant.
	/// </summary>
	public required string FullName { get; init; }

	/// <summary>
	///     Gets or initializes the tenant ID associated with the grant.
	/// </summary>
	public required string TenantId { get; init; }

	/// <summary>
	///     Gets or initializes the type of the grant.
	/// </summary>
	public required string GrantType { get; init; }

	/// <summary>
	///     Gets or initializes the qualifier of the grant.
	/// </summary>
	public required string Qualifier { get; init; }

	/// <summary>
	///     Gets or initializes the expiration date of the grant, if applicable.
	/// </summary>
	public DateTimeOffset? ExpiresOn { get; init; }

	/// <summary>
	///     Gets or initializes the name of the entity granting the permission.
	/// </summary>
	public required string GrantedBy { get; init; }

	/// <summary>
	///     Gets or initializes the date and time the grant was issued.
	/// </summary>
	public DateTimeOffset? GrantedOn { get; init; }
}
