using Dapper;

using Excalibur.DataAccess;

namespace Excalibur.A3.Postgres.RequestProviders.Authorization.Grants;

/// <summary>
///     Represents a query to retrieve a distinct list of user IDs associated with a specific grant type.
/// </summary>
public class GetDistinctActivityGroupGrantUserIdsRequest : DataRequest<IEnumerable<string>>
{
	/// <summary>
	///     Initializes a new instance of the <see cref="GetDistinctActivityGroupGrantUserIdsRequest" /> class.
	/// </summary>
	/// <param name="grantType"> The grant type to filter the user IDs. </param>
	/// <param name="cancellationToken"> The cancellation token for the query. </param>
	public GetDistinctActivityGroupGrantUserIdsRequest(string grantType, CancellationToken cancellationToken = default)
	{
		const string CommandText = """
		                               SELECT DISTINCT user_id
		                               FROM Authz.grant
		                               WHERE grant_type = @GrantType
		                           """;

		Command = CreateCommand(
			CommandText,
			new DynamicParameters(new { GrantType = grantType }),
			commandTimeout: DbTimeouts.RegularTimeoutSeconds,
			cancellationToken: cancellationToken);

		ResolveAsync = async connection => await connection.QueryAsync<string>(Command).ConfigureAwait(false);
	}
}
