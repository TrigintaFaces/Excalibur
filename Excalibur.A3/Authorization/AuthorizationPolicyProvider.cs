using System.Text.Json;

using Excalibur.A3.Authentication;
using Excalibur.A3.Authorization.PolicyData;
using Excalibur.Data.Serialization;

using Microsoft.Extensions.Caching.Distributed;

using IOpaPolicy = DOPA.IOpaPolicy;

namespace Excalibur.A3.Authorization;

/// <summary>
///     Provides an implementation for an authorization policy provider.
/// </summary>
/// <remarks>
///     The provider sets up policy data for authorization using Open Policy Agent (OPA), activities, activity groups, and grants for a
///     given user.
/// </remarks>
/// <param name="opaPolicy"> The OPA policy instance used for policy evaluation. </param>
/// <param name="activities"> A collection of available activities. </param>
/// <param name="activityGroups"> A collection of activity groups. </param>
/// <param name="userGrants"> A collection of grants for authorization purposes. </param>
/// <param name="currentUser"> The current authenticated user token. </param>
/// <param name="cache"> Distributed cache used for caching authorization data. </param>
/// <param name="tenantId"> The tenant identifier for the current context. </param>
internal sealed class AuthorizationPolicyProvider(
	IOpaPolicy opaPolicy,
	Activities activities,
	ActivityGroups activityGroups,
	UserGrants userGrants,
	IAuthenticationToken currentUser,
	IDistributedCache cache,
	ITenantId tenantId
) : IAuthorizationPolicyProvider
{
	/// <inheritdoc />
	public async Task<IAuthorizationPolicy> GetPolicyAsync()
	{
		await SetPolicyData(currentUser.UserId).ConfigureAwait(false);

		return new AuthorizationPolicy(opaPolicy, tenantId, currentUser.UserId);
	}

	/// <summary>
	///     Sets the policy data required for OPA evaluation.
	/// </summary>
	/// <param name="userId"> The user identifier for which the policy data is being set. </param>
	private async Task SetPolicyData(string userId)
	{
		var authData = new
		{
			grants = await GetGrants(userId).ConfigureAwait(false),
			activityGroups = await GetActivityGroups().ConfigureAwait(false),
			activities = activities.Value
		};

		opaPolicy.SetData(authData);
	}

	/// <summary>
	///     Retrieves grants for the specified user, using a cached value if available.
	/// </summary>
	/// <param name="userId"> The user identifier. </param>
	/// <returns> A dictionary of grants for the user. </returns>
	private async Task<IDictionary<string, object>> GetGrants(string userId)
	{
		var cachedGrants = await ReadFromCache(AuthorizationCacheKey.ForGrants(userId)).ConfigureAwait(false);

		if (cachedGrants != null)
		{
			return cachedGrants;
		}

		cachedGrants = await userGrants.Value(userId).ConfigureAwait(false);
		await WriteToCache(AuthorizationCacheKey.ForGrants(userId), cachedGrants, TimeSpan.FromMinutes(5)).ConfigureAwait(false);

		return cachedGrants;
	}

	/// <summary>
	///     Retrieves activity groups, using a cached value if available.
	/// </summary>
	/// <returns> A dictionary of activity groups. </returns>
	private async Task<IDictionary<string, object>> GetActivityGroups()
	{
		var groups = await ReadFromCache(AuthorizationCacheKey.ForActivityGroups()).ConfigureAwait(false);

		if (groups != null)
		{
			return groups;
		}

		groups = await activityGroups.Value().ConfigureAwait(false);
		await WriteToCache(AuthorizationCacheKey.ForActivityGroups(), groups, TimeSpan.FromHours(1)).ConfigureAwait(false);

		return groups;
	}

	/// <summary>
	///     Reads an item from the distributed cache.
	/// </summary>
	/// <param name="key"> The cache key. </param>
	/// <returns> The cached item as a dictionary or <c> null </c> if not found. </returns>
	private async Task<IDictionary<string, object>?> ReadFromCache(string key)
	{
		var item = await cache.GetStringAsync(key).ConfigureAwait(false);
		return item == null
			? null
			: JsonSerializer.Deserialize<IDictionary<string, object>>(item, ExcaliburJsonSerializerOptions.Opa);
	}

	/// <summary>
	///     Writes an item to the distributed cache.
	/// </summary>
	/// <param name="key"> The cache key. </param>
	/// <param name="item"> The item to cache. </param>
	/// <param name="slidingExpiration"> The sliding expiration duration for the cache entry. </param>
	private Task WriteToCache(string key, IDictionary<string, object> item, TimeSpan slidingExpiration)
	{
		var options = new DistributedCacheEntryOptions { SlidingExpiration = slidingExpiration };

		return cache.SetStringAsync(key, JsonSerializer.Serialize(item, ExcaliburJsonSerializerOptions.Opa), options);
	}
}
