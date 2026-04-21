// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections.Concurrent;

using Excalibur.A3.Abstractions.Authorization;

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
	/// <param name="roleName">The role name (qualifier from a Role grant).</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>The set of effective activity names, or empty if the role is not found or inactive.</returns>
	public async Task<HashSet<string>> ResolveRolePermissionsAsync(
		string roleName,
		CancellationToken cancellationToken)
	{
		var cacheDuration = TimeSpan.FromSeconds(roleOptions.Value.PermissionCacheDurationSeconds);

		if (cacheDuration > TimeSpan.Zero
			&& _cache.TryGetValue(roleName, out var cached)
			&& cached.ExpiresAt > DateTimeOffset.UtcNow)
		{
			return cached.Activities;
		}

		var activities = await ResolveFromStoresAsync(roleName, cancellationToken).ConfigureAwait(false);

		if (cacheDuration > TimeSpan.Zero && _cache.Count < MaxCacheSize)
		{
			_cache[roleName] = new CachedPermissions(activities, DateTimeOffset.UtcNow + cacheDuration);
		}

		return activities;
	}

	private async Task<HashSet<string>> ResolveFromStoresAsync(
		string roleName,
		CancellationToken cancellationToken)
	{
		var activities = new HashSet<string>(StringComparer.Ordinal);
		var visited = new HashSet<string>(StringComparer.Ordinal);
		var maxDepth = roleOptions.Value.MaxHierarchyDepth;

		IReadOnlyDictionary<string, object>? allGroups = null;

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
				allGroups ??= await activityGroupStore.FindActivityGroupsAsync(cancellationToken)
					.ConfigureAwait(false);

				foreach (var groupName in role.ActivityGroupNames)
				{
					if (allGroups.TryGetValue(groupName, out var groupActivities)
						&& groupActivities is IEnumerable<string> activityNames)
					{
						foreach (var name in activityNames)
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
