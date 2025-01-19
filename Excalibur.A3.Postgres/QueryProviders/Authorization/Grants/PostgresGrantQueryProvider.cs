using System.Data;

using Excalibur.A3.Authorization.Grants.Domain.Model;
using Excalibur.A3.Authorization.Grants.Domain.QueryProviders;
using Excalibur.DataAccess;

namespace Excalibur.A3.Postgres.QueryProviders.Authorization.Grants;

/// <summary>
///     A query provider for PostgreSQL databases, implementing the <see cref="IGrantQueryProvider" /> interface.
/// </summary>
public class PostgresGrantQueryProvider : IGrantQueryProvider
{
	/// <inheritdoc />
	public IDataQuery<IDbConnection, int> DeleteGrant(string userId, string tenantId, string grantType, string qualifier, string? revokedBy,
		DateTimeOffset? revokedOn, CancellationToken cancellationToken) =>
		new DeleteGrantQuery(userId, tenantId, grantType, qualifier, revokedBy, revokedOn, cancellationToken);

	/// <inheritdoc />
	public IDataQuery<IDbConnection, bool> GrantExists(string userId, string tenantId, string grantType, string qualifier,
		CancellationToken cancellationToken) =>
		new ExistsGrantQuery(userId, tenantId, grantType, qualifier, cancellationToken);

	/// <inheritdoc />
	public IDataQuery<IDbConnection, IEnumerable<Grant>> GetMatchingGrants(string? userId, string tenantId, string grantType,
		string qualifier,
		CancellationToken cancellationToken) =>
		new MatchingGrantsQuery(userId, tenantId, grantType, qualifier, cancellationToken);

	/// <inheritdoc />
	public IDataQuery<IDbConnection, Grant?> GetGrant(string userId, string tenantId, string grantType, string qualifier,
		CancellationToken cancellationToken) =>
		new ReadGrantQuery(userId, tenantId, grantType, qualifier, cancellationToken);

	/// <inheritdoc />
	public IDataQuery<IDbConnection, IEnumerable<Grant>> GetAllGrants(string userId, CancellationToken cancellationToken) =>
		new ReadAllGrantsQuery(userId, cancellationToken);

	/// <inheritdoc />
	public IDataQuery<IDbConnection, int> SaveGrant(Grant grant, CancellationToken cancellationToken) =>
		new SaveGrantQuery(grant, cancellationToken);

	/// <inheritdoc />
	public IDataQuery<IDbConnection, Dictionary<string, object>> FindUserGrants(string userId, CancellationToken cancellationToken) =>
		new FindUserGrantsQuery(userId, cancellationToken);

	/// <inheritdoc />
	public IDataQuery<IDbConnection, int> DeleteActivityGroupGrantsByUserId(string userId, string grantType,
		CancellationToken cancellationToken = default) =>
		new DeleteActivityGroupGrantsByUserIdQuery(userId, grantType, cancellationToken);

	/// <inheritdoc />
	public IDataQuery<IDbConnection, int> DeleteAllActivityGroupGrants(string grantType, CancellationToken cancellationToken = default) =>
		new DeleteAllActivityGroupGrantsQuery(grantType, cancellationToken);

	/// <inheritdoc />
	public IDataQuery<IDbConnection, int> InsertActivityGroupGrant(string userId, string fullName, string? tenantId, string grantType,
		string qualifier, DateTimeOffset? expiresOn, string grantedBy, CancellationToken cancellationToken = default) =>
		new InsertActivityGroupGrantQuery(userId, fullName, tenantId, grantType, qualifier, expiresOn, grantedBy, cancellationToken);

	/// <inheritdoc />
	public IDataQuery<IDbConnection, IEnumerable<string>> GetDistinctActivityGroupGrantUserIds(string grantType,
		CancellationToken cancellationToken = default) => new GetDistinctActivityGroupGrantUserIdsQuery(grantType, cancellationToken);
}
