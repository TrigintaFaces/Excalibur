using Dapper;

using Excalibur.DataAccess;

namespace Excalibur.A3.Postgres.RequestProviders.Authorization.Grants;

/// <summary>
///     Represents a query to check if a grant exists in the database.
/// </summary>
public class ExistsGrantRequest : DataRequest<bool>
{
	/// <summary>
	///     Initializes a new instance of the <see cref="ExistsGrantRequest" /> class.
	/// </summary>
	/// <param name="userId"> The ID of the user associated with the grant. </param>
	/// <param name="tenantId"> The tenant ID associated with the grant. </param>
	/// <param name="grantType"> The type of the grant. </param>
	/// <param name="qualifier"> The qualifier of the grant. </param>
	/// <param name="cancellationToken"> The cancellation token for the query. </param>
	public ExistsGrantRequest(string userId, string tenantId, string grantType, string qualifier, CancellationToken cancellationToken)
	{
		const string CommandText = """
		                           SELECT EXISTS (
		                               SELECT 1
		                               FROM authz.grant
		                               WHERE user_id = @UserId
		                               AND tenant_id = @TenantId
		                               AND grant_type = @GrantType
		                               AND qualifier = @Qualifier
		                               AND COALESCE(expires_on, 'infinity') > now() at time zone 'utc'
		                           );
		                           """;

		Command = CreateCommand(
			CommandText,
			new DynamicParameters(new { UserId = userId, TenantId = tenantId, GrantType = grantType, Qualifier = qualifier }),
			commandTimeout: DbTimeouts.RegularTimeoutSeconds,
			cancellationToken: cancellationToken);

		ResolveAsync = async connection => await connection.ExecuteScalarAsync<bool>(Command).ConfigureAwait(false);
	}
}
