namespace Excalibur.A3.Authorization;

/// <summary>
///     Defines methods for managing activity groups and synchronizing their associated data.
/// </summary>
public interface IActivityGroupService
{
	/// <summary>
	///     Checks whether an activity group with the specified name exists.
	/// </summary>
	/// <param name="activityGroupName"> The name of the activity group to check for existence. </param>
	/// <returns>
	///     A <see cref="Task{TResult}" /> that resolves to <c> true </c> if the activity group exists; otherwise, <c> false </c>.
	/// </returns>
	public Task<bool> Exists(string activityGroupName);

	/// <summary>
	///     Synchronizes all activity groups by updating or creating them based on the current state of the source data.
	/// </summary>
	/// <returns> A <see cref="Task" /> representing the asynchronous operation. </returns>
	public Task SyncActivityGroups();

	/// <summary>
	///     Synchronizes the grants (permissions or roles) of the activity groups for a specific user.
	/// </summary>
	/// <param name="userId"> The unique identifier of the user whose grants will be synchronized. </param>
	/// <returns> A <see cref="Task" /> representing the asynchronous operation. </returns>
	public Task SyncActivityGroupGrants(string userId);

	/// <summary>
	///     Synchronizes the grants (permissions or roles) for all users across all activity groups.
	/// </summary>
	/// <returns> A <see cref="Task" /> representing the asynchronous operation. </returns>
	public Task SyncAllActivityGroupGrants();
}
