// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.A3.Authorization.Grants;
using Excalibur.Data;

namespace Excalibur.A3.Authorization.PolicyData;

/// <summary>
/// Manages grants in the database.
/// </summary>
internal sealed class UserGrants(IDomainDb domainDb, IGrantRequestProvider grantRequestProvider)
{
	/// <summary>
	/// Asynchronously retrieves a dictionary of grants for a specific user.
	/// </summary>
	/// <param name="userId"> The ID of the user. </param>
	/// <returns> A dictionary of grants. </returns>
	public async Task<IDictionary<string, object>> ValueAsync(string userId)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(userId);

		return await grantRequestProvider
			.FindUserGrants(userId, CancellationToken.None).ResolveAsync(domainDb.Connection)
			.ConfigureAwait(false);
	}
}
