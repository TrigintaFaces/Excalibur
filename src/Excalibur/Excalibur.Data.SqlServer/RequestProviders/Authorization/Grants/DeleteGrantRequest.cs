// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Dapper;

using Excalibur.Data.Abstractions;

namespace Excalibur.Data.SqlServer.RequestProviders;

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
		DateTimeOffset? revokedOn,
		CancellationToken cancellationToken)
	{
		const string CommandText = """
		                           INSERT INTO Authz.GrantHistory (
		                           	UserId,
		                           	FullName,
		                           	TenantId,
		                           	GrantType,
		                           	Qualifier,
		                           	ExpiresOn,
		                           	GrantedBy,
		                           	GrantedOn,
		                           	RevokedBy,
		                           	RevokedOn
		                           )
		                           SELECT
		                           	UserId,
		                           	FullName,
		                           	TenantId,
		                           	GrantType,
		                           	Qualifier,
		                           	ExpiresOn,
		                           	GrantedBy,
		                           	GrantedOn,
		                           	@RevokedBy AS RevokedBy,
		                           	@RevokedOn AS RevokedOn
		                           FROM Authz.Grant
		                           WHERE UserId = @UserId AND TenantId = @TenantId AND GrantType = @GrantType AND Qualifier = @Qualifier;

		                           DELETE FROM Authz.Grant
		                           WHERE UserId = @UserId AND TenantId = @TenantId AND GrantType = @GrantType AND Qualifier = @Qualifier;
		                           """;

		Command = CreateCommand(
			CommandText,
			parameters: new DynamicParameters(new
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
