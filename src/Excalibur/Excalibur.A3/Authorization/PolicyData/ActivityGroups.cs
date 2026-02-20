// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.A3.Authorization.Grants;
using Excalibur.Data;

namespace Excalibur.A3.Authorization.PolicyData;

/// <summary>
/// Provides functionality for managing activity groups in a domain database.
/// </summary>
/// <remarks> Provides functionality for managing activity groups in a domain database. </remarks>
/// <param name="domainDb"> The domain database connection. </param>
/// <param name="activityGroupRequestProvider"> The activity group request provider for database operations. </param>
internal sealed class ActivityGroups(IDomainDb domainDb, IActivityGroupRequestProvider activityGroupRequestProvider)
{
	/// <summary>
	/// Asynchronously retrieves a dictionary of activity groups and their data.
	/// </summary>
	/// <returns> A dictionary of activity group data. </returns>
	public async Task<IDictionary<string, object>> ValueAsync() =>
		await activityGroupRequestProvider.FindActivityGroups(CancellationToken.None).ResolveAsync(domainDb.Connection).ConfigureAwait(false);
}
