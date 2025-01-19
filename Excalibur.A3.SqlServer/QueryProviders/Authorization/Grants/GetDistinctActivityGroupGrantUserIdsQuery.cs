using Dapper;

using Excalibur.DataAccess;

namespace Excalibur.A3.SqlServer.QueryProviders.Authorization.Grants;

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
		                               SELECT DISTINCT UserId
		                               FROM Authz.Grant
		                               WHERE GrantType = @GrantType
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
