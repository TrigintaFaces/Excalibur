using Dapper;

using Excalibur.A3.Authorization.Grants.Domain.Model;
using Excalibur.DataAccess;

namespace Excalibur.A3.Postgres.QueryProviders.Authorization.Grants;

/// <summary>
///     Represents a query to retrieve a specific grant based on user, tenant, grant type, and qualifier.
/// </summary>
public class ReadGrantQuery : DataQuery<Grant?>
{
	/// <summary>
	///     Initializes a new instance of the <see cref="ReadGrantQuery" /> class.
	/// </summary>
	/// <param name="userId"> The ID of the user associated with the grant. </param>
	/// <param name="tenantId"> The tenant ID associated with the grant. </param>
	/// <param name="grantType"> The type of the grant. </param>
	/// <param name="qualifier"> The qualifier of the grant. </param>
	/// <param name="cancellationToken"> The cancellation token for the query. </param>
	public ReadGrantQuery(string userId, string tenantId, string grantType, string qualifier, CancellationToken cancellationToken)
	{
		const string CommandText = """
		                           SELECT *
		                           FROM authz.grant
		                           WHERE user_id = @UserId
		                           AND tenant_id = @TenantId
		                           AND grant_type = @GrantType
		                           AND qualifier = @Qualifier;
		                           """;

		Command = CreateCommand(
			CommandText,
			new DynamicParameters(new { UserId = userId, TenantId = tenantId, GrantType = grantType, Qualifier = qualifier }),
			DbTimeouts.RegularTimeoutSeconds,
			cancellationToken);

		Resolve = async connection =>
		{
			var grant = await connection.QuerySingleOrDefaultAsync<GrantData>(Command).ConfigureAwait(false);

			return grant != null
				? new Grant(grant.UserId, grant.FullName, grant.TenantId, grant.GrantType, grant.Qualifier, grant.ExpiresOn,
					grant.GrantedBy, grant.GrantedOn!.Value)
				: null;
		};
	}
}
