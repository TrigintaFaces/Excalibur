using System.Data;

using Excalibur.A3.Authorization.Grants.Domain.Model;
using Excalibur.DataAccess;

namespace Excalibur.A3.Authorization.Grants.Domain.RequestProviders;

/// <summary>
///     Interface for a provider that generates database-specific requests for managing grants.
/// </summary>
public interface IGrantRequestProvider
{
	/// <summary>
	///     Creates a request to delete a grant and archive it in the grant history.
	/// </summary>
	/// <param name="userId"> The ID of the user associated with the grant. </param>
	/// <param name="tenantId"> The tenant ID associated with the grant. </param>
	/// <param name="grantType"> The type of the grant. </param>
	/// <param name="qualifier"> The qualifier of the grant. </param>
	/// <param name="revokedBy"> The ID of the entity revoking the grant (optional). </param>
	/// <param name="revokedOn"> The timestamp when the grant was revoked (optional). </param>
	/// <param name="cancellationToken"> The cancellation token for the request. </param>
	/// <returns> A request object for deleting a grant. </returns>
	public IDataRequest<IDbConnection, int> DeleteGrant(string userId, string tenantId, string grantType, string qualifier,
		string? revokedBy,
		DateTimeOffset? revokedOn, CancellationToken cancellationToken = default);

	/// <summary>
	///     Creates a request to check if a grant exists.
	/// </summary>
	/// <param name="userId"> The ID of the user associated with the grant. </param>
	/// <param name="tenantId"> The tenant ID associated with the grant. </param>
	/// <param name="grantType"> The type of the grant. </param>
	/// <param name="qualifier"> The qualifier of the grant. </param>
	/// <param name="cancellationToken"> The cancellation token for the request. </param>
	/// <returns> A request object for checking if the grant exists. </returns>
	public IDataRequest<IDbConnection, bool> GrantExists(string userId, string tenantId, string grantType, string qualifier,
		CancellationToken cancellationToken = default);

	/// <summary>
	///     Creates a request to retrieve matching grants based on the specified scope.
	/// </summary>
	/// <param name="userId"> The ID of the user associated with the grants (optional). </param>
	/// <param name="tenantId"> The tenant ID to filter the grants. </param>
	/// <param name="grantType"> The type of grants to filter. </param>
	/// <param name="qualifier"> The qualifier of the grants to filter. </param>
	/// <param name="cancellationToken"> The cancellation token for the request. </param>
	/// <returns> A request object for retrieving matching grants. </returns>
	public IDataRequest<IDbConnection, IEnumerable<Grant>> GetMatchingGrants(string? userId, string tenantId, string grantType,
		string qualifier,
		CancellationToken cancellationToken = default);

	/// <summary>
	///     Creates a request to retrieve a specific grant.
	/// </summary>
	/// <param name="userId"> The ID of the user associated with the grant. </param>
	/// <param name="tenantId"> The tenant ID associated with the grant. </param>
	/// <param name="grantType"> The type of the grant. </param>
	/// <param name="qualifier"> The qualifier of the grant. </param>
	/// <param name="cancellationToken"> The cancellation token for the request. </param>
	/// <returns> A request object for retrieving the grant. </returns>
	public IDataRequest<IDbConnection, Grant?> GetGrant(string userId, string tenantId, string grantType, string qualifier,
		CancellationToken cancellationToken = default);

	/// <summary>
	///     Creates a request to retrieve all grants for a specific user.
	/// </summary>
	/// <param name="userId"> The ID of the user associated with the grants. </param>
	/// <param name="cancellationToken"> The cancellation token for the request. </param>
	/// <returns> A request object for retrieving all grants for the user. </returns>
	public IDataRequest<IDbConnection, IEnumerable<Grant>> GetAllGrants(string userId, CancellationToken cancellationToken = default);

	/// <summary>
	///     Creates a request to save a new or updated grant.
	/// </summary>
	/// <param name="grant"> The grant to save. </param>
	/// <param name="cancellationToken"> The cancellation token for the request. </param>
	/// <returns> A request object for saving the grant. </returns>
	public IDataRequest<IDbConnection, int> SaveGrant(Grant grant, CancellationToken cancellationToken = default);

	/// <summary>
	///     Creates a request to retrieve user grants in a custom format (e.g., as a dictionary).
	/// </summary>
	/// <param name="userId"> The ID of the user associated with the grants. </param>
	/// <param name="cancellationToken"> The cancellation token for the request. </param>
	/// <returns> A request object for retrieving user grants. </returns>
	public IDataRequest<IDbConnection, Dictionary<string, object>> FindUserGrants(string userId, CancellationToken cancellationToken);

	/// <summary>
	///     Creates a request to delete all activity group grants for a specific user.
	/// </summary>
	/// <param name="userId"> The ID of the user whose activity group grants will be deleted. </param>
	/// <param name="grantType"> The type of grant to delete. </param>
	/// <param name="cancellationToken"> The cancellation token for the request. </param>
	/// <returns> A request object for deleting activity group grants for the user. </returns>
	IDataRequest<IDbConnection, int> DeleteActivityGroupGrantsByUserId(string userId, string grantType,
		CancellationToken cancellationToken = default);

	/// <summary>
	///     Creates a request to delete all activity group grants in the database.
	/// </summary>
	/// <param name="grantType"> The type of grant to delete. </param>
	/// <param name="cancellationToken"> The cancellation token for the request. </param>
	/// <returns> A request object for deleting all activity group grants. </returns>
	IDataRequest<IDbConnection, int> DeleteAllActivityGroupGrants(string grantType, CancellationToken cancellationToken = default);

	/// <summary>
	///     Creates a request to insert a new activity group grant into the database.
	/// </summary>
	/// <param name="userId"> The ID of the user associated with the grant. </param>
	/// <param name="fullName"> The full name of the user. </param>
	/// <param name="tenantId"> The tenant ID associated with the grant (optional). </param>
	/// <param name="grantType"> The type of the grant. </param>
	/// <param name="qualifier"> The qualifier of the grant. </param>
	/// <param name="expiresOn"> The expiration date of the grant (optional). </param>
	/// <param name="grantedBy"> The name of the entity granting the grant. </param>
	/// <param name="cancellationToken"> The cancellation token for the request. </param>
	/// <returns> A request object for inserting the activity group grant. </returns>
	IDataRequest<IDbConnection, int> InsertActivityGroupGrant(string userId, string fullName, string? tenantId, string grantType,
		string qualifier,
		DateTimeOffset? expiresOn, string grantedBy, CancellationToken cancellationToken = default);

	/// <summary>
	///     Creates a request to retrieve distinct user IDs associated with activity group grants.
	/// </summary>
	/// <param name="grantType"> The type of grant to filter. </param>
	/// <param name="cancellationToken"> The cancellation token for the request. </param>
	/// <returns> A request object for retrieving distinct user IDs. </returns>
	IDataRequest<IDbConnection, IEnumerable<string>> GetDistinctActivityGroupGrantUserIds(string grantType,
		CancellationToken cancellationToken = default);
}
