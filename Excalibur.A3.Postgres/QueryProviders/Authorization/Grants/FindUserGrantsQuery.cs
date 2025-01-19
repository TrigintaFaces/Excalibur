using Dapper;

using Excalibur.DataAccess;

namespace Excalibur.A3.Postgres.QueryProviders.Authorization.Grants;

/// <summary>
///     Represents a query to retrieve all grants associated with a specific user.
/// </summary>
public class FindUserGrantsQuery : DataQuery<Dictionary<string, object>>
{
	/// <summary>
	///     Initializes a new instance of the <see cref="FindUserGrantsQuery" /> class.
	/// </summary>
	/// <param name="userId"> The ID of the user for whom grants are being retrieved. </param>
	/// <param name="cancellationToken"> The cancellation token for the query. </param>
	public FindUserGrantsQuery(string userId, CancellationToken cancellationToken)
	{
		const string CommandText = """
		                           SELECT tenant_id, grant_type, qualifier, expires_on::timestamptz
		                           from authz.grant
		                           WHERE user_id = @userId
		                           """;

		Command = CreateCommand(CommandText, new DynamicParameters(new { UserId = userId }), DbTimeouts.RegularTimeoutSeconds,
			cancellationToken);

		Resolve = async connection =>
		{
			var grants = await connection
				.QueryAsync<(string TenantId, string GrantType, string Qualifier, DateTimeOffset? ExpiresOn)>(Command)
				.ConfigureAwait(false);

			return grants.ToDictionary(
				grant => string.Join(":", grant.TenantId, grant.GrantType, grant.Qualifier),
				object (grant) => new Data(grant.ExpiresOn));
		};
	}

	/// <summary>
	///     Represents data associated with a user grant.
	/// </summary>
	internal sealed record Data
	{
		/// <summary>
		///     Initializes a new instance of the <see cref="Data" /> record.
		/// </summary>
		/// <param name="expiresOn"> The expiration date of the grant, if applicable. </param>
		public Data(DateTimeOffset? expiresOn) => ExpiresOn = expiresOn?.ToUniversalTime().Ticks;

		/// <summary>
		///     Gets the expiration date of the grant, represented as ticks since the Unix epoch.
		/// </summary>
		public long? ExpiresOn { get; }
	}
}
