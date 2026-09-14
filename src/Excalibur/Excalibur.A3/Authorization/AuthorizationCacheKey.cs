// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.A3.Authorization;

/// <summary>
/// Provides methods to generate cache keys for authorization-related data.
/// </summary>
/// <remarks>
/// <para>
/// These keys are a pure function of their inputs. They carry no application or tenant prefix, and they
/// deliberately do not read one from configuration: a key builder is the wrong place to decide which
/// application a cache entry belongs to.
/// </para>
/// <para>
/// <b>The keys are therefore NOT self-scoping.</b> Two applications sharing one distributed cache would
/// address the same entry for the same user, so the cache registration is responsible for scoping the
/// keyspace. What these keys guarantee is only that a given user's grants and the activity-group set have
/// stable, distinct names within whatever keyspace they are placed in.
/// </para>
/// </remarks>
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

		return $"authorization/{userId}/grants";
	}

	/// <summary>
	/// Generates a cache key for storing activity group data.
	/// </summary>
	/// <returns> A string representing the cache key for activity groups. </returns>
	public static string ForActivityGroups() => "authorization/activity-groups";
}
