using Excalibur.Core;

namespace Excalibur.A3.Authorization;

/// <summary>
///     Provides methods to generate cache keys for authorization-related data.
/// </summary>
public static class AuthorizationCacheKey
{
	/// <summary>
	///     Generates a cache key for authorization grants for a specific user.
	/// </summary>
	/// <param name="userId"> The unique identifier of the user. </param>
	/// <returns> A string representing the cache key for user grants. </returns>
	public static string ForGrants(string userId)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(userId);

		return $"{GetBasePath()}/authorization/{userId}/grants";
	}

	/// <summary>
	///     Generates a cache key for storing activity group data.
	/// </summary>
	/// <returns> A string representing the cache key for activity groups. </returns>
	public static string ForActivityGroups() => $"{GetBasePath()}/authorization/activity-groups";

	/// <summary>
	///     Retrieves the base path for the cache keys from the application context.
	/// </summary>
	/// <returns> A string representing the base cache key namespace. </returns>
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
