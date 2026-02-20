// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Dapper;

using Excalibur.Data.Abstractions;

namespace Excalibur.Data.Postgres.RequestProviders;

/// <summary>
/// Represents a query to insert a new activity group grant into the Postgres database.
/// </summary>
public sealed class InsertActivityGroupGrantRequest : DataRequest<int>
{
	/// <summary>
	/// Initializes a new instance of the <see cref="InsertActivityGroupGrantRequest" /> class.
	/// </summary>
	/// <param name="userId"> The ID of the user associated with the grant. </param>
	/// <param name="fullName"> The full name of the user or entity associated with the grant. </param>
	/// <param name="tenantId"> The tenant ID associated with the grant (optional). </param>
	/// <param name="grantType"> The type of the grant. </param>
	/// <param name="qualifier"> The qualifier for the grant, often specifying a specific scope or resource. </param>
	/// <param name="expiresOn"> The expiration date of the grant (optional). </param>
	/// <param name="grantedBy"> The identifier of the entity granting the permission. </param>
	/// <param name="cancellationToken"> The cancellation token for the query. </param>
	public InsertActivityGroupGrantRequest(string userId, string fullName, string? tenantId, string grantType, string qualifier,
		DateTimeOffset? expiresOn, string grantedBy, CancellationToken cancellationToken)
	{
		const string CommandText = """
		                           INSERT INTO "Authz"."Grant" ("UserId", "FullName", "TenantId", "GrantType", "Qualifier", "ExpiresOn", "GrantedBy", "GrantedOn")
		                           VALUES (@UserId, @FullName, @TenantId, @GrantType, @Qualifier, @ExpiresOn, @GrantedBy, NOW() AT TIME ZONE 'UTC')
		""";

		Command = CreateCommand(
			CommandText,
			new DynamicParameters(new
			{
				UserId = userId,
				FullName = fullName,
				TenantId = tenantId,
				GrantType = grantType,
				Qualifier = qualifier,
				ExpiresOn = expiresOn,
				GrantedBy = grantedBy,
			}),
			commandTimeout: DbTimeouts.RegularTimeoutSeconds,
			cancellationToken: cancellationToken);

		ResolveAsync = async connection => await connection.ExecuteAsync(Command).ConfigureAwait(false);
	}
}
