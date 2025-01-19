using System.Data;

using Excalibur.A3.Authorization.Grants.Domain.Model;
using Excalibur.DataAccess;

namespace Excalibur.A3.Authorization.Grants.Domain.QueryProviders;

/// <summary>
///     Interface for a provider that generates database-specific queries for managing grants.
/// </summary>
public interface IGrantQueryProvider
{
	/// <summary>
	///     Creates a query to delete a grant and archive it in the grant history.
	/// </summary>
	/// <param name="userId"> The ID of the user associated with the grant. </param>
	/// <param name="tenantId"> The tenant ID associated with the grant. </param>
	/// <param name="grantType"> The type of the grant. </param>
	/// <param name="qualifier"> The qualifier of the grant. </param>
	/// <param name="revokedBy"> The ID of the entity revoking the grant (optional). </param>
	/// <param name="revokedOn"> The timestamp when the grant was revoked (optional). </param>
	/// <param name="cancellationToken"> The cancellation token for the query. </param>
	/// <returns> A query object for deleting a grant. </returns>
	public IDataQuery<IDbConnection, int> DeleteGrant(string userId, string tenantId, string grantType, string qualifier, string? revokedBy,
		DateTimeOffset? revokedOn, CancellationToken cancellationToken = default);

	/// <summary>
	///     Creates a query to check if a grant exists.
	/// </summary>
	/// <param name="userId"> The ID of the user associated with the grant. </param>
	/// <param name="tenantId"> The tenant ID associated with the grant. </param>
	/// <param name="grantType"> The type of the grant. </param>
	/// <param name="qualifier"> The qualifier of the grant. </param>
	/// <param name="cancellationToken"> The cancellation token for the query. </param>
	/// <returns> A query object for checking if the grant exists. </returns>
	public IDataQuery<IDbConnection, bool> GrantExists(string userId, string tenantId, string grantType, string qualifier,
		CancellationToken cancellationToken = default);

	/// <summary>
	///     Creates a query to retrieve matching grants based on the specified scope.
	/// </summary>
	/// <param name="userId"> The ID of the user associated with the grants (optional). </param>
	/// <param name="tenantId"> The tenant ID to filter the grants. </param>
	/// <param name="grantType"> The type of grants to filter. </param>
	/// <param name="qualifier"> The qualifier of the grants to filter. </param>
	/// <param name="cancellationToken"> The cancellation token for the query. </param>
	/// <returns> A query object for retrieving matching grants. </returns>
	public IDataQuery<IDbConnection, IEnumerable<Grant>> GetMatchingGrants(string? userId, string tenantId, string grantType,
		string qualifier,
		CancellationToken cancellationToken = default);

	/// <summary>
	///     Creates a query to retrieve a specific grant.
	/// </summary>
	/// <param name="userId"> The ID of the user associated with the grant. </param>
	/// <param name="tenantId"> The tenant ID associated with the grant. </param>
	/// <param name="grantType"> The type of the grant. </param>
	/// <param name="qualifier"> The qualifier of the grant. </param>
	/// <param name="cancellationToken"> The cancellation token for the query. </param>
	/// <returns> A query object for retrieving the grant. </returns>
	public IDataQuery<IDbConnection, Grant?> GetGrant(string userId, string tenantId, string grantType, string qualifier,
		CancellationToken cancellationToken = default);

	/// <summary>
	///     Creates a query to retrieve all grants for a specific user.
	/// </summary>
	/// <param name="userId"> The ID of the user associated with the grants. </param>
	/// <param name="cancellationToken"> The cancellation token for the query. </param>
	/// <returns> A query object for retrieving all grants for the user. </returns>
	public IDataQuery<IDbConnection, IEnumerable<Grant>> GetAllGrants(string userId, CancellationToken cancellationToken = default);

	/// <summary>
	///     Creates a query to save a new or updated grant.
	/// </summary>
	/// <param name="grant"> The grant to save. </param>
	/// <param name="cancellationToken"> The cancellation token for the query. </param>
	/// <returns> A query object for saving the grant. </returns>
	public IDataQuery<IDbConnection, int> SaveGrant(Grant grant, CancellationToken cancellationToken = default);

	/// <summary>
	///     Creates a query to retrieve user grants in a custom format (e.g., as a dictionary).
	/// </summary>
	/// <param name="userId"> The ID of the user associated with the grants. </param>
	/// <param name="cancellationToken"> The cancellation token for the query. </param>
	/// <returns> A query object for retrieving user grants. </returns>
	public IDataQuery<IDbConnection, Dictionary<string, object>> FindUserGrants(string userId, CancellationToken cancellationToken);

	/// <summary>
	///     Creates a query to delete all activity group grants for a specific user.
	/// </summary>
	/// <param name="userId"> The ID of the user whose activity group grants will be deleted. </param>
	/// <param name="grantType"> The type of grant to delete. </param>
	/// <param name="cancellationToken"> The cancellation token for the query. </param>
	/// <returns> A query object for deleting activity group grants for the user. </returns>
	IDataQuery<IDbConnection, int> DeleteActivityGroupGrantsByUserId(string userId, string grantType,
		CancellationToken cancellationToken = default);

	/// <summary>
	///     Creates a query to delete all activity group grants in the database.
	/// </summary>
	/// <param name="grantType"> The type of grant to delete. </param>
	/// <param name="cancellationToken"> The cancellation token for the query. </param>
	/// <returns> A query object for deleting all activity group grants. </returns>
	IDataQuery<IDbConnection, int> DeleteAllActivityGroupGrants(string grantType, CancellationToken cancellationToken = default);

	/// <summary>
	///     Creates a query to insert a new activity group grant into the database.
	/// </summary>
	/// <param name="userId"> The ID of the user associated with the grant. </param>
	/// <param name="fullName"> The full name of the user. </param>
	/// <param name="tenantId"> The tenant ID associated with the grant (optional). </param>
	/// <param name="grantType"> The type of the grant. </param>
	/// <param name="qualifier"> The qualifier of the grant. </param>
	/// <param name="expiresOn"> The expiration date of the grant (optional). </param>
	/// <param name="grantedBy"> The name of the entity granting the grant. </param>
	/// <param name="cancellationToken"> The cancellation token for the query. </param>
	/// <returns> A query object for inserting the activity group grant. </returns>
	IDataQuery<IDbConnection, int> InsertActivityGroupGrant(string userId, string fullName, string? tenantId, string grantType,
		string qualifier,
		DateTimeOffset? expiresOn, string grantedBy, CancellationToken cancellationToken = default);

	/// <summary>
	///     Creates a query to retrieve distinct user IDs associated with activity group grants.
	/// </summary>
	/// <param name="grantType"> The type of grant to filter. </param>
	/// <param name="cancellationToken"> The cancellation token for the query. </param>
	/// <returns> A query object for retrieving distinct user IDs. </returns>
	IDataQuery<IDbConnection, IEnumerable<string>> GetDistinctActivityGroupGrantUserIds(string grantType,
		CancellationToken cancellationToken = default);
}
