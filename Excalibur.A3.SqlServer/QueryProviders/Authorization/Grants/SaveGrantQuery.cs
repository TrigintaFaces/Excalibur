using Dapper;

using Excalibur.A3.Authorization.Grants.Domain.Model;
using Excalibur.DataAccess;

namespace Excalibur.A3.SqlServer.QueryProviders.Authorization.Grants;

/// <summary>
///     Represents a query to save a new or updated grant in the database.
/// </summary>
public class SaveGrantQuery : DataQuery<int>
{
	/// <summary>
	///     Initializes a new instance of the <see cref="SaveGrantQuery" /> class.
	/// </summary>
	/// <param name="grant"> The grant object containing the details to save. </param>
	/// <param name="cancellationToken"> The cancellation token for the query. </param>
	/// <exception cref="ArgumentNullException"> Thrown if the <paramref name="grant" /> is null. </exception>
	public SaveGrantQuery(Grant grant, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(grant);

		const string CommandText = """
		                           INSERT INTO Authz.Grant (
		                              UserId,
		                              FullName,
		                              TenantId,
		                              GrantType,
		                              Qualifier,
		                              ExpiresOn,
		                              GrantedBy,
		                              GrantedOn
		                           ) VALUES (
		                              @UserId,
		                              @FullName,
		                              @TenantId,
		                              @GrantType,
		                              @Qualifier,
		                              @ExpiresOn,
		                              @GrantedBy,
		                              @GrantedOn
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
			DbTimeouts.RegularTimeoutSeconds,
			cancellationToken);

		Resolve = async connection => await connection.ExecuteAsync(Command).ConfigureAwait(false);
	}
}
