// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Dapper;

using Excalibur.Data.Abstractions;

namespace Excalibur.Data.Postgres.RequestProviders;

/// <summary>
/// Represents a query to retrieve activity groups and their associated activities from the database.
/// </summary>
public sealed class FindActivityGroupsRequest : DataRequest<Dictionary<string, object>>
{
	/// <summary>
	/// Initializes a new instance of the <see cref="FindActivityGroupsRequest" /> class.
	/// </summary>
	/// <param name="cancellationToken"> The cancellation token for the query. </param>
	public FindActivityGroupsRequest(CancellationToken cancellationToken)
	{
		const string CommandText = """
		                           SELECT name, tenant_id, activity_name
		                           FROM authz.activity_group
		""";

		Command = CreateCommand(CommandText, commandTimeout: DbTimeouts.RegularTimeoutSeconds, cancellationToken: cancellationToken);

		ResolveAsync = async connection =>
		{
			var activityGroups = await connection
				.QueryAsync<(string Name, string TenantId, string ActivityName)>(Command)
				.ConfigureAwait(false);

			return activityGroups
				.GroupBy(
					group => group.Name,
					group => new ActivityGroupActivity(group.TenantId, group.ActivityName), StringComparer.Ordinal)
				.ToDictionary(
					group => group.Key,
					object (group) => new Data([.. group]), StringComparer.Ordinal);
		};
	}

	/// <summary>
	/// Represents data associated with an activity group.
	/// </summary>
	internal sealed record Data
	{
		/// <summary>
		/// Initializes a new instance of the <see cref="Data"/> class.
		/// Initializes a new instance of the <see cref="Data" /> record.
		/// </summary>
		/// <param name="activities"> A list of activities associated with the group. </param>
		public Data(IList<ActivityGroupActivity> activities)
		{
			ArgumentNullException.ThrowIfNull(activities);

			Activities = activities;
		}

		/// <summary>
		/// Gets or sets the list of activities in the group.
		/// </summary>
		/// <value>
		/// The list of activities in the group.
		/// </value>
		public IList<ActivityGroupActivity> Activities { get; set; } = [];
	}

	/// <summary>
	/// Represents an individual activity within an activity group.
	/// </summary>
	/// <param name="TenantId"> The tenant ID associated with the activity. </param>
	/// <param name="ActivityName"> The name of the activity. </param>
	internal sealed record ActivityGroupActivity(string TenantId, string ActivityName);
}
