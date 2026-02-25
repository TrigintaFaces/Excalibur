// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Dapper;

using Excalibur.Data.Abstractions;

namespace Excalibur.Data.SqlServer.RequestProviders;

/// <summary>
/// Represents a query to retrieve a distinct list of user IDs associated with a specific grant type.
/// </summary>
public sealed class GetDistinctActivityGroupGrantUserIdsRequest : DataRequest<IEnumerable<string>>
{
	/// <summary>
	/// Initializes a new instance of the <see cref="GetDistinctActivityGroupGrantUserIdsRequest" /> class.
	/// </summary>
	/// <param name="grantType"> The grant type to filter the user IDs. </param>
	/// <param name="cancellationToken"> The cancellation token for the query. </param>
	public GetDistinctActivityGroupGrantUserIdsRequest(string grantType, CancellationToken cancellationToken)
	{
		const string CommandText = """
		                                SELECT DISTINCT UserId
		                                FROM Authz.Grant
		                                WHERE GrantType = @GrantType
		                           """;

		Command = CreateCommand(
			CommandText,
			parameters: new DynamicParameters(new { GrantType = grantType }),
			commandTimeout: DbTimeouts.RegularTimeoutSeconds,
			cancellationToken: cancellationToken);

		ResolveAsync = async connection => await connection.QueryAsync<string>(Command).ConfigureAwait(false);
	}
}
