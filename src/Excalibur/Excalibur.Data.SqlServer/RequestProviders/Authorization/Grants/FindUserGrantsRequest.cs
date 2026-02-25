// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Dapper;

using Excalibur.Data.Abstractions;

namespace Excalibur.Data.SqlServer.RequestProviders;

/// <summary>
/// Represents a data request to find authorization grants for a specific user.
/// </summary>
public sealed class FindUserGrantsRequest : DataRequest<Dictionary<string, object>>
{
	/// <summary>
	/// Initializes a new instance of the <see cref="FindUserGrantsRequest" /> class.
	/// </summary>
	/// <param name="userId"> The unique identifier of the user to find grants for. </param>
	/// <param name="cancellationToken"> A token to observe while waiting for the task to complete. </param>
	public FindUserGrantsRequest(string userId, CancellationToken cancellationToken)
	{
		const string CommandText = """
		                                SELECT TenantId, GrantType, Qualifier, ExpiresOn
		                                FROM Authz.Grant
		                                WHERE UserId = @UserId;
		                           """;

		var parameters = new DynamicParameters(new { UserId = userId });

		Command = CreateCommand(CommandText, parameters: parameters, commandTimeout: DbTimeouts.RegularTimeoutSeconds,
			cancellationToken: cancellationToken);

		ResolveAsync = async connection =>
		{
			var grants = await connection
				.QueryAsync<(string TenantId, string GrantType, string Qualifier, DateTimeOffset? ExpiresOn)>(Command)
				.ConfigureAwait(false);

			return grants.ToDictionary(
				grant => string.Join(":", grant.TenantId, grant.GrantType, grant.Qualifier),
				object (grant) => new Data(grant.ExpiresOn),
				StringComparer.Ordinal);
		};
	}

	/// <summary>
	/// Represents data associated with a user grant.
	/// </summary>
	internal sealed record Data
	{
		/// <summary>
		/// Initializes a new instance of the <see cref="Data" /> class. Initializes a new instance of the <see cref="Data" /> record.
		/// </summary>
		/// <param name="expiresOn"> The expiration date of the grant, if applicable. </param>
		public Data(DateTimeOffset? expiresOn) => ExpiresOn = expiresOn?.ToUniversalTime().Ticks;

		/// <summary>
		/// Gets the expiration date of the grant, represented as ticks since the Unix epoch.
		/// </summary>
		/// <value> The expiration date of the grant, represented as ticks since the Unix epoch. </value>
		public long? ExpiresOn { get; }
	}
}
