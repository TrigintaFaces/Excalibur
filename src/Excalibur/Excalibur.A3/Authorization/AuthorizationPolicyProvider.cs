// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics.CodeAnalysis;

using Excalibur.A3.Authentication;
using Excalibur.A3.Authorization.PolicyData;
using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Serialization;
using Excalibur.Dispatch.Options.Serialization;
using Excalibur.Dispatch.Serialization;

using Microsoft.Extensions.Caching.Distributed;

namespace Excalibur.A3.Authorization;

/// <summary>
/// Provides an implementation for an authorization policy provider.
/// </summary>
/// <remarks>
/// The provider sets up policy data for authorization using activities, activity groups, and grants for a given user,
/// then evaluates them via pure C# grant checking.
/// </remarks>
/// <param name="activityGroups"> A collection of activity groups. </param>
/// <param name="userGrants"> A collection of grants for authorization purposes. </param>
/// <param name="currentUser"> The current authenticated user token. </param>
/// <param name="cache"> Distributed cache used for caching authorization data. </param>
/// <param name="tenantId"> The tenant identifier for the current context. </param>
internal sealed class AuthorizationPolicyProvider(
	ActivityGroups activityGroups,
	UserGrants userGrants,
	IAuthenticationToken currentUser,
	IDistributedCache cache,
	ITenantId tenantId
) : IAuthorizationPolicyProvider
{
	/// <inheritdoc />
	/// <exception cref="InvalidOperationException">
	/// Thrown when <see cref="IAuthenticationToken.UserId"/> is null or
	/// <see cref="ITenantId.Value"/> is null or empty.
	/// </exception>
	public async Task<IAuthorizationPolicy> GetPolicyAsync()
	{
		if (currentUser.UserId is null)
		{
			throw new InvalidOperationException("User ID is required for authorization policy.");
		}

		if (string.IsNullOrEmpty(tenantId.Value))
		{
			throw new InvalidOperationException(
				"Tenant ID is required for authorization policy. " +
				"Register ITenantId via TryAddTenantId() or ensure TenantIdentityMiddleware is configured.");
		}

		var authData = await LoadPolicyDataAsync(currentUser.UserId).ConfigureAwait(false);

		return new AuthorizationPolicy(
			authData.Grants,
			authData.ActivityGroups,
			tenantId,
			currentUser.UserId);
	}

	/// <summary>
	/// Loads the policy data required for grant evaluation.
	/// </summary>
	/// <param name="userId"> The user identifier for which the policy data is being loaded. </param>
	/// <returns> The authorization data containing grants, activity groups, and activities. </returns>
	[RequiresDynamicCode("Creates DispatchJsonSerializer which uses dynamic code for JSON serialization")]
	private async Task<AuthorizationData> LoadPolicyDataAsync(string userId)
	{
		var grantsTask = GetGrantsAsync(userId);
		var activityGroupsTask = GetActivityGroupsAsync();

		await Task.WhenAll(grantsTask, activityGroupsTask).ConfigureAwait(false);

		return new AuthorizationData(
			await grantsTask.ConfigureAwait(false),
			await activityGroupsTask.ConfigureAwait(false));
	}

	/// <summary>
	/// Retrieves grants for the specified user, using a cached value if available.
	/// </summary>
	/// <param name="userId"> The user identifier. </param>
	/// <returns> A dictionary of grants for the user. </returns>
	[RequiresDynamicCode("Calls Excalibur.A3.Authorization.AuthorizationPolicyProvider.ReadFromCacheAsync(String)")]
	private async Task<IDictionary<string, object>> GetGrantsAsync(string userId)
	{
		var cachedGrants = await ReadFromCacheAsync(AuthorizationCacheKey.ForGrants(userId)).ConfigureAwait(false);

		if (cachedGrants != null)
		{
			return cachedGrants;
		}

		cachedGrants = await userGrants.ValueAsync(userId).ConfigureAwait(false);
		await WriteToCacheAsync(AuthorizationCacheKey.ForGrants(userId), cachedGrants, TimeSpan.FromMinutes(5)).ConfigureAwait(false);

		return cachedGrants;
	}

	/// <summary>
	/// Retrieves activity groups, using a cached value if available.
	/// </summary>
	/// <returns> A dictionary of activity groups. </returns>
	[RequiresDynamicCode("Calls Excalibur.A3.Authorization.AuthorizationPolicyProvider.ReadFromCacheAsync(String)")]
	private async Task<IDictionary<string, object>> GetActivityGroupsAsync()
	{
		var groups = await ReadFromCacheAsync(AuthorizationCacheKey.ForActivityGroups()).ConfigureAwait(false);

		if (groups != null)
		{
			return groups;
		}

		groups = await activityGroups.ValueAsync().ConfigureAwait(false);
		await WriteToCacheAsync(AuthorizationCacheKey.ForActivityGroups(), groups, TimeSpan.FromHours(1)).ConfigureAwait(false);

		return groups;
	}

	[RequiresDynamicCode("Creates DispatchJsonSerializer which uses dynamic code for JSON serialization")]
	private async Task<IDictionary<string, object>?> ReadFromCacheAsync(string key)
	{
		var item = await cache.GetStringAsync(key).ConfigureAwait(false);
		using var serializer = new DispatchJsonSerializer(options =>
		{
			var defaultOptions = DispatchJsonSerializerOptions.Default;
			options.PropertyNamingPolicy = defaultOptions.PropertyNamingPolicy;
			options.DefaultIgnoreCondition = defaultOptions.DefaultIgnoreCondition;
			options.WriteIndented = defaultOptions.WriteIndented;
		});
		return item == null
			? null
			: await serializer.DeserializeAsync<IDictionary<string, object>>(item).ConfigureAwait(false);
	}

	/// <summary>
	/// Writes an item to the distributed cache.
	/// </summary>
	/// <param name="key"> The cache key. </param>
	/// <param name="item"> The item to cache. </param>
	/// <param name="slidingExpiration"> The sliding expiration duration for the cache entry. </param>
	[RequiresDynamicCode("Creates DispatchJsonSerializer which uses dynamic code for JSON serialization")]
	private async Task WriteToCacheAsync(string key, IDictionary<string, object> item, TimeSpan slidingExpiration)
	{
		using var serializer = new DispatchJsonSerializer(options =>
		{
			var defaultOptions = DispatchJsonSerializerOptions.Default;
			options.PropertyNamingPolicy = defaultOptions.PropertyNamingPolicy;
			options.DefaultIgnoreCondition = defaultOptions.DefaultIgnoreCondition;
			options.WriteIndented = defaultOptions.WriteIndented;
		});
		var cacheOptions = new DistributedCacheEntryOptions { SlidingExpiration = slidingExpiration };
		var serialized = await serializer.SerializeAsync(item).ConfigureAwait(false);

		await cache.SetStringAsync(key, serialized, cacheOptions).ConfigureAwait(false);
	}

	/// <summary>
	/// Internal record holding the loaded authorization data.
	/// </summary>
	private sealed record AuthorizationData(
		IDictionary<string, object> Grants,
		IDictionary<string, object> ActivityGroups);
}
