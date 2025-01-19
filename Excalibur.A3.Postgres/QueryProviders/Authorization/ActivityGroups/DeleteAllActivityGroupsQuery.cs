using Dapper;

using Excalibur.DataAccess;

namespace Excalibur.A3.Postgres.QueryProviders.Authorization.ActivityGroups;

/// <summary>
///     Represents a query for deleting all activity groups from the database.
/// </summary>
public class DeleteAllActivityGroupsQuery : DataQuery<int>
{
	/// <summary>
	///     Initializes a new instance of the <see cref="DeleteAllActivityGroupsQuery" /> class.
	/// </summary>
	/// <param name="cancellationToken"> The cancellation token for the query. </param>
	public DeleteAllActivityGroupsQuery(CancellationToken cancellationToken)
	{
		const string CommandText = "DELETE FROM authz.activity_group";

		Command = CreateCommand(CommandText, null, DbTimeouts.RegularTimeoutSeconds, cancellationToken);

		Resolve = async connection => await connection.ExecuteAsync(Command).ConfigureAwait(false);
	}
}
