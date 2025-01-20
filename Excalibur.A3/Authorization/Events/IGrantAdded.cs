using Excalibur.Core.Domain.Events;

namespace Excalibur.A3.Authorization.Events;

/// <summary>
///     Represents a domain event that occurs when a grant is added to a user.
/// </summary>
public interface IGrantAdded : IDomainEvent
{
	/// <summary>
	///     Gets the name of the application associated with the grant.
	/// </summary>
	string ApplicationName { get; init; }

	/// <summary>
	///     Gets the expiration date of the grant, if applicable.
	/// </summary>
	DateTimeOffset? ExpiresOn { get; init; }

	/// <summary>
	///     Gets the full name of the user to whom the grant was added.
	/// </summary>
	string FullName { get; init; }

	/// <summary>
	///     Gets the identifier of the entity or user that granted the access.
	/// </summary>
	string GrantedBy { get; init; }

	/// <summary>
	///     Gets the date and time when the grant was added.
	/// </summary>
	DateTimeOffset GrantedOn { get; init; }

	/// <summary>
	///     Gets the type of the grant that was added.
	/// </summary>
	string GrantType { get; init; }

	/// <summary>
	///     Gets any additional qualifier for the grant.
	/// </summary>
	string Qualifier { get; init; }

	/// <summary>
	///     Gets the tenant ID associated with the grant.
	/// </summary>
	string TenantId { get; init; }

	/// <summary>
	///     Gets the ID of the user to whom the grant was added.
	/// </summary>
	string UserId { get; init; }
}
