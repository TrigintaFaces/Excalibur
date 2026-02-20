// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Dapper;

using Excalibur.A3.Abstractions.Authorization;
using Excalibur.Data.Abstractions;

namespace Excalibur.Data.SqlServer.RequestProviders;

/// <summary>
/// Represents a query to save a new or updated grant in the database.
/// </summary>
public sealed class SaveGrantRequest : DataRequest<int>
{
	/// <summary>
	/// Initializes a new instance of the <see cref="SaveGrantRequest" /> class.
	/// </summary>
	/// <param name="grant"> The grant object containing the details to save. </param>
	/// <param name="cancellationToken"> The cancellation token for the query. </param>
	/// <exception cref="ArgumentNullException"> Thrown if the <paramref name="grant" /> is null. </exception>
	public SaveGrantRequest(Grant grant, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(grant);

		const string CommandText = """
		                           INSERT INTO Authz.Grant (
		                           		UserId,
		                           		FullName,
		                           		TenantId,
		                           		GrantType,
		                           		Qualifier,
		                           		ExpiresOn,
		                           		GrantedBy,
		                           	GrantedOn
		                           ) VALUES (
		                           		@UserId,
		                           		@FullName,
		                           		@TenantId,
		                           		@GrantType,
		                           		@Qualifier,
		                           		@ExpiresOn,
		                           		@GrantedBy,
		                           	@GrantedOn
		                           );
		                           """;

		Command = CreateCommand(
			CommandText,
			parameters: new DynamicParameters(new
			{
				grant.UserId,
				grant.FullName,
				grant.TenantId,
				grant.GrantType,
				grant.Qualifier,
				grant.ExpiresOn,
				grant.GrantedBy,
				grant.GrantedOn,
			}),
			commandTimeout: DbTimeouts.RegularTimeoutSeconds,
			cancellationToken: cancellationToken);

		ResolveAsync = async connection => await connection.ExecuteAsync(Command).ConfigureAwait(false);
	}
}
