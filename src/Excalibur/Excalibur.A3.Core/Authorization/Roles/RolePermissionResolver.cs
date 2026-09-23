// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Collections.Concurrent;

using Excalibur.Dispatch;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Excalibur.A3.Authorization.Roles;

/// <summary>
/// Resolves a role's effective activity names by expanding its activity groups,
/// unioning with its direct activity names, and traversing the role hierarchy
/// up to <see cref="RoleOptions.MaxHierarchyDepth"/>. Results are cached with a bounded
/// <see cref="ConcurrentDictionary{TKey, TValue}"/> (cap 1024) and TTL-based invalidation.
/// </summary>
/// <remarks>
/// <para>
/// Shared by <c>RoleAwareAuthorizationEvaluator</c> and future SoD evaluator
/// to avoid duplicating role-to-permission resolution logic.
/// </para>
/// <para>
/// Hierarchy traversal respects cycle detection and skips inactive parent roles.
/// </para>
/// </remarks>
internal sealed class RolePermissionResolver(
	IRoleStore roleStore,
	IActivityGroupStore activityGroupStore,
	IOptions<RoleOptions> roleOptions,
	ILogger<RolePermissionResolver> logger)
{
	private const int MaxCacheSize = 1024;

	private readonly ConcurrentDictionary<string, CachedPermissions> _cache = new(StringComparer.Ordinal);

	/// <summary>
	/// Resolves the effective activity names for the given role.
	/// </summary>
	/// <param name="tenantId">
	/// The tenant the role is being resolved for. A role name identifies one role WITHIN a tenant, so a
	/// resolver shared across tenants must discriminate on it: this type is a singleton and memoizes what
	/// it resolves, and a cache keyed on the role name alone returned the first tenant's activities to
	/// every later tenant asking for the same role name.
	/// </param>
	/// <param name="roleName">The role name (qualifier from a Role grant).</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>The set of effective activity names, or empty if the role is not found or inactive.</returns>
	public async Task<HashSet<string>> ResolveRolePermissionsAsync(
		string tenantId,
		string roleName,
		CancellationToken cancellationToken)
	{
		var cacheDuration = TimeSpan.FromSeconds(roleOptions.Value.PermissionCacheDurationSeconds);

		// Tenant-composed: this resolver is a process-wide singleton, so a bare role name let one tenant's
		// resolved activity set satisfy another tenant's lookup for the lifetime of the entry. Composing
		// multiplies the key space against MaxCacheSize, so under many tenants this degrades to NOT
		// caching (skip-when-full below) rather than to evicting wrongly -- slower, never wrong.
		var cacheKey = SegmentedKey.Compose(tenantId, roleName);

		if (cacheDuration > TimeSpan.Zero
			&& _cache.TryGetValue(cacheKey, out var cached)
			&& cached.ExpiresAt > DateTimeOffset.UtcNow)
		{
			return cached.Activities;
		}

		var activities = await ResolveFromStoresAsync(tenantId, roleName, cancellationToken).ConfigureAwait(false);

		if (cacheDuration > TimeSpan.Zero && _cache.Count < MaxCacheSize)
		{
			_cache[cacheKey] = new CachedPermissions(activities, DateTimeOffset.UtcNow + cacheDuration);
		}

		return activities;
	}

	private async Task<HashSet<string>> ResolveFromStoresAsync(
		string tenantId,
		string roleName,
		CancellationToken cancellationToken)
	{
		var activities = new HashSet<string>(StringComparer.Ordinal);
		var visited = new HashSet<string>(StringComparer.Ordinal);
		var maxDepth = roleOptions.Value.MaxHierarchyDepth;

		IReadOnlyDictionary<string, IReadOnlyCollection<string>>? allGroups = null;

		var currentRoleName = roleName;
		var depth = 0;

		while (currentRoleName is not null && depth <= maxDepth && visited.Add(currentRoleName))
		{
			var role = await roleStore.GetRoleAsync(currentRoleName, cancellationToken).ConfigureAwait(false);

			if (role is null || role.State != RoleState.Active)
			{
				break;
			}

			// Direct activity names
			foreach (var activityName in role.ActivityNames)
			{
				activities.Add(activityName);
			}

			// Expand activity group names
			if (role.ActivityGroupNames.Count > 0)
			{
				allGroups ??= await activityGroupStore.FindActivityGroupsAsync(tenantId, cancellationToken)
					.ConfigureAwait(false);

				foreach (var groupName in role.ActivityGroupNames)
				{
					// The store keys groups by the composed (tenant, name) pair, because a group name is
					// unique only within a tenant. Looking up the bare name would miss every entry.
					// No type test: the store contract now carries the activity names, so there is no shape
					// left to guess at. This previously read `groupActivities is IEnumerable<string>`, which
					// matched the in-memory store's List<string> and matched NOTHING on either database
					// provider -- so every role naming an activity group contributed zero activities, with
					// no exception and no log, because a failed type test is silent.
					if (allGroups.TryGetValue(SegmentedKey.Compose(tenantId, groupName), out var groupActivities))
					{
						foreach (var name in groupActivities)
						{
							activities.Add(name);
						}
					}
				}
			}

			currentRoleName = role.ParentRoleName;
			depth++;
		}

		if (currentRoleName is not null && !visited.Add(currentRoleName))
		{
			logger.LogWarning(
				"Role hierarchy cycle detected: role '{RoleName}' encountered again while resolving '{OriginalRole}'. Traversal stopped.",
				currentRoleName,
				roleName);
		}

		return activities;
	}

	private sealed record CachedPermissions(HashSet<string> Activities, DateTimeOffset ExpiresAt);
}
