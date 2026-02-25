// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Dapper;

using Excalibur.Data.Abstractions;

namespace Excalibur.Data.SqlServer.RequestProviders;

/// <summary>
/// Represents a query for deleting all activity groups from the database.
/// </summary>
public sealed class DeleteAllActivityGroupsRequest : DataRequest<int>
{
	/// <summary>
	/// Initializes a new instance of the <see cref="DeleteAllActivityGroupsRequest" /> class.
	/// </summary>
	/// <param name="cancellationToken"> The cancellation token for the query. </param>
	public DeleteAllActivityGroupsRequest(CancellationToken cancellationToken)
	{
		const string CommandText = "DELETE FROM authz.ActivityGroup";

		Command = CreateCommand(CommandText, commandTimeout: DbTimeouts.RegularTimeoutSeconds, cancellationToken: cancellationToken);
		ResolveAsync = async connection => await connection.ExecuteAsync(Command).ConfigureAwait(false);
	}
}
