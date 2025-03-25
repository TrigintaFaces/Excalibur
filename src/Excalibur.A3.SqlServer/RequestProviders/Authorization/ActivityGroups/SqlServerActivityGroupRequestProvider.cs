using System.Data;

using Excalibur.A3.Authorization.Grants.Domain.RequestProviders;
using Excalibur.DataAccess;

namespace Excalibur.A3.SqlServer.RequestProviders.Authorization.ActivityGroups;

/// <summary>
///     Provides MSSQL-specific implementations for queries related to activity groups.
/// </summary>
public class SqlServerActivityGroupRequestProvider : IActivityGroupRequestProvider
{
	///<inheritdoc />
	public IDataRequest<IDbConnection, bool> ActivityGroupExists(string activityGroupName, CancellationToken cancellationToken = default) =>
		new ExistsActivityGroupRequest(activityGroupName, cancellationToken);

	///<inheritdoc />
	public IDataRequest<IDbConnection, Dictionary<string, object>> FindActivityGroups(CancellationToken cancellationToken = default) =>
		new FindActivityGroupsRequest(cancellationToken);

	///<inheritdoc />
	public IDataRequest<IDbConnection, int> DeleteAllActivityGroups(CancellationToken cancellationToken = default) =>
		new DeleteAllActivityGroupsRequest(cancellationToken);

	///<inheritdoc />
	public IDataRequest<IDbConnection, int> CreateActivityGroup(string? tenantId, string name, string activityName,
		CancellationToken cancellationToken = default) => new CreateActivityGroupRequest(tenantId, name, activityName, cancellationToken);
}
