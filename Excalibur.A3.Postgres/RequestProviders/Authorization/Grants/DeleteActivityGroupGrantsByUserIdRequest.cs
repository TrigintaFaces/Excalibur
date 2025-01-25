using Dapper;

using Excalibur.DataAccess;

namespace Excalibur.A3.Postgres.RequestProviders.Authorization.Grants;

/// <summary>
///     Represents a query to delete activity group grants for a specific user by user ID and grant type.
/// </summary>
public class DeleteActivityGroupGrantsByUserIdRequest : DataRequest<int>
{
	/// <summary>
	///     Initializes a new instance of the <see cref="DeleteActivityGroupGrantsByUserIdRequest" /> class.
	/// </summary>
	/// <param name="userId"> The ID of the user whose grants are to be deleted. </param>
	/// <param name="grantType"> The type of grant to delete. </param>
	/// <param name="cancellationToken"> The cancellation token for the query. </param>
	public DeleteActivityGroupGrantsByUserIdRequest(string userId, string grantType, CancellationToken cancellationToken)
	{
		const string CommandText = """
		                               DELETE FROM Authz.grant
		                               WHERE user_id = @UserId
		                                 AND grant_type = @GrantType
		                           """;
		Command = CreateCommand(
			CommandText,
			new DynamicParameters(new { UserId = userId, GrantType = grantType }),
			commandTimeout: DbTimeouts.RegularTimeoutSeconds,
			cancellationToken: cancellationToken);

		ResolveAsync = async connection => await connection.ExecuteAsync(Command).ConfigureAwait(false);
	}
}
