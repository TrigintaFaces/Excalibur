using Dapper;

using Excalibur.DataAccess;

namespace Excalibur.A3.SqlServer.RequestProviders.Authorization.Grants;

/// <summary>
///     Represents a query to delete all activity group grants of a specific grant type.
/// </summary>
public class DeleteAllActivityGroupGrantsRequest : DataRequest<int>
{
	/// <summary>
	///     Initializes a new instance of the <see cref="DeleteAllActivityGroupGrantsRequest" /> class.
	/// </summary>
	/// <param name="grantType"> The type of the grant to delete. </param>
	/// <param name="cancellationToken"> The cancellation token for the query. </param>
	public DeleteAllActivityGroupGrantsRequest(string grantType, CancellationToken cancellationToken = default)
	{
		const string CommandText = """
		                               DELETE FROM Authz.Grant
		                               WHERE GrantType = @GrantType
		                           """;

		Command = CreateCommand(
			CommandText,
			new DynamicParameters(new { GrantType = grantType }),
			commandTimeout: DbTimeouts.RegularTimeoutSeconds,
			cancellationToken: cancellationToken);

		ResolveAsync = async connection => await connection.ExecuteAsync(Command).ConfigureAwait(false);
	}
}
