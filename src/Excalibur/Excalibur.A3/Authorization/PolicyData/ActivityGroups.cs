// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.A3.Abstractions.Authorization;

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
	/// <param name="cancellationToken"> A token to cancel the asynchronous operation. </param>
	/// <returns> A dictionary of activity group data. </returns>
	public async Task<IDictionary<string, object>> ValueAsync(CancellationToken cancellationToken)
	{
		var result = await activityGroupStore.FindActivityGroupsAsync(cancellationToken).ConfigureAwait(false);
		return new Dictionary<string, object>(result);
	}
}
