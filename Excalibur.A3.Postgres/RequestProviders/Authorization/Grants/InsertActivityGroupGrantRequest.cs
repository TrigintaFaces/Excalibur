using Dapper;

using Excalibur.DataAccess;

namespace Excalibur.A3.Postgres.RequestProviders.Authorization.Grants;

/// <summary>
///     Represents a query to insert a new activity group grant into the database.
/// </summary>
public class InsertActivityGroupGrantRequest : DataRequest<int>
{
	/// <summary>
	///     Initializes a new instance of the <see cref="InsertActivityGroupGrantRequest" /> class.
	/// </summary>
	/// <param name="userId"> The ID of the user associated with the grant. </param>
	/// <param name="fullName"> The full name of the user or entity associated with the grant. </param>
	/// <param name="tenantId"> The tenant ID associated with the grant (optional). </param>
	/// <param name="grantType"> The type of the grant. </param>
	/// <param name="qualifier"> The qualifier for the grant, often specifying a specific scope or resource. </param>
	/// <param name="expiresOn"> The expiration date of the grant (optional). </param>
	/// <param name="grantedBy"> The identifier of the entity granting the permission. </param>
	/// <param name="cancellationToken"> The cancellation token for the query. </param>
	public InsertActivityGroupGrantRequest(string userId, string fullName, string? tenantId, string grantType, string qualifier,
		DateTimeOffset? expiresOn, string grantedBy, CancellationToken cancellationToken)
	{
		const string CommandText = """
		                           INSERT INTO Authz.grant (user_id, full_name, tenant_id, grant_type, qualifier, expires_on, granted_by, granted_on)
		                           VALUES (@UserId, @FullName, @TenantId, @GrantType, @Qualifier, @ExpiresOn, @GrantedBy, now() at time zone 'utc')
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
			commandTimeout: DbTimeouts.RegularTimeoutSeconds,
			cancellationToken: cancellationToken);

		ResolveAsync = async connection => await connection.ExecuteAsync(Command).ConfigureAwait(false);
	}
}
