// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Domain;

namespace Excalibur.A3.Authorization;

/// <summary>
/// Provides methods to generate cache keys for authorization-related data.
/// </summary>
public static class AuthorizationCacheKey
{
	/// <summary>
	/// Generates a cache key for authorization grants for a specific user.
	/// </summary>
	/// <param name="userId"> The unique identifier of the user. </param>
	/// <returns> A string representing the cache key for user grants. </returns>
	public static string ForGrants(string userId)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(userId);

		return $"{GetBasePath()}/authorization/{userId}/grants";
	}

	/// <summary>
	/// Generates a cache key for storing activity group data.
	/// </summary>
	/// <returns> A string representing the cache key for activity groups. </returns>
	public static string ForActivityGroups() => $"{GetBasePath()}/authorization/activity-groups";

	/// <summary>
	/// Retrieves the base path for the cache keys from the application context.
	/// </summary>
	/// <returns> A string representing the base cache key Excalibur.Dispatch.Transport.Aws.Advanced.SessionManagement. </returns>
	/// <exception cref="InvalidOperationException"></exception>
	private static string GetBasePath()
	{
		var basePath = ApplicationContext.Get(nameof(AuthorizationCacheKey));
		if (string.IsNullOrWhiteSpace(basePath))
		{
			throw new InvalidOperationException(
				"The base path for AuthorizationCacheKey is not configured correctly in ApplicationContext.");
		}

		return basePath;
	}
}
