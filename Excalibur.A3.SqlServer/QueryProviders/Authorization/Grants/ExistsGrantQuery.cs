using Dapper;

using Excalibur.DataAccess;

namespace Excalibur.A3.SqlServer.QueryProviders.Authorization.Grants;

/// <summary>
///     Represents a query to check if a grant exists in the database.
/// </summary>
public class ExistsGrantQuery : DataQuery<bool>
{
	/// <summary>
	///     Initializes a new instance of the <see cref="ExistsGrantQuery" /> class.
	/// </summary>
	/// <param name="userId"> The ID of the user associated with the grant. </param>
	/// <param name="tenantId"> The tenant ID associated with the grant. </param>
	/// <param name="grantType"> The type of the grant. </param>
	/// <param name="qualifier"> The qualifier of the grant. </param>
	/// <param name="cancellationToken"> The cancellation token for the query. </param>
	public ExistsGrantQuery(string userId, string tenantId, string grantType, string qualifier, CancellationToken cancellationToken)
	{
		const string CommandText = """
		                           SELECT CASE
		                             WHEN EXISTS (
		                                SELECT 1
		                                FROM Authz.Grant
		                                WHERE UserId = @UserId
		                                AND TenantId = @TenantId
		                                AND GrantType = @GrantType
		                                AND Qualifier = @Qualifier
		                                AND ISNULL(ExpiresOn, '9999-12-31') > GETUTCDATE()
		                             )
		                             THEN CAST(1 AS BIT)
		                             ELSE CAST(0 AS BIT)
		                           END;
		                           """;

		Command = CreateCommand(
			CommandText,
			new DynamicParameters(new { UserId = userId, TenantId = tenantId, GrantType = grantType, Qualifier = qualifier }),
			DbTimeouts.RegularTimeoutSeconds,
			cancellationToken);

		Resolve = async connection => await connection.ExecuteScalarAsync<bool>(Command).ConfigureAwait(false);
	}
}
