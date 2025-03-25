using Dapper;

using Excalibur.DataAccess;

namespace Excalibur.A3.SqlServer.RequestProviders.Authorization.ActivityGroups;

/// <summary>
///     Represents a query to retrieve activity groups and their associated activities from the database.
/// </summary>
public class FindActivityGroupsRequest : DataRequest<Dictionary<string, object>>
{
	/// <summary>
	///     Initializes a new instance of the <see cref="FindActivityGroupsRequest" /> class.
	/// </summary>
	/// <param name="cancellationToken"> The cancellation token for the query. </param>
	public FindActivityGroupsRequest(CancellationToken cancellationToken)
	{
		const string CommandText = """
		                           SELECT Name, TenantId, ActivityName
		                           FROM authz.ActivityGroup
		                           """;

		Command = CreateCommand(CommandText, null, commandTimeout: DbTimeouts.RegularTimeoutSeconds, cancellationToken: cancellationToken);

		ResolveAsync = async connection =>
		{
			var activityGroups = await connection
				.QueryAsync<(string Name, string TenantId, string ActivityName)>(Command)
				.ConfigureAwait(false);

			return activityGroups
				.GroupBy(
					group => group.Name,
					group => new ActivityGroupActivity(group.TenantId, group.ActivityName))
				.ToDictionary(
					group => group.Key,
					object (group) => new Data([.. group]));
		};
	}

	/// <summary>
	///     Represents data associated with an activity group.
	/// </summary>
	internal sealed record Data
	{
		/// <summary>
		///     Initializes a new instance of the <see cref="Data" /> record.
		/// </summary>
		/// <param name="activities"> A list of activities associated with the group. </param>
		public Data(IList<ActivityGroupActivity> activities)
		{
			ArgumentNullException.ThrowIfNull(activities);

			Activities = activities;
		}

		/// <summary>
		///     Gets or sets the list of activities in the group.
		/// </summary>
		public IList<ActivityGroupActivity> Activities { get; set; } = [];
	}

	/// <summary>
	///     Represents an individual activity within an activity group.
	/// </summary>
	/// <param name="TenantId"> The tenant ID associated with the activity. </param>
	/// <param name="ActivityName"> The name of the activity. </param>
	internal sealed record ActivityGroupActivity(string TenantId, string ActivityName);
}
