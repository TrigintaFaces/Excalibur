// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Dapper;

using Excalibur.A3.Abstractions.Authorization;
using Excalibur.Data.Abstractions;

namespace Excalibur.Data.SqlServer.RequestProviders;

/// <summary>
/// Represents a query to retrieve all grants for a specific user.
/// </summary>
public sealed class ReadAllGrantsRequest : DataRequest<IEnumerable<Grant>>
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
		                                FROM Authz.Grant
		                                WHERE UserId = @UserId;
		                           """;

		Command = CreateCommand(
			CommandText,
			parameters: new DynamicParameters(new { UserId = userId }),
			commandTimeout: DbTimeouts.RegularTimeoutSeconds,
			cancellationToken: cancellationToken);

		ResolveAsync = async connection =>
		{
			var grants = await connection.QueryAsync<GrantData>(Command).ConfigureAwait(false);

			return grants.Select(g => new Grant(
				g.UserId, g.FullName, g.TenantId, g.GrantType, g.Qualifier, g.ExpiresOn, g.GrantedBy, g.GrantedOn!.Value));
		};
	}
}
