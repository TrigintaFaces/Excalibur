using Dapper;

using Excalibur.DataAccess;

namespace Excalibur.A3.SqlServer.QueryProviders.Authorization.Grants;

/// <summary>
///     Represents a query to delete all activity group grants of a specific grant type.
/// </summary>
public class DeleteAllActivityGroupGrantsQuery : DataQuery<int>
{
	/// <summary>
	///     Initializes a new instance of the <see cref="DeleteAllActivityGroupGrantsQuery" /> class.
	/// </summary>
	/// <param name="grantType"> The type of the grant to delete. </param>
	/// <param name="cancellationToken"> The cancellation token for the query. </param>
	public DeleteAllActivityGroupGrantsQuery(string grantType, CancellationToken cancellationToken = default)
	{
		const string CommandText = """
		                               DELETE FROM Authz.Grant
		                               WHERE GrantType = @GrantType
		                           """;

		Command = CreateCommand(
			CommandText,
			new DynamicParameters(new { GrantType = grantType }),
			DbTimeouts.RegularTimeoutSeconds,
			cancellationToken
		);

		Resolve = async connection => await connection.ExecuteAsync(Command).ConfigureAwait(false);
	}
}
