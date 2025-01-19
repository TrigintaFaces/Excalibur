using Dapper;

using Excalibur.DataAccess;

namespace Excalibur.A3.SqlServer.QueryProviders.Authorization.Grants;

/// <summary>
///     Represents a query to delete activity group grants for a specific user by user ID and grant type.
/// </summary>
public class DeleteActivityGroupGrantsByUserIdQuery : DataQuery<int>
{
	/// <summary>
	///     Initializes a new instance of the <see cref="DeleteActivityGroupGrantsByUserIdQuery" /> class.
	/// </summary>
	/// <param name="userId"> The ID of the user whose grants are to be deleted. </param>
	/// <param name="grantType"> The type of grant to delete. </param>
	/// <param name="cancellationToken"> The cancellation token for the query. </param>
	public DeleteActivityGroupGrantsByUserIdQuery(string userId, string grantType, CancellationToken cancellationToken)
	{
		const string CommandText = """
		                               DELETE FROM Authz.Grant
		                               WHERE UserId = @UserId
		                                 AND GrantType = @GrantType
		                           """;

		Command = CreateCommand(
			CommandText,
			new DynamicParameters(new { UserId = userId, GrantType = grantType }),
			DbTimeouts.RegularTimeoutSeconds, // Timeout in seconds
			cancellationToken
		);

		Resolve = async connection => await connection.ExecuteAsync(Command).ConfigureAwait(false);
	}
}
