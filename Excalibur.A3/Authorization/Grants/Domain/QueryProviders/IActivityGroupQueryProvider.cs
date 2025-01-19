using System.Data;

using Excalibur.DataAccess;

namespace Excalibur.A3.Authorization.Grants.Domain.QueryProviders;

/// <summary>
///     Interface for a provider that generates database-specific queries for activity groups.
/// </summary>
public interface IActivityGroupQueryProvider
{
	/// <summary>
	///     Creates a query to check if an activity group with the specified name exists.
	/// </summary>
	/// <param name="activityGroupName"> The name of the activity group to check for existence. </param>
	/// <param name="cancellationToken"> The cancellation token to propagate notification that the operation should be canceled. </param>
	/// <returns>
	///     A query object implementing <see cref="IDataQuery{TConnection, TResult}" /> that checks if the activity group exists.
	/// </returns>
	public IDataQuery<IDbConnection, bool> ActivityGroupExists(string activityGroupName, CancellationToken cancellationToken = default);

	/// <summary>
	///     Creates a query to retrieve activity groups in a customizable format, such as a dictionary.
	/// </summary>
	/// <param name="cancellationToken"> The cancellation token to propagate notification that the operation should be canceled. </param>
	/// <returns>
	///     A query object implementing <see cref="IDataQuery{TConnection, TResult}" /> that retrieves activity groups as a dictionary.
	/// </returns>
	public IDataQuery<IDbConnection, Dictionary<string, object>> FindActivityGroups(CancellationToken cancellationToken = default);

	/// <summary>
	///     Creates a query to delete all activity groups from the database.
	/// </summary>
	/// <param name="cancellationToken"> The cancellation token to propagate notification that the operation should be canceled. </param>
	/// <returns> A query object implementing <see cref="IDataQuery{TConnection, TResult}" /> that deletes all activity groups. </returns>
	public IDataQuery<IDbConnection, int> DeleteAllActivityGroups(CancellationToken cancellationToken = default);

	/// <summary>
	///     Creates a query to insert a new activity group with the specified tenant ID, name, and activity name.
	/// </summary>
	/// <param name="tenantId">
	///     The ID of the tenant to which the activity group belongs. This value may be null for global activity groups.
	/// </param>
	/// <param name="name"> The name of the activity group. </param>
	/// <param name="activityName"> The name of the activity to associate with the group. </param>
	/// <param name="cancellationToken"> The cancellation token to propagate notification that the operation should be canceled. </param>
	/// <returns> A query object implementing <see cref="IDataQuery{TConnection, TResult}" /> that inserts the activity group. </returns>
	public IDataQuery<IDbConnection, int> CreateActivityGroup(string? tenantId, string name, string activityName,
		CancellationToken cancellationToken = default);
}
