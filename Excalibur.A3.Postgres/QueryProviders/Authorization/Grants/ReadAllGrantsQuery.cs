using Dapper;

using Excalibur.A3.Authorization.Grants.Domain.Model;
using Excalibur.DataAccess;

namespace Excalibur.A3.Postgres.QueryProviders.Authorization.Grants;

/// <summary>
///     Represents a query to retrieve all grants for a specific user.
/// </summary>
public class ReadAllGrantsQuery : DataQuery<IEnumerable<Grant>>
{
	/// <summary>
	///     Initializes a new instance of the <see cref="ReadAllGrantsQuery" /> class.
	/// </summary>
	/// <param name="userId"> The ID of the user for whom grants are to be retrieved. </param>
	/// <param name="cancellationToken"> The cancellation token for the query. </param>
	public ReadAllGrantsQuery(string userId, CancellationToken cancellationToken)
	{
		const string CommandText = """
		                           SELECT *
		                           FROM authz.grant
		                           WHERE user_id = @UserId;
		                           """;

		Command = CreateCommand(
			CommandText,
			new DynamicParameters(new { UserId = userId }),
			DbTimeouts.RegularTimeoutSeconds,
			cancellationToken);

		Resolve = async connection =>
		{
			var grants = await connection.QueryAsync<GrantData>(Command).ConfigureAwait(false);

			return grants.Select(g => new Grant(
				g.UserId, g.FullName, g.TenantId, g.GrantType, g.Qualifier, g.ExpiresOn, g.GrantedBy, g.GrantedOn!.Value));
		};
	}
}
