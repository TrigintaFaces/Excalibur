using Dapper;

using Excalibur.A3.Authorization.Grants.Domain.Model;
using Excalibur.DataAccess;

namespace Excalibur.A3.Postgres.RequestProviders.Authorization.Grants;

/// <summary>
///     Represents a query to save a new or updated grant in the database.
/// </summary>
public class SaveGrantRequest : DataRequest<int>
{
	/// <summary>
	///     Initializes a new instance of the <see cref="SaveGrantRequest" /> class.
	/// </summary>
	/// <param name="grant"> The grant object containing the details to save. </param>
	/// <param name="cancellationToken"> The cancellation token for the query. </param>
	/// <exception cref="ArgumentNullException"> Thrown if the <paramref name="grant" /> is null. </exception>
	public SaveGrantRequest(Grant grant, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(grant);

		const string CommandText = """
		                           INSERT INTO authz."grant" (
		                               user_id,
		                               full_name,
		                               tenant_id,
		                               grant_type,
		                               qualifier,
		                               expires_on,
		                               granted_by,
		                               granted_on
		                           ) VALUES (
		                               @UserId,
		                               @FullName,
		                               @TenantId,
		                               @GrantType,
		                               @Qualifier,
		                               @ExpiresOn::timestamptz,
		                               @GrantedBy,
		                               @GrantedOn::timestamptz
		                           );
		                           """;

		Command = CreateCommand(
			CommandText,
			new DynamicParameters(new
			{
				grant.UserId,
				grant.FullName,
				grant.Scope.TenantId,
				grant.Scope.GrantType,
				grant.Scope.Qualifier,
				grant.ExpiresOn,
				grant.GrantedBy,
				grant.GrantedOn
			}),
			commandTimeout: DbTimeouts.RegularTimeoutSeconds,
			cancellationToken: cancellationToken);

		ResolveAsync = async connection => await connection.ExecuteAsync(Command).ConfigureAwait(false);
	}
}
