// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Dapper;

using Excalibur.Data.Abstractions;

namespace Excalibur.Data.SqlServer.RequestProviders;

/// <summary>
/// Represents a query for creating a new activity group in the database.
/// </summary>
public sealed class CreateActivityGroupRequest : DataRequest<int>
{
	/// <summary>
	/// Initializes a new instance of the <see cref="CreateActivityGroupRequest" /> class.
	/// </summary>
	/// <param name="tenantId"> The tenant ID associated with the activity group (optional). </param>
	/// <param name="name"> The name of the activity group. </param>
	/// <param name="activityName"> The name of the activity within the group. </param>
	/// <param name="cancellationToken"> The cancellation token for the query. </param>
	public CreateActivityGroupRequest(string? tenantId, string name, string activityName, CancellationToken cancellationToken)
	{
		const string CommandText = """
		                              INSERT INTO authz.ActivityGroup (
		                               TenantId,
		                               Name,
		                               ActivityName
		                              ) VALUES (
		                               @TenantId,
		                               @ActivityGroupName,
		                               @ActivityName
		                              );
		                           """;

		Command = CreateCommand(
			CommandText,
			parameters: new DynamicParameters(new { TenantId = tenantId, ActivityGroupName = name, ActivityName = activityName }),
			commandTimeout: DbTimeouts.RegularTimeoutSeconds,
			cancellationToken: cancellationToken);

		ResolveAsync = async connection => await connection.ExecuteAsync(Command).ConfigureAwait(false);
	}
}
