using Excalibur.Core.Domain.Events;

namespace Excalibur.A3.Authorization.Events;

/// <summary>
///     Represents a domain event that occurs when a grant is revoked from a user.
/// </summary>
public interface IGrantRevoked : IDomainEvent
{
	/// <summary>
	///     Gets the name of the application associated with the grant.
	/// </summary>
	public string ApplicationName { get; init; }

	/// <summary>
	///     Gets the expiration date of the revoked grant, if applicable.
	/// </summary>
	public DateTimeOffset? ExpiresOn { get; init; }

	/// <summary>
	///     Gets the full name of the user from whom the grant was revoked.
	/// </summary>
	public string FullName { get; init; }

	/// <summary>
	///     Gets the type of the grant that was revoked.
	/// </summary>
	public string GrantType { get; init; }

	/// <summary>
	///     Gets any additional qualifier for the grant.
	/// </summary>
	public string Qualifier { get; init; }

	/// <summary>
	///     Gets the identifier of the entity or user who revoked the grant.
	/// </summary>
	public string RevokedBy { get; init; }

	/// <summary>
	///     Gets the date and time when the grant was revoked.
	/// </summary>
	public DateTimeOffset RevokedOn { get; init; }

	/// <summary>
	///     Gets the tenant ID associated with the revoked grant.
	/// </summary>
	public string TenantId { get; init; }

	/// <summary>
	///     Gets the ID of the user from whom the grant was revoked.
	/// </summary>
	public string UserId { get; init; }
}
