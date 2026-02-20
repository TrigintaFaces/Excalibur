// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Dapper;

using Excalibur.A3.Abstractions.Authorization;
using Excalibur.Data.Abstractions;

namespace Excalibur.Data.SqlServer.RequestProviders;

/// <summary>
/// Represents a query to retrieve matching grants based on user ID, tenant ID, grant type, and qualifier.
/// </summary>
public sealed class MatchingGrantsRequest : DataRequest<IEnumerable<Grant>>
{
	/// <summary>
	/// Initializes a new instance of the <see cref="MatchingGrantsRequest" /> class.
	/// </summary>
	/// <param name="userId"> The ID of the user to filter grants by, or <c> null </c> to match all users. </param>
	/// <param name="tenantId"> The tenant ID to filter grants by. </param>
	/// <param name="grantType"> The type of grants to filter by. </param>
	/// <param name="qualifier"> The qualifier of the grants to filter by. </param>
	/// <param name="cancellationToken"> The cancellation token for the query. </param>
	public MatchingGrantsRequest(string? userId, string tenantId, string grantType, string qualifier, CancellationToken cancellationToken)
	{
		const string CommandText = """
		                                SELECT *
		                                FROM Authz.Grant
		                                WHERE UserId LIKE ISNULL(@UserId, '%')
		                                AND TenantId LIKE @TenantId
		                                AND GrantType LIKE @GrantType
		                                AND Qualifier LIKE @Qualifier;
		                           """;

		Command = CreateCommand(
			CommandText,
			parameters: new DynamicParameters(new
			{
				UserId = userId ?? "%",
				TenantId = tenantId,
				GrantType = grantType,
				Qualifier = qualifier,
			}),
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
