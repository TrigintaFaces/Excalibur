namespace Excalibur.A3.Authorization;

/// <summary>
///     Represents an authorization policy for validating user access to activities and resources.
/// </summary>
public interface IAuthorizationPolicy : IPolicy
{
	/// <summary>
	///     Gets the tenant identifier associated with the authorization policy.
	/// </summary>
	string TenantId { get; }

	/// <summary>
	///     Gets the user identifier associated with the authorization policy.
	/// </summary>
	string? UserId { get; }

	/// <summary>
	///     Determines if the user is authorized to perform an activity on an optional resource.
	/// </summary>
	/// <param name="activityName"> The name of the activity to authorize. </param>
	/// <param name="resourceId"> The identifier of the resource (optional). </param>
	/// <returns> <c> true </c> if the user is authorized; otherwise, <c> false </c>. </returns>
	bool IsAuthorized(string activityName, string? resourceId = null);

	/// <summary>
	///     Determines if the user has a grant for the specified activity.
	/// </summary>
	/// <param name="activityName"> The name of the activity to check for a grant. </param>
	/// <returns> <c> true </c> if the user has a grant; otherwise, <c> false </c>. </returns>
	bool HasGrant(string activityName);

	/// <summary>
	///     Determines if the user has a grant for a specific activity type.
	/// </summary>
	/// <typeparam name="TActivity"> The activity type to check for a grant. </typeparam>
	/// <returns> <c> true </c> if the user has a grant; otherwise, <c> false </c>. </returns>
	bool HasGrant<TActivity>();

	/// <summary>
	///     Determines if the user has a grant for a specific resource type and resource ID.
	/// </summary>
	/// <param name="resourceType"> The type of the resource. </param>
	/// <param name="resourceId"> The identifier of the resource. </param>
	/// <returns> <c> true </c> if the user has a grant; otherwise, <c> false </c>. </returns>
	bool HasGrant(string resourceType, string resourceId);

	/// <summary>
	///     Determines if the user has a grant for a specific resource type and resource ID.
	/// </summary>
	/// <typeparam name="TResourceType"> The resource type to check. </typeparam>
	/// <param name="resourceId"> The identifier of the resource. </param>
	/// <returns> <c> true </c> if the user has a grant; otherwise, <c> false </c>. </returns>
	bool HasGrant<TResourceType>(string resourceId);
}
