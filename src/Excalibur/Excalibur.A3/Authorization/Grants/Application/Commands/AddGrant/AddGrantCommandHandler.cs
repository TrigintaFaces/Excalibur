// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0


using Excalibur.A3.Audit;
using Excalibur.A3.Exceptions;
using Excalibur.Dispatch.Delivery;

namespace Excalibur.A3.Authorization.Grants;

/// <summary>
/// Handles the execution of the <see cref="AddGrantCommand" />, which adds a new grant to a user.
/// </summary>
/// <remarks>
/// This handler checks for existing grants, ensures the request is valid, and updates the repository with the new grant. It takes
/// no cache dependency: the store advances the user's monotonic grant revision inside the same commit, and reads address the cache
/// by a key carrying that revision, so a read following this grant composes a different key and cannot serve the stale entry.
/// </remarks>
/// <param name="grantRepository"> The repository for managing grants. </param>
internal sealed class AddGrantCommandHandler(IGrantRepository grantRepository)
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
#pragma warning disable IL2026, IL3050 // Serialization/reflection inherently not AOT-safe
		var existingGrant = await grantRepository.GetByIdAsync(key.ToString(), cancellationToken).ConfigureAwait(false);
#pragma warning restore IL2026, IL3050

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
					$"{request.AccessToken.FullName} failed to grant {request.FullName} on {DateTimeOffset.UtcNow:g} because the grant was already in effect.");
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
#pragma warning disable IL2026, IL3050 // Repository uses reflection-based serialization
		await grantRepository.SaveAsync(grant, cancellationToken).ConfigureAwait(false);
#pragma warning restore IL2026, IL3050

		// No cache removal here, deliberately. Reads address the cache by a key carrying the user's
		// monotonic grant revision, which the store advances inside this same commit, so a read that
		// follows this grant composes a different key and cannot serve the pre-grant entry.
		// The removal that used to sit here passed the bare key while the writer composes
		// key + "revision-v1" + revision, so it could never name the entry it claimed to evict.

		// Return a success result with an audit message.
		return new AuditableResult<bool>(
			result: true,
			$"Granted to {request.FullName} on {DateTimeOffset.UtcNow:g} by {request.AccessToken.FullName}.");
	}
}
