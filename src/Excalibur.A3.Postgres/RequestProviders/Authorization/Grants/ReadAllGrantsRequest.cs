using Dapper;

using Excalibur.A3.Authorization.Grants.Domain.Model;
using Excalibur.DataAccess;

namespace Excalibur.A3.Postgres.RequestProviders.Authorization.Grants;

/// <summary>
///     Represents a query to retrieve all grants for a specific user.
/// </summary>
public class ReadAllGrantsRequest : DataRequest<IEnumerable<Grant>>
{
	/// <summary>
	///     Initializes a new instance of the <see cref="ReadAllGrantsRequest" /> class.
	/// </summary>
	/// <param name="userId"> The ID of the user for whom grants are to be retrieved. </param>
	/// <param name="cancellationToken"> The cancellation token for the query. </param>
	public ReadAllGrantsRequest(string userId, CancellationToken cancellationToken)
	{
		const string CommandText = """
		                           SELECT *
		                           FROM authz.grant
		                           WHERE user_id = @UserId;
		                           """;

		Command = CreateCommand(
			CommandText,
			new DynamicParameters(new { UserId = userId }),
			commandTimeout: DbTimeouts.RegularTimeoutSeconds,
			cancellationToken: cancellationToken);

		ResolveAsync = async connection =>
		{
			var grants = await connection.QueryAsync<GrantData>(Command).ConfigureAwait(false);

			return grants.Select(g => new Grant(
				g.UserId, g.FullName, g.TenantId, g.GrantType, g.Qualifier, g.ExpiresOn, g.GrantedBy, g.GrantedOn!.Value));
		};
	}
}
