// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.A3.Audit;
using Excalibur.A3.Exceptions;
using Excalibur.Dispatch.Abstractions.Delivery;

using Microsoft.Extensions.Caching.Distributed;

namespace Excalibur.A3.Authorization.Grants;

/// <summary>
/// Handles the execution of the <see cref="AddGrantCommand" />, which adds a new grant to a user.
/// </summary>
/// <remarks>
/// This handler checks for existing grants, ensures the request is valid, and updates the repository with the new grant. It also
/// manages caching to ensure grant changes are reflected immediately.
/// </remarks>
/// <param name="grantRepository"> The repository for managing grants. </param>
/// <param name="cache"> The distributed cache for caching grant data. </param>
internal sealed class AddGrantCommandHandler(IGrantRepository grantRepository, IDistributedCache cache)
	: IActionHandler<AddGrantCommand, AuditableResult<bool>>
{
	/// <inheritdoc />
	public async Task<AuditableResult<bool>> HandleAsync(AddGrantCommand request, CancellationToken cancellationToken)
	{
		// Validate AccessToken is present
		if (request.AccessToken is null)
		{
			throw new InvalidOperationException("Access token is required for grant addition.");
		}

		// Prevent users from administering their own grants.
		if (request.UserId.Equals(request.AccessToken.UserId, StringComparison.OrdinalIgnoreCase))
		{
			throw NotAuthorizedException.Because(request.AccessToken, "Users are not allowed to administer their own grants.");
		}

		// Generate the unique key for the grant.
		var key = new GrantKey(request.UserId, request.TenantId, request.GrantType, request.Qualifier);

		// Check if the grant already exists in the repository.
		var existingGrant = await grantRepository.GetByIdAsync(key.ToString(), cancellationToken).ConfigureAwait(false);

		// If grant exists, check if it's expired or active
		if (existingGrant is not null)
		{
			if (existingGrant.IsExpired())
			{
				// Delete expired grant and allow creating a new one
				await grantRepository.DeleteAsync(existingGrant, cancellationToken).ConfigureAwait(false);
			}
			else
			{
				// If the grant exists and is active, return a failure result with an audit message.
				return new AuditableResult<bool>(
					result: false,
					$"{request.AccessToken.FullName} failed to grant {request.FullName} on {DateTime.Now:g} because the grant was already in effect.");
			}
		}

		// Create a new grant with the specified details.
		var grant = new Grant(
			request.UserId,
			request.FullName,
			request.TenantId,
			request.GrantType,
			request.Qualifier,
			request.ExpiresOn,
			request.AccessToken.FullName);

		// Save the new grant to the repository.
		await grantRepository.SaveAsync(grant, cancellationToken).ConfigureAwait(false);

		// Remove the relevant cache entry to reflect changes.
		await cache.RemoveAsync(AuthorizationCacheKey.ForGrants(request.UserId), cancellationToken).ConfigureAwait(false);

		// Return a success result with an audit message.
		return new AuditableResult<bool>(
			result: true,
			$"Granted to {request.FullName} on {DateTime.Now:g} by {request.AccessToken.FullName}.");
	}
}
