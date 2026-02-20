// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Dapper;

using Excalibur.Data.Abstractions;

namespace Excalibur.Data.Postgres.RequestProviders;

/// <summary>
/// Represents a query for checking the existence of an activity group in the database.
/// </summary>
public sealed class ExistsActivityGroupRequest : DataRequest<bool>
{
	/// <summary>
	/// Initializes a new instance of the <see cref="ExistsActivityGroupRequest" /> class.
	/// </summary>
	/// <param name="activityGroupName"> The name of the activity group to check for existence. </param>
	/// <param name="cancellationToken"> The cancellation token for the query. </param>
	public ExistsActivityGroupRequest(string activityGroupName, CancellationToken cancellationToken)
	{
		const string CommandText = """
		                           SELECT EXISTS
		                           (
		                           SELECT 1
		                           FROM authz.activity_group
		                           WHERE name=@activityGroupName
		                           );
		""";

		Command = CreateCommand(
			CommandText,
			new DynamicParameters(new { activityGroupName }),
			commandTimeout: DbTimeouts.RegularTimeoutSeconds,
			cancellationToken: cancellationToken);

		ResolveAsync = async connection => await connection.ExecuteScalarAsync<bool>(Command).ConfigureAwait(false);
	}
}
