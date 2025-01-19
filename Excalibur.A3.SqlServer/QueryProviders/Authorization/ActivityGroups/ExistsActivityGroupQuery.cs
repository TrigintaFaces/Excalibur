using Dapper;

using Excalibur.DataAccess;

namespace Excalibur.A3.SqlServer.QueryProviders.Authorization.ActivityGroups;

/// <summary>
///     Represents a query for checking the existence of an activity group in the database.
/// </summary>
public class ExistsActivityGroupQuery : DataQuery<bool>
{
	/// <summary>
	///     Initializes a new instance of the <see cref="ExistsActivityGroupQuery" /> class.
	/// </summary>
	/// <param name="activityGroupName"> The name of the activity group to check for existence. </param>
	/// <param name="cancellationToken"> The cancellation token for the query. </param>
	public ExistsActivityGroupQuery(string activityGroupName, CancellationToken cancellationToken)
	{
		const string CommandText = """
		                           SELECT EXISTS
		                           (
		                             SELECT 1
		                             FROM authz.ActivityGroup
		                             WHERE Name=@activityGroupName
		                           );
		                           """;

		Command = CreateCommand(
			CommandText,
			new DynamicParameters(new { activityGroupName }),
			DbTimeouts.RegularTimeoutSeconds,
			cancellationToken);

		Resolve = async connection => await connection.ExecuteScalarAsync<bool>(Command).ConfigureAwait(false);
	}
}
