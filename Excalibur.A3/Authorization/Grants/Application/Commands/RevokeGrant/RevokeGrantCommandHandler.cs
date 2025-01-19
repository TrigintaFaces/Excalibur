using Excalibur.A3.Audit;
using Excalibur.A3.Authorization.Grants.Domain.Model;
using Excalibur.A3.Authorization.Grants.Domain.Repositories;
using Excalibur.A3.Exceptions;
using Excalibur.DataAccess.Exceptions;

using MediatR;

using Microsoft.Extensions.Caching.Distributed;

namespace Excalibur.A3.Authorization.Grants.Application.Commands.RevokeGrant;

/// <summary>
///     Handles the execution of the <see cref="RevokeGrantCommand" />.
/// </summary>
/// <remarks>
///     This handler is responsible for revoking a specific grant from a user. It performs the necessary checks, interacts with the
///     <see cref="IGrantRepository" /> to modify grant data, and invalidates the cache to ensure up-to-date authorization data.
/// </remarks>
internal sealed class RevokeGrantCommandHandler(IGrantRepository grantRepository, IDistributedCache cache)
	: IRequestHandler<RevokeGrantCommand, AuditableResult<bool>>
{
	/// <inheritdoc />
	public async Task<AuditableResult<bool>> Handle(RevokeGrantCommand request, CancellationToken cancellationToken)
	{
		// Prevent users from administering their own grants
		if (request.UserId.Equals(request.AccessToken.UserId, StringComparison.OrdinalIgnoreCase))
		{
			throw NotAuthorizedException.Because(request.AccessToken, "Users are not allowed to administer their own grants.");
		}

		// Create a unique key for the grant to be revoked
		var key = new GrantKey(request.UserId, request.TenantId, request.GrantType, request.Qualifier);

		Grant grant;

		try
		{
			// Attempt to retrieve the grant using the key
			grant = await grantRepository.Read(key.ToString(), cancellationToken).ConfigureAwait(false);
		}
		catch (ResourceNotFoundException)
		{
			return new AuditableResult<bool>(false,
				$"{request.AccessToken.FullName} failed to revoke grant {key.Scope} on {DateTime.Now:g} because the grant was not found and may have already been revoked.");
		}

		// Revoke the grant and delete it from the repository
		grant.Revoke(request.AccessToken.FullName);
		_ = await grantRepository.Delete(grant, cancellationToken).ConfigureAwait(false);

		// Invalidate the cache for the user's grants
		await cache.RemoveAsync(AuthorizationCacheKey.ForGrants(request.UserId), cancellationToken).ConfigureAwait(false);

		// Return an auditable result confirming the revocation
		return new AuditableResult<bool>(true, $"Revoked from {grant.FullName} on {DateTime.Now:g} by {request.AccessToken.FullName}.");
	}
}
