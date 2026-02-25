// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Dapper;

using Excalibur.Data.Abstractions;

namespace Excalibur.Data.SqlServer.RequestProviders;

/// <summary>
/// Represents a query to check if a grant exists in the database.
/// </summary>
public sealed class ExistsGrantRequest : DataRequest<bool>
{
	/// <summary>
	/// Initializes a new instance of the <see cref="ExistsGrantRequest" /> class.
	/// </summary>
	/// <param name="userId"> The ID of the user associated with the grant. </param>
	/// <param name="tenantId"> The tenant ID associated with the grant. </param>
	/// <param name="grantType"> The type of the grant. </param>
	/// <param name="qualifier"> The qualifier of the grant. </param>
	/// <param name="cancellationToken"> The cancellation token for the query. </param>
	public ExistsGrantRequest(string userId, string tenantId, string grantType, string qualifier, CancellationToken cancellationToken)
	{
		const string CommandText = """
		                                 SELECT
		                                 CASE
		                                    WHEN EXISTS (
		                                     SELECT 1
		                                     FROM Authz.Grant
		                                     WHERE UserId = @UserId
		                                     AND TenantId = @TenantId
		                                     AND GrantType = @GrantType
		                                     AND Qualifier = @Qualifier
		                                     AND ISNULL(ExpiresOn, '9999-12-31') > GETUTCDATE()
		                                 )
		                                 THEN CAST(1 AS BIT)
		                                 ELSE CAST(0 AS BIT)
		                                 END;
		                           """;

		Command = CreateCommand(
			CommandText,
			parameters: new DynamicParameters(new { UserId = userId, TenantId = tenantId, GrantType = grantType, Qualifier = qualifier }),
			commandTimeout: DbTimeouts.RegularTimeoutSeconds,
			cancellationToken: cancellationToken);

		ResolveAsync = async connection => await connection.ExecuteScalarAsync<bool>(Command).ConfigureAwait(false);
	}
}
