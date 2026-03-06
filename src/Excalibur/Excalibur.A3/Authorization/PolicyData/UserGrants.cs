// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.A3.Abstractions.Authorization;

namespace Excalibur.A3.Authorization.PolicyData;

/// <summary>
/// Manages grants in the store via the provider-neutral query interface.
/// </summary>
internal sealed class UserGrants(IGrantStore grantStore)
{
	private readonly IGrantQueryStore _queryStore =
		grantStore.GetService(typeof(IGrantQueryStore)) as IGrantQueryStore
		?? throw new InvalidOperationException(
			"The configured IGrantStore does not support IGrantQueryStore. " +
			"Ensure the store implementation returns IGrantQueryStore from GetService().");

	/// <summary>
	/// Asynchronously retrieves a dictionary of grants for a specific user.
	/// </summary>
	/// <param name="userId"> The ID of the user. </param>
	/// <returns> A dictionary of grants. </returns>
	public async Task<IDictionary<string, object>> ValueAsync(string userId)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(userId);

		var result = await _queryStore.FindUserGrantsAsync(userId, CancellationToken.None).ConfigureAwait(false);
		return new Dictionary<string, object>(result);
	}
}
