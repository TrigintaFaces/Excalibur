// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0


using Excalibur.A3.Audit;
using Excalibur.A3.Exceptions;
using Excalibur.Dispatch;
using Excalibur.Dispatch.Delivery;

namespace Excalibur.A3.Authorization.Grants;

/// <summary>
/// Handles the execution of the <see cref="RevokeAllGrantsCommand" />.
/// </summary>
/// <remarks>
/// Revokes every grant type in the requested scope atomically. The store revision fences later authorization acquisitions, so
/// this handler takes no cache dependency: reads address the cache by a key carrying that revision, and the store advances it
/// inside the same commit, so a read following this revocation cannot name the pre-revocation entry.
/// </remarks>
internal sealed class RevokeAllGrantsCommandHandler(IGrantStore grantStore, ITenantContext tenantContext)
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
		ArgumentNullException.ThrowIfNull(request.RevocationScope);
		var tenant = request.RevocationScope.TenantId;
		if (tenant is null || !string.Equals(tenant, request.TenantId, StringComparison.Ordinal)
			|| !string.Equals(tenant, tenantContext.TenantId, StringComparison.Ordinal))
		{
			throw NotAuthorizedException.Because(request.AccessToken,
				"Grant revocation must remain within the command's authorized ambient tenant. Use an explicitly authorized administrative store operation for all tenants.");
		}

		var atomicStore = grantStore.GetService(typeof(IAtomicGrantRevocationStore)) as IAtomicGrantRevocationStore
			?? throw new NotSupportedException("The configured grant store does not support atomic grant revocation. No grants were changed.");
		var committed = await atomicStore.RevokeAllAsync(
			new GrantRevocationRequest(request.OperationId, request.UserId, request.RevocationScope, actor), cancellationToken).ConfigureAwait(false);

		// No cache removal here, deliberately. Reads address the cache by a key that carries the user's
		// monotonic grant revision, and the store advances that revision inside this same commit, so a
		// post-revocation read composes a DIFFERENT key and cannot observe the pre-revocation entry.
		// The orphaned entry is unreachable and expires on its own TTL.
		//
		// A removal here was not merely redundant, it was inert: it passed the bare key while the writer
		// composes key + "revision-v1" + revision, so the two could never name the same entry. Deleted
		// rather than repaired, because repairing it is impossible by construction -- the revision has
		// already advanced by this point, so the caller cannot name the key it would need to evict.

		// Return an audit result
		return committed.RevokedCount > 0
			? new AuditableResult<bool>(result: true, $"Revoked from {request.FullName} on {DateTimeOffset.UtcNow:g} by {actor}.")
			: new AuditableResult<bool>(result: true, $"No grants were found to revoke from {request.FullName}.");
	}
}
