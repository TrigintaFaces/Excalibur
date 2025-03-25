namespace Excalibur.A3.Authorization.Events;

/// <summary>
///     Represents an event that occurs when a grant is added to a user.
/// </summary>
public class GrantAdded : IGrantAdded
{
	/// <summary>
	///     Initializes a new instance of the <see cref="GrantAdded" /> class with the specified details.
	/// </summary>
	/// <param name="userId"> The ID of the user to whom the grant was added. </param>
	/// <param name="fullName"> The full name of the user. </param>
	/// <param name="applicationName"> The name of the application associated with the grant. </param>
	/// <param name="tenantId"> The tenant ID associated with the grant. </param>
	/// <param name="grantType"> The type of the grant. </param>
	/// <param name="qualifier"> Additional qualifiers for the grant. </param>
	/// <param name="expiresOn"> The expiration date of the grant, if applicable. </param>
	/// <param name="grantedBy"> The identifier of the entity or user that granted the access. </param>
	/// <param name="grantedOn"> The date and time the grant was issued. </param>
	public GrantAdded(
		string userId,
		string fullName,
		string applicationName,
		string tenantId,
		string grantType,
		string qualifier,
		DateTimeOffset? expiresOn,
		string grantedBy,
		DateTimeOffset grantedOn)
	{
		UserId = userId;
		FullName = fullName;
		ApplicationName = applicationName;
		TenantId = tenantId;
		GrantType = grantType;
		Qualifier = qualifier;
		ExpiresOn = expiresOn;
		GrantedBy = grantedBy;
		GrantedOn = grantedOn;
	}

	/// <summary>
	///     Initializes a new instance of the <see cref="GrantAdded" /> class for deserialization or manual population.
	/// </summary>
	public GrantAdded()
	{
	}

	/// <inheritdoc />
	public string ApplicationName { get; init; }

	/// <inheritdoc />
	public DateTimeOffset? ExpiresOn { get; init; }

	/// <inheritdoc />
	public string FullName { get; init; }

	/// <inheritdoc />
	public string GrantedBy { get; init; }

	/// <inheritdoc />
	public DateTimeOffset GrantedOn { get; init; }

	/// <inheritdoc />
	public string GrantType { get; init; }

	/// <inheritdoc />
	public string Qualifier { get; init; }

	/// <inheritdoc />
	public string TenantId { get; init; }

	/// <inheritdoc />
	public string UserId { get; init; }
}
