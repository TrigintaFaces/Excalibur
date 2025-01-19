using Dapper;

using Excalibur.DataAccess;

namespace Excalibur.A3.Postgres.QueryProviders.Authorization.Grants;

/// <summary>
///     Represents a query to retrieve a distinct list of user IDs associated with a specific grant type.
/// </summary>
public class GetDistinctActivityGroupGrantUserIdsQuery : DataQuery<IEnumerable<string>>
{
	/// <summary>
	///     Initializes a new instance of the <see cref="GetDistinctActivityGroupGrantUserIdsQuery" /> class.
	/// </summary>
	/// <param name="grantType"> The grant type to filter the user IDs. </param>
	/// <param name="cancellationToken"> The cancellation token for the query. </param>
	public GetDistinctActivityGroupGrantUserIdsQuery(string grantType, CancellationToken cancellationToken = default)
	{
		const string CommandText = """
		                               SELECT DISTINCT user_id
		                               FROM Authz.grant
		                               WHERE grant_type = @GrantType
		                           """;

		Command = CreateCommand(
			CommandText,
			new DynamicParameters(new { GrantType = grantType }),
			DbTimeouts.RegularTimeoutSeconds,
			cancellationToken
		);

		Resolve = async connection => await connection.QueryAsync<string>(Command).ConfigureAwait(false);
	}
}
