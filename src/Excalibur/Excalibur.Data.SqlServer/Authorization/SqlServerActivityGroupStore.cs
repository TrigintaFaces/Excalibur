// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Data;

using Dapper;

using Excalibur.A3.Abstractions.Authorization;
using Excalibur.Data.Abstractions;

namespace Excalibur.Data.SqlServer.Authorization;

/// <summary>
/// SQL Server implementation of <see cref="IActivityGroupStore"/> using inline Dapper queries.
/// </summary>
public sealed class SqlServerActivityGroupStore : IActivityGroupStore
{
	private readonly IDbConnection _connection;

	/// <summary>
	/// Initializes a new instance of the <see cref="SqlServerActivityGroupStore"/> class.
	/// </summary>
	/// <param name="domainDb">The domain database connection provider.</param>
	public SqlServerActivityGroupStore(IDomainDb domainDb)
	{
		ArgumentNullException.ThrowIfNull(domainDb);
		_connection = domainDb.Connection;
	}

	/// <inheritdoc />
	public async Task<bool> ActivityGroupExistsAsync(string activityGroupName,
		CancellationToken cancellationToken)
	{
		const string sql = """
		                                          SELECT EXISTS
		                                          (
		                                            SELECT 1
		                                            FROM authz.ActivityGroup
		                                            WHERE Name=@activityGroupName
		                                          );
		                   """;

		return await _connection.ExecuteScalarAsync<bool>(
			new CommandDefinition(sql,
				new { activityGroupName },
				commandTimeout: DbTimeouts.RegularTimeoutSeconds,
				cancellationToken: cancellationToken)).ConfigureAwait(false);
	}

	/// <inheritdoc />
	public async Task<IReadOnlyDictionary<string, object>> FindActivityGroupsAsync(
		CancellationToken cancellationToken)
	{
		const string sql = """
		                        SELECT Name, TenantId, ActivityName
		                        FROM authz.ActivityGroup
		                   """;

		var activityGroups = await _connection
			.QueryAsync<(string Name, string TenantId, string ActivityName)>(
				new CommandDefinition(sql,
					commandTimeout: DbTimeouts.RegularTimeoutSeconds,
					cancellationToken: cancellationToken)).ConfigureAwait(false);

		return activityGroups
			.GroupBy(
				group => group.Name,
				group => new ActivityGroupActivity(group.TenantId, group.ActivityName),
				StringComparer.Ordinal)
			.ToDictionary(
				group => group.Key,
				object (group) => new ActivityGroupData([.. group]),
				StringComparer.Ordinal);
	}

	/// <inheritdoc />
	public async Task<int> DeleteAllActivityGroupsAsync(CancellationToken cancellationToken)
	{
		const string sql = "DELETE FROM authz.ActivityGroup";

		return await _connection.ExecuteAsync(
			new CommandDefinition(sql,
				commandTimeout: DbTimeouts.RegularTimeoutSeconds,
				cancellationToken: cancellationToken)).ConfigureAwait(false);
	}

	/// <inheritdoc />
	public async Task<int> CreateActivityGroupAsync(string? tenantId, string name,
		string activityName, CancellationToken cancellationToken)
	{
		const string sql = """
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

		return await _connection.ExecuteAsync(
			new CommandDefinition(sql,
				new { TenantId = tenantId, ActivityGroupName = name, ActivityName = activityName },
				commandTimeout: DbTimeouts.RegularTimeoutSeconds,
				cancellationToken: cancellationToken)).ConfigureAwait(false);
	}

	/// <inheritdoc />
	public object? GetService(Type serviceType)
	{
		ArgumentNullException.ThrowIfNull(serviceType);
		return null;
	}

	/// <summary>
	/// Data associated with an activity group for <see cref="FindActivityGroupsAsync"/> results.
	/// </summary>
	private sealed record ActivityGroupData
	{
		public ActivityGroupData(IList<ActivityGroupActivity> activities)
		{
			ArgumentNullException.ThrowIfNull(activities);
			Activities = activities;
		}

		public IList<ActivityGroupActivity> Activities { get; set; } = [];
	}

	/// <summary>
	/// An individual activity within an activity group.
	/// </summary>
	private sealed record ActivityGroupActivity(string TenantId, string ActivityName);
}
