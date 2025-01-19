namespace Excalibur.A3.Authorization.Events;

/// <summary>
///     Represents an event that occurs when a grant is revoked from a user.
/// </summary>
public class GrantRevoked : IGrantRevoked
{
	/// <summary>
	///     Initializes a new instance of the <see cref="GrantRevoked" /> class with the specified details.
	/// </summary>
	/// <param name="userId"> The ID of the user from whom the grant was revoked. </param>
	/// <param name="fullName"> The full name of the user. </param>
	/// <param name="applicationName"> The name of the application associated with the grant. </param>
	/// <param name="tenantId"> The tenant ID associated with the grant. </param>
	/// <param name="grantType"> The type of the grant. </param>
	/// <param name="qualifier"> Additional qualifiers for the grant. </param>
	/// <param name="expiresOn"> The expiration date of the grant, if applicable. </param>
	/// <param name="revokedBy"> The identifier of the entity or user that revoked the grant. </param>
	/// <param name="revokedOn"> The date and time the grant was revoked. </param>
	public GrantRevoked(
		string userId,
		string fullName,
		string applicationName,
		string tenantId,
		string grantType,
		string qualifier,
		DateTimeOffset? expiresOn,
		string revokedBy,
		DateTimeOffset revokedOn)
	{
		UserId = userId;
		FullName = fullName;
		ApplicationName = applicationName;
		TenantId = tenantId;
		GrantType = grantType;
		Qualifier = qualifier;
		ExpiresOn = expiresOn;
		RevokedBy = revokedBy;
		RevokedOn = revokedOn;
	}

	/// <summary>
	///     Initializes a new instance of the <see cref="GrantRevoked" /> class for deserialization or manual population.
	/// </summary>
	public GrantRevoked()
	{
	}

	/// <inheritdoc />
	public string ApplicationName { get; init; }

	/// <inheritdoc />
	public DateTimeOffset? ExpiresOn { get; init; }

	/// <inheritdoc />
	public string FullName { get; init; }

	/// <inheritdoc />
	public string GrantType { get; init; }

	/// <inheritdoc />
	public string Qualifier { get; init; }

	/// <inheritdoc />
	public string RevokedBy { get; init; }

	/// <inheritdoc />
	public DateTimeOffset RevokedOn { get; init; }

	/// <inheritdoc />
	public string TenantId { get; init; }

	/// <inheritdoc />
	public string UserId { get; init; }
}
