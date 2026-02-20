// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.A3.Audit;
using Excalibur.A3.Exceptions;
using Excalibur.Dispatch.Abstractions.Delivery;

using Microsoft.Extensions.Caching.Distributed;

namespace Excalibur.A3.Authorization.Grants;

/// <summary>
/// Handles the execution of the <see cref="RevokeAllGrantsCommand" />.
/// </summary>
/// <remarks>
/// This handler revokes all non-expired grants for a specified user and clears the related cache entries to ensure updated state for
/// subsequent authorization checks.
/// </remarks>
internal sealed class RevokeAllGrantsCommandHandler(IGrantRepository grantRepository, IDistributedCache cache)
	: IActionHandler<RevokeAllGrantsCommand, AuditableResult<bool>>
{
	/// <inheritdoc />
	public async Task<AuditableResult<bool>> HandleAsync(RevokeAllGrantsCommand request, CancellationToken cancellationToken)
	{
		// Validate AccessToken is present
		if (request.AccessToken is null)
		{
			throw new InvalidOperationException("Access token is required for grant revocation.");
		}

		// Prevent self-administration of grants
		if (request.UserId.Equals(request.AccessToken.UserId, StringComparison.OrdinalIgnoreCase))
		{
			throw NotAuthorizedException.Because(request.AccessToken, "Users are not allowed to administer their own grants.");
		}

		// Actor performing the revocation
		var actor = request.AccessToken.FullName ?? request.AccessToken.UserId ?? "Unknown";

		// Retrieve all applicable grants for the user
		var applicationGrants = (await grantRepository.ReadAllAsync(request.UserId).ConfigureAwait(false))
			.Where(g => g.Scope != null && !string.Equals(g.Scope.GrantType, GrantType.ActivityGroup, StringComparison.Ordinal)).ToArray();

		// Revoke and delete each grant
		foreach (var grant in applicationGrants)
		{
			if (!grant.IsExpired())
			{
				grant.Revoke(actor);
			}

			await grantRepository.DeleteAsync(grant, cancellationToken).ConfigureAwait(false);
		}

		// Clear the user's grants from the cache
		await cache.RemoveAsync(AuthorizationCacheKey.ForGrants(request.UserId), cancellationToken).ConfigureAwait(false);

		// Return an audit result
		return applicationGrants.Length > 0
			? new AuditableResult<bool>(result: true, $"Revoked from {request.FullName} on {DateTime.Now:g} by {actor}.")
			: new AuditableResult<bool>(result: true, $"No grants were found to revoke from {request.FullName}.");
	}
}
