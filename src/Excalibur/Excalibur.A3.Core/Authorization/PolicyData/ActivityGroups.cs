// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

namespace Excalibur.A3.Authorization.PolicyData;

/// <summary>
/// Provides functionality for managing activity groups via the provider-neutral store.
/// </summary>
/// <remarks> Provides functionality for managing activity groups via the provider-neutral store. </remarks>
/// <param name="activityGroupStore"> The activity group store for persistence operations. </param>
internal sealed class ActivityGroups(IActivityGroupStore activityGroupStore)
{
	/// <summary>
	/// Asynchronously retrieves a dictionary of activity groups and their data.
	/// </summary>
	/// <param name="tenantId"> The tenant whose activity groups to read. </param>
	/// <param name="cancellationToken"> A token to cancel the asynchronous operation. </param>
	/// <returns> A dictionary of that tenant's activity group data. </returns>
	public Task<IReadOnlyDictionary<string, IReadOnlyCollection<string>>> ValueAsync(
		string tenantId,
		CancellationToken cancellationToken) =>
		// The store's return value IS the answer. This used to re-box each value to `object` on the
		// grounds that the policy document round-trips through a cache serializer -- which had the
		// causality backwards: the round trip is precisely why this seam must NOT be weakly typed,
		// because `object` is the one type that cannot survive it with its shape intact. A declared
		// `object` is materialised as a JsonElement, which satisfies no collection test, so the widening
		// silently denied every activity-group grant on the cached path for every provider.
		activityGroupStore.FindActivityGroupsAsync(tenantId, cancellationToken);
}
