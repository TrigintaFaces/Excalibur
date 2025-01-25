using System.Data;

using Excalibur.A3.Authorization.Grants.Domain.Model;
using Excalibur.A3.Authorization.Grants.Domain.RequestProviders;
using Excalibur.DataAccess;

namespace Excalibur.A3.SqlServer.RequestProviders.Authorization.Grants;

/// <summary>
///     A query provider for SQL Server databases, implementing the <see /> interface.
/// </summary>
public class SqlServerGrantRequestProvider : IGrantRequestProvider
{
	/// <inheritdoc />
	public IDataRequest<IDbConnection, int> DeleteGrant(string userId, string tenantId, string grantType, string qualifier,
		string? revokedBy,
		DateTimeOffset? revokedOn, CancellationToken cancellationToken) =>
		new DeleteGrantRequest(userId, tenantId, grantType, qualifier, revokedBy, revokedOn, cancellationToken);

	/// <inheritdoc />
	public IDataRequest<IDbConnection, bool> GrantExists(string userId, string tenantId, string grantType, string qualifier,
		CancellationToken cancellationToken) =>
		new ExistsGrantRequest(userId, tenantId, grantType, qualifier, cancellationToken);

	/// <inheritdoc />
	public IDataRequest<IDbConnection, IEnumerable<Grant>> GetMatchingGrants(string? userId, string tenantId, string grantType,
		string qualifier,
		CancellationToken cancellationToken) =>
		new MatchingGrantsRequest(userId, tenantId, grantType, qualifier, cancellationToken);

	/// <inheritdoc />
	public IDataRequest<IDbConnection, Grant?> GetGrant(string userId, string tenantId, string grantType, string qualifier,
		CancellationToken cancellationToken) =>
		new ReadGrantRequest(userId, tenantId, grantType, qualifier, cancellationToken);

	/// <inheritdoc />
	public IDataRequest<IDbConnection, IEnumerable<Grant>> GetAllGrants(string userId, CancellationToken cancellationToken) =>
		new ReadAllGrantsRequest(userId, cancellationToken);

	/// <inheritdoc />
	public IDataRequest<IDbConnection, int> SaveGrant(Grant grant, CancellationToken cancellationToken) =>
		new SaveGrantRequest(grant, cancellationToken);

	/// <inheritdoc />
	public IDataRequest<IDbConnection, Dictionary<string, object>> FindUserGrants(string userId, CancellationToken cancellationToken) =>
		new FindUserGrantsRequest(userId, cancellationToken);

	/// <inheritdoc />
	public IDataRequest<IDbConnection, int> DeleteActivityGroupGrantsByUserId(string userId, string grantType,
		CancellationToken cancellationToken = default) =>
		new DeleteActivityGroupGrantsByUserIdRequest(userId, grantType, cancellationToken);

	/// <inheritdoc />
	public IDataRequest<IDbConnection, int> DeleteAllActivityGroupGrants(string grantType, CancellationToken cancellationToken = default) =>
		new DeleteAllActivityGroupGrantsRequest(grantType, cancellationToken);

	/// <inheritdoc />
	public IDataRequest<IDbConnection, int> InsertActivityGroupGrant(string userId, string fullName, string? tenantId, string grantType,
		string qualifier,
		DateTimeOffset? expiresOn,
		string grantedBy, CancellationToken cancellationToken = default) =>
		new InsertActivityGroupGrantRequest(userId, fullName, tenantId, grantType, qualifier, expiresOn, grantedBy, cancellationToken);

	/// <inheritdoc />
	public IDataRequest<IDbConnection, IEnumerable<string>> GetDistinctActivityGroupGrantUserIds(string grantType,
		CancellationToken cancellationToken = default) => new GetDistinctActivityGroupGrantUserIdsRequest(grantType, cancellationToken);
}
