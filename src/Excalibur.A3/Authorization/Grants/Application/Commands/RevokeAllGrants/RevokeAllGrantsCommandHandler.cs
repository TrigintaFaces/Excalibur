using Excalibur.A3.Audit;
using Excalibur.A3.Authorization.Grants.Domain.Model;
using Excalibur.A3.Authorization.Grants.Domain.Repositories;
using Excalibur.A3.Exceptions;

using MediatR;

using Microsoft.Extensions.Caching.Distributed;

namespace Excalibur.A3.Authorization.Grants.Application.Commands.RevokeAllGrants;

/// <summary>
///     Handles the execution of the <see cref="RevokeAllGrantsCommand" />.
/// </summary>
/// <remarks>
///     This handler revokes all non-expired grants for a specified user and clears the related cache entries to ensure updated state for
///     subsequent authorization checks.
/// </remarks>
internal sealed class RevokeAllGrantsCommandHandler(IGrantRepository grantRepository, IDistributedCache cache)
	: IRequestHandler<RevokeAllGrantsCommand, AuditableResult<bool>>
{
	/// <inheritdoc />
	public async Task<AuditableResult<bool>> Handle(RevokeAllGrantsCommand request, CancellationToken cancellationToken)
	{
		// Prevent self-administration of grants
		if (request.UserId.Equals(request.AccessToken.UserId, StringComparison.OrdinalIgnoreCase))
		{
			throw NotAuthorizedException.Because(request.AccessToken, "Users are not allowed to administer their own grants.");
		}

		// Actor performing the revocation
		var actor = request.AccessToken.FullName;

		// Retrieve all applicable grants for the user
		var applicationGrants = (await grantRepository.ReadAll(request.UserId).ConfigureAwait(false))
			.Where(g => g.Scope.GrantType != GrantType.ActivityGroup).ToArray();

		// Revoke and delete each grant
		foreach (var grant in applicationGrants)
		{
			if (!grant.IsExpired())
			{
				grant.Revoke(actor);
			}

			_ = await grantRepository.Delete(grant, cancellationToken).ConfigureAwait(false);
		}

		// Clear the user's grants from the cache
		await cache.RemoveAsync(AuthorizationCacheKey.ForGrants(request.UserId), cancellationToken).ConfigureAwait(false);

		// Return an audit result
		return applicationGrants.Length > 0
			? new AuditableResult<bool>(true, $"Revoked from {request.FullName} on {DateTime.Now:g} by {actor}.")
			: new AuditableResult<bool>(true, $"No grants were found to revoke from {request.FullName}.");
	}
}
