using Dapper;

using Excalibur.DataAccess;

namespace Excalibur.A3.SqlServer.QueryProviders.Authorization.Grants;

/// <summary>
///     Represents a query to insert a new activity group grant into the database.
/// </summary>
public class InsertActivityGroupGrantQuery : DataQuery<int>
{
	/// <summary>
	///     Initializes a new instance of the <see cref="InsertActivityGroupGrantQuery" /> class.
	/// </summary>
	/// <param name="userId"> The ID of the user associated with the grant. </param>
	/// <param name="fullName"> The full name of the user or entity associated with the grant. </param>
	/// <param name="tenantId"> The tenant ID associated with the grant (optional). </param>
	/// <param name="grantType"> The type of the grant. </param>
	/// <param name="qualifier"> The qualifier for the grant, often specifying a specific scope or resource. </param>
	/// <param name="expiresOn"> The expiration date of the grant (optional). </param>
	/// <param name="grantedBy"> The identifier of the entity granting the permission. </param>
	/// <param name="cancellationToken"> The cancellation token for the query. </param>
	public InsertActivityGroupGrantQuery(string userId, string fullName, string? tenantId, string grantType, string qualifier,
		DateTimeOffset? expiresOn, string grantedBy, CancellationToken cancellationToken)
	{
		const string CommandText = """
		                               INSERT INTO Authz.Grant (UserId, FullName, TenantId, GrantType, Qualifier, ExpiresOn, GrantedBy, GrantedOn)
		                               VALUES (@UserId, @FullName, @TenantId, @GrantType, @Qualifier, @ExpiresOn, @GrantedBy, GETUTCDATE())
		                           """;

		Command = CreateCommand(
			CommandText,
			new DynamicParameters(new
			{
				UserId = userId,
				FullName = fullName,
				TenantId = tenantId,
				GrantType = grantType,
				Qualifier = qualifier,
				ExpiresOn = expiresOn,
				GrantedBy = grantedBy
			}),
			DbTimeouts.RegularTimeoutSeconds,
			cancellationToken
		);

		Resolve = async connection => await connection.ExecuteAsync(Command).ConfigureAwait(false);
	}
}
