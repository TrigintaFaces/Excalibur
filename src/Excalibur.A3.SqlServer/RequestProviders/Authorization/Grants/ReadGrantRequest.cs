using Dapper;

using Excalibur.A3.Authorization.Grants.Domain.Model;
using Excalibur.DataAccess;

namespace Excalibur.A3.SqlServer.RequestProviders.Authorization.Grants;

/// <summary>
///     Represents a query to retrieve a specific grant based on user, tenant, grant type, and qualifier.
/// </summary>
public class ReadGrantRequest : DataRequest<Grant?>
{
	/// <summary>
	///     Initializes a new instance of the <see cref="ReadGrantRequest" /> class.
	/// </summary>
	/// <param name="userId"> The ID of the user associated with the grant. </param>
	/// <param name="tenantId"> The tenant ID associated with the grant. </param>
	/// <param name="grantType"> The type of the grant. </param>
	/// <param name="qualifier"> The qualifier of the grant. </param>
	/// <param name="cancellationToken"> The cancellation token for the query. </param>
	public ReadGrantRequest(string userId, string tenantId, string grantType, string qualifier, CancellationToken cancellationToken)
	{
		const string CommandText = """
		                           SELECT *
		                           FROM Authz.Grant
		                           WHERE UserId = @UserId
		                           AND TenantId = @TenantId
		                           AND GrantType = @GrantType
		                           AND Qualifier = @Qualifier;
		                           """;

		Command = CreateCommand(
			CommandText,
			new DynamicParameters(new { UserId = userId, TenantId = tenantId, GrantType = grantType, Qualifier = qualifier }),
			commandTimeout: DbTimeouts.RegularTimeoutSeconds,
			cancellationToken: cancellationToken);

		ResolveAsync = async connection =>
		{
			var grant = await connection.QuerySingleOrDefaultAsync<GrantData>(Command).ConfigureAwait(false);

			return grant != null
				? new Grant(grant.UserId, grant.FullName, grant.TenantId, grant.GrantType, grant.Qualifier,
					grant.ExpiresOn, grant.GrantedBy, grant.GrantedOn!.Value)
				: null;
		};
	}
}
