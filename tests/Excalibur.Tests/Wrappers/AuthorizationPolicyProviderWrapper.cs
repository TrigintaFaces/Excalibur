using DOPA;

using Excalibur.A3.Authentication;
using Excalibur.A3.Authorization;
using Excalibur.Core;

using Microsoft.Extensions.Caching.Distributed;

namespace Excalibur.Tests.Wrappers;

/// <summary>
///     A wrapper for the internal AuthorizationPolicyProvider class to enable testing.
/// </summary>
public class AuthorizationPolicyProviderWrapper : IAuthorizationPolicyProvider
{
	private readonly IAuthorizationPolicyProvider _provider;

	/// <summary>
	///     Initializes a new instance of the <see cref="AuthorizationPolicyProviderWrapper" /> class.
	/// </summary>
	/// <param name="opaPolicy"> The OPA policy. </param>
	/// <param name="activities"> The activities wrapper. </param>
	/// <param name="activityGroups"> The activity groups wrapper. </param>
	/// <param name="userGrants"> The user grants wrapper. </param>
	/// <param name="currentUser"> The current user. </param>
	/// <param name="cache"> The distributed cache. </param>
	/// <param name="tenantId"> The tenant ID. </param>
	public AuthorizationPolicyProviderWrapper(
		IOpaPolicy opaPolicy,
		ActivitiesWrapper activities,
		ActivityGroupsWrapper activityGroups,
		UserGrantsWrapper userGrants,
		IAuthenticationToken currentUser,
		IDistributedCache cache,
		ITenantId tenantId)
	{
		ArgumentNullException.ThrowIfNull(opaPolicy);
		ArgumentNullException.ThrowIfNull(activities);
		ArgumentNullException.ThrowIfNull(activityGroups);
		ArgumentNullException.ThrowIfNull(userGrants);
		ArgumentNullException.ThrowIfNull(currentUser);
		ArgumentNullException.ThrowIfNull(cache);
		ArgumentNullException.ThrowIfNull(tenantId);

		// Get the internal types
		var providerType = Type.GetType("Excalibur.A3.Authorization.AuthorizationPolicyProvider, Excalibur.A3");

		// Get the field info for private fields in the wrapper classes
		var userGrantsField = userGrants.GetType().GetField("_userGrantsInstance",
			System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

		if (userGrantsField == null)
		{
			throw new InvalidOperationException("Required fields not found in wrapper classes");
		}

		// Get the actual internal instances
		var activitiesInstance = activities.InternalInstance;
		var activityGroupsInstance = activityGroups.InternalInstance;
		var userGrantsInstance = userGrantsField.GetValue(userGrants);

		if (providerType == null || activitiesInstance == null ||
			activityGroupsInstance == null || userGrantsInstance == null)
		{
			throw new InvalidOperationException("Required internal types or instances not found");
		}

		// Create the policy provider with internal classes
		_provider = (IAuthorizationPolicyProvider)Activator.CreateInstance(
			providerType,
			opaPolicy,
			activitiesInstance,
			activityGroupsInstance,
			userGrantsInstance,
			currentUser,
			cache,
			tenantId);
	}

	/// <inheritdoc />
	public Task<IAuthorizationPolicy> GetPolicyAsync() => _provider.GetPolicyAsync();
}
