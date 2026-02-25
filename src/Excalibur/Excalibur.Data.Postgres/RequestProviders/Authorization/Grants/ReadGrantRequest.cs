// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Dapper;

using Excalibur.Data.Abstractions;

using DomainGrant = Excalibur.A3.Abstractions.Authorization.Grant;

namespace Excalibur.Data.Postgres.RequestProviders;

/// <summary>
/// Represents a query to retrieve a specific grant based on user, tenant, grant type, and qualifier.
/// </summary>
public sealed class ReadGrantRequest : DataRequest<DomainGrant?>
{
	/// <summary>
	/// Initializes a new instance of the <see cref="ReadGrantRequest" /> class.
	/// </summary>
	/// <param name="userId"> The ID of the user associated with the grant. </param>
	/// <param name="tenantId"> The tenant ID associated with the grant. </param>
	/// <param name="grantType"> The type of the grant. </param>
	/// <param name="qualifier"> The qualifier of the grant. </param>
	/// <param name="cancellationToken"> The cancellation token for the query. </param>
	public ReadGrantRequest(string userId, string tenantId, string grantType, string qualifier, CancellationToken cancellationToken)
	{
		const string CommandText = """
		                           SELECT *
		                           FROM authz.grant
		                           WHERE user_id = @UserId
		                           AND tenant_id = @TenantId
		                           AND grant_type = @GrantType
		                           AND qualifier = @Qualifier;
		""";

		Command = CreateCommand(
			CommandText,
			new DynamicParameters(new { UserId = userId, TenantId = tenantId, GrantType = grantType, Qualifier = qualifier }),
			commandTimeout: DbTimeouts.RegularTimeoutSeconds,
			cancellationToken: cancellationToken);

		ResolveAsync = async connection =>
		{
			var grant = await connection.QuerySingleOrDefaultAsync<GrantData>(Command).ConfigureAwait(false);

			return grant != null
				? new DomainGrant(grant.UserId, grant.FullName, grant.TenantId, grant.GrantType, grant.Qualifier, grant.ExpiresOn,
					grant.GrantedBy, grant.GrantedOn!.Value)
				: null;
		};
	}
}
