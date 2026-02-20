// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Dapper;

using Excalibur.Data.Abstractions;

namespace Excalibur.Data.SqlServer.RequestProviders;

/// <summary>
/// Represents a query to delete all activity group grants of a specific grant type.
/// </summary>
public sealed class DeleteAllActivityGroupGrantsRequest : DataRequest<int>
{
	/// <summary>
	/// Initializes a new instance of the <see cref="DeleteAllActivityGroupGrantsRequest" /> class.
	/// </summary>
	/// <param name="grantType"> The type of the grant to delete. </param>
	/// <param name="cancellationToken"> The cancellation token for the query. </param>
	public DeleteAllActivityGroupGrantsRequest(string grantType, CancellationToken cancellationToken)
	{
		const string CommandText = """
		                                 DELETE FROM Authz.Grant
		                                 WHERE GrantType = @GrantType
		                           """;

		Command = CreateCommand(
			CommandText,
			parameters: new DynamicParameters(new { GrantType = grantType }),
			commandTimeout: DbTimeouts.RegularTimeoutSeconds,
			cancellationToken: cancellationToken);

		ResolveAsync = async connection => await connection.ExecuteAsync(Command).ConfigureAwait(false);
	}
}
