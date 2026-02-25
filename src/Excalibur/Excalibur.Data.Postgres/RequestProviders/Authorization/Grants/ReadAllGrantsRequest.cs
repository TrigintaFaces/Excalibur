// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Dapper;

using Excalibur.Data.Abstractions;

using DomainGrant = Excalibur.A3.Abstractions.Authorization.Grant;

namespace Excalibur.Data.Postgres.RequestProviders;

/// <summary>
/// Represents a query to retrieve all grants for a specific user.
/// </summary>
public sealed class ReadAllGrantsRequest : DataRequest<IEnumerable<DomainGrant>>
{
	/// <summary>
	/// Initializes a new instance of the <see cref="ReadAllGrantsRequest" /> class.
	/// </summary>
	/// <param name="userId"> The ID of the user for whom grants are to be retrieved. </param>
	/// <param name="cancellationToken"> The cancellation token for the query. </param>
	public ReadAllGrantsRequest(string userId, CancellationToken cancellationToken)
	{
		const string CommandText = """
		                           SELECT *
		                           FROM authz.grant
		                           WHERE user_id = @UserId;
		""";

		Command = CreateCommand(
			CommandText,
			new DynamicParameters(new { UserId = userId }),
			commandTimeout: DbTimeouts.RegularTimeoutSeconds,
			cancellationToken: cancellationToken);

		ResolveAsync = async connection =>
		{
			var grants = await connection.QueryAsync<GrantData>(Command).ConfigureAwait(false);

			return grants.Select(g => new DomainGrant(
				g.UserId, g.FullName, g.TenantId, g.GrantType, g.Qualifier, g.ExpiresOn, g.GrantedBy, g.GrantedOn!.Value));
		};
	}
}
