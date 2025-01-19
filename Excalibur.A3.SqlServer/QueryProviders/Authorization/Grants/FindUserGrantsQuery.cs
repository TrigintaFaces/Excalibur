using Dapper;

using Excalibur.DataAccess;

namespace Excalibur.A3.SqlServer.QueryProviders.Authorization.Grants;

public class FindUserGrantsQuery : DataQuery<Dictionary<string, object>>
{
	public FindUserGrantsQuery(string userId, CancellationToken cancellationToken)
	{
		const string CommandText = """
		                           SELECT TenantId, GrantType, Qualifier, ExpiresOn
		                           FROM Authz.Grant
		                           WHERE UserId = @UserId;
		                           """;

		Parameters = new DynamicParameters(new { UserId = userId });

		Command = CreateCommand(CommandText, Parameters, DbTimeouts.RegularTimeoutSeconds, cancellationToken);

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
