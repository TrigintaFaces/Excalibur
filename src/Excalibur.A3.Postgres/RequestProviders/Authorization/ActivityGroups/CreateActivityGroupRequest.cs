using Dapper;

using Excalibur.DataAccess;

namespace Excalibur.A3.Postgres.RequestProviders.Authorization.ActivityGroups;

/// <summary>
///     Represents a query for creating a new activity group in the database.
/// </summary>
public class CreateActivityGroupRequest : DataRequest<int>
{
	/// <summary>
	///     Initializes a new instance of the <see cref="CreateActivityGroupRequest" /> class.
	/// </summary>
	/// <param name="tenantId"> The tenant ID associated with the activity group (optional). </param>
	/// <param name="name"> The name of the activity group. </param>
	/// <param name="activityName"> The name of the activity within the group. </param>
	/// <param name="cancellationToken"> The cancellation token for the query. </param>
	public CreateActivityGroupRequest(string? tenantId, string name, string activityName, CancellationToken cancellationToken)
	{
		const string CommandText = """
		                           INSERT INTO authz.activity_group (
		                             tenant_id,
		                             name,
		                             activity_name
		                           ) VALUES (
		                             @TenantId,
		                             @ActivityGroupName,
		                             @ActivityName
		                           );
		                           """;

		Command = CreateCommand(
			CommandText,
			new DynamicParameters(new { TenantId = tenantId, ActivityGroupName = name, ActivityName = activityName }),
			commandTimeout: DbTimeouts.RegularTimeoutSeconds,
			cancellationToken: cancellationToken);

		ResolveAsync = async connection => await connection.ExecuteAsync(Command).ConfigureAwait(false);
	}
}
