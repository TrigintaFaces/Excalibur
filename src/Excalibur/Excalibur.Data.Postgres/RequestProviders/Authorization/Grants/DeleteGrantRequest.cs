// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Dapper;

using Excalibur.Data.Abstractions;

namespace Excalibur.Data.Postgres.RequestProviders;

/// <summary>
/// Represents a query to delete a grant and archive it in the grant history.
/// </summary>
public sealed class DeleteGrantRequest : DataRequest<int>
{
	/// <summary>
	/// Initializes a new instance of the <see cref="DeleteGrantRequest" /> class.
	/// </summary>
	/// <param name="userId"> The ID of the user associated with the grant. </param>
	/// <param name="tenantId"> The tenant ID associated with the grant. </param>
	/// <param name="grantType"> The type of the grant. </param>
	/// <param name="qualifier"> The qualifier of the grant. </param>
	/// <param name="revokedBy"> The entity that revoked the grant (optional). </param>
	/// <param name="revokedOn"> The timestamp when the grant was revoked (optional). </param>
	/// <param name="cancellationToken"> The cancellation token for the query. </param>
	public DeleteGrantRequest(string userId, string tenantId, string grantType, string qualifier, string? revokedBy,
		DateTimeOffset? revokedOn, CancellationToken cancellationToken)
	{
		const string CommandText = """
		                           INSERT INTO authz.grant_history (
		                           user_id,
		                           full_name,
		                           tenant_id,
		                           grant_type,
		                           qualifier,
		                           expires_on,
		                           granted_by,
		                           granted_on,
		                           revoked_by,
		                           revoked_on
		                           )
		                           SELECT
		                           user_id,
		                           full_name,
		                           tenant_id,
		                           grant_type,
		                           qualifier,
		                           expires_on,
		                           granted_by,
		                           granted_on,
		                           @RevokedBy AS revoked_by,
		                           @RevokedOn::timestamptz AS revoked_on
		                           FROM authz.grant
		                           WHERE user_id = @UserId AND tenant_id = @TenantId AND grant_type = @GrantType AND qualifier = @Qualifier;

		                           DELETE FROM authz.grant WHERE user_id = @UserId AND tenant_id = @TenantId AND grant_type = @GrantType AND qualifier = @Qualifier;
		""";

		Command = CreateCommand(
			CommandText,
			new DynamicParameters(new
			{
				UserId = userId,
				TenantId = tenantId,
				GrantType = grantType,
				Qualifier = qualifier,
				RevokedBy = revokedBy,
				RevokedOn = revokedOn,
			}),
			commandTimeout: DbTimeouts.RegularTimeoutSeconds,
			cancellationToken: cancellationToken);

		ResolveAsync = async connection => await connection.ExecuteAsync(Command).ConfigureAwait(false);
	}
}
