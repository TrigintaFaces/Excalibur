using Dapper;

using Excalibur.DataAccess;

namespace Excalibur.A3.SqlServer.RequestProviders.Authorization.ActivityGroups;

/// <summary>
///     Represents a query for deleting all activity groups from the database.
/// </summary>
public class DeleteAllActivityGroupsRequest : DataRequest<int>
{
	/// <summary>
	///     Initializes a new instance of the <see cref="DeleteAllActivityGroupsRequest" /> class.
	/// </summary>
	/// <param name="cancellationToken"> The cancellation token for the query. </param>
	public DeleteAllActivityGroupsRequest(CancellationToken cancellationToken)
	{
		const string CommandText = "DELETE FROM authz.ActivityGroup";

		Command = CreateCommand(CommandText, null, commandTimeout: DbTimeouts.RegularTimeoutSeconds, cancellationToken: cancellationToken);

		ResolveAsync = async connection => await connection.ExecuteAsync(Command).ConfigureAwait(false);
	}
}
