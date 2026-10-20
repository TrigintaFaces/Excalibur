// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0


using Excalibur.A3.Audit;
using Excalibur.A3.Exceptions;
using Excalibur.Dispatch.Delivery;

namespace Excalibur.A3.Authorization.Grants;

/// <summary>
/// Handles the execution of the <see cref="RevokeGrantCommand" />.
/// </summary>
/// <remarks>
/// This handler is responsible for revoking a specific grant from a user. It performs the necessary checks and interacts with the
/// <see cref="IGrantRepository" /> to modify grant data. It takes no cache dependency: the store advances the user's monotonic
/// grant revision inside the same commit, and reads address the cache by a key carrying that revision, so a read following this
/// revocation composes a different key and cannot observe the pre-revocation entry.
/// </remarks>
internal sealed class RevokeGrantCommandHandler(IGrantRepository grantRepository)
	: IActionHandler<RevokeGrantCommand, AuditableResult<bool>>
{

	/// <inheritdoc />
	public async Task<AuditableResult<bool>> HandleAsync(RevokeGrantCommand request, CancellationToken cancellationToken)
	{
		// Validate AccessToken is present
		if (request.AccessToken is null)
		{
			throw new InvalidOperationException("Access token is required for grant revocation.");
		}

		// Prevent users from administering their own grants
		if (request.UserId.Equals(request.AccessToken.UserId, StringComparison.OrdinalIgnoreCase))
		{
			throw NotAuthorizedException.Because(request.AccessToken, "Users are not allowed to administer their own grants.");
		}

		// Create a unique key for the grant to be revoked
		var key = new GrantKey(request.UserId, request.TenantId, request.GrantType, request.Qualifier);

		// Attempt to retrieve the grant using the key
#pragma warning disable IL2026, IL3050 // Serialization/reflection inherently not AOT-safe
		var grant = await grantRepository.GetByIdAsync(key.ToString(), cancellationToken).ConfigureAwait(false);
#pragma warning restore IL2026, IL3050

		if (grant is null)
		{
			return new AuditableResult<bool>(
				result: false,
				$"{request.AccessToken.FullName} failed to revoke grant {key.Scope} on {DateTimeOffset.UtcNow:g} because the grant was not found and may have already been revoked.");
		}

		// Revoke the grant and delete it from the repository
		grant.Revoke(request.AccessToken.FullName);
		await grantRepository.DeleteAsync(grant, cancellationToken).ConfigureAwait(false);

		// No cache removal here, deliberately. Reads address the cache by a key carrying the user's
		// monotonic grant revision, which the store advances inside this same commit, so a
		// post-revocation read composes a different key and cannot observe the stale entry.
		// The removal that used to sit here passed the bare key while the writer composes
		// key + "revision-v1" + revision, so it could never name the entry it claimed to evict.

		// Return an auditable result confirming the revocation
		return new AuditableResult<bool>(
			result: true,
			$"Revoked from {grant.FullName} on {DateTimeOffset.UtcNow:g} by {request.AccessToken.FullName}.");
	}
}
