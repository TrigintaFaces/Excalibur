using System.Data;

using Excalibur.A3.Authorization.Grants.Domain.QueryProviders;
using Excalibur.DataAccess;

namespace Excalibur.A3.Postgres.QueryProviders.Authorization.ActivityGroups;

/// <summary>
///     Provides PostgreSQL-specific implementations for queries related to activity groups.
/// </summary>
public class PostgresActivityGroupQueryProvider : IActivityGroupQueryProvider
{
	///<inheritdoc />
	public IDataQuery<IDbConnection, bool> ActivityGroupExists(string activityGroupName, CancellationToken cancellationToken = default) =>
		new ExistsActivityGroupQuery(activityGroupName, cancellationToken);

	///<inheritdoc />
	public IDataQuery<IDbConnection, Dictionary<string, object>> FindActivityGroups(CancellationToken cancellationToken = default) =>
		new FindActivityGroupsQuery(cancellationToken);

	///<inheritdoc />
	public IDataQuery<IDbConnection, int> DeleteAllActivityGroups(CancellationToken cancellationToken = default) =>
		new DeleteAllActivityGroupsQuery(cancellationToken);

	///<inheritdoc />
	public IDataQuery<IDbConnection, int> CreateActivityGroup(string? tenantId, string name, string activityName,
		CancellationToken cancellationToken = default) => new CreateActivityGroupQuery(tenantId, name, activityName, cancellationToken);
}
