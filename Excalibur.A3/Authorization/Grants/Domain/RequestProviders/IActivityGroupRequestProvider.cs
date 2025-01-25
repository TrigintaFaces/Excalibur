using System.Data;

using Excalibur.DataAccess;

namespace Excalibur.A3.Authorization.Grants.Domain.RequestProviders;

/// <summary>
///     Interface for a provider that generates database-specific requests for activity groups.
/// </summary>
public interface IActivityGroupRequestProvider
{
	/// <summary>
	///     Creates a request to check if an activity group with the specified name exists.
	/// </summary>
	/// <param name="activityGroupName"> The name of the activity group to check for existence. </param>
	/// <param name="cancellationToken"> The cancellation token to propagate notification that the operation should be canceled. </param>
	/// <returns>
	///     A request object implementing <see cref="IDataRequest{TConnection,TModel}" /> that checks if the activity group exists.
	/// </returns>
	public IDataRequest<IDbConnection, bool> ActivityGroupExists(string activityGroupName, CancellationToken cancellationToken = default);

	/// <summary>
	///     Creates a request to retrieve activity groups in a customizable format, such as a dictionary.
	/// </summary>
	/// <param name="cancellationToken"> The cancellation token to propagate notification that the operation should be canceled. </param>
	/// <returns>
	///     A request object implementing <see cref="IDataRequest{TConnection,TModel}" /> that retrieves activity groups as a dictionary.
	/// </returns>
	public IDataRequest<IDbConnection, Dictionary<string, object>> FindActivityGroups(CancellationToken cancellationToken = default);

	/// <summary>
	///     Creates a request to delete all activity groups from the database.
	/// </summary>
	/// <param name="cancellationToken"> The cancellation token to propagate notification that the operation should be canceled. </param>
	/// <returns> A request object implementing <see cref="IDataRequest{TConnection,TModel}" /> that deletes all activity groups. </returns>
	public IDataRequest<IDbConnection, int> DeleteAllActivityGroups(CancellationToken cancellationToken = default);

	/// <summary>
	///     Creates a request to insert a new activity group with the specified tenant ID, name, and activity name.
	/// </summary>
	/// <param name="tenantId">
	///     The ID of the tenant to which the activity group belongs. This value may be null for global activity groups.
	/// </param>
	/// <param name="name"> The name of the activity group. </param>
	/// <param name="activityName"> The name of the activity to associate with the group. </param>
	/// <param name="cancellationToken"> The cancellation token to propagate notification that the operation should be canceled. </param>
	/// <returns> A request object implementing <see cref="IDataRequest{TConnection,TModel}" /> that inserts the activity group. </returns>
	public IDataRequest<IDbConnection, int> CreateActivityGroup(string? tenantId, string name, string activityName,
		CancellationToken cancellationToken = default);
}
