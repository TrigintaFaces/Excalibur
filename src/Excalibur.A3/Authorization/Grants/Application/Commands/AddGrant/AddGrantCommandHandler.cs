using Excalibur.A3.Audit;
using Excalibur.A3.Authorization.Grants.Domain.Model;
using Excalibur.A3.Authorization.Grants.Domain.Repositories;
using Excalibur.A3.Exceptions;

using MediatR;

using Microsoft.Extensions.Caching.Distributed;

namespace Excalibur.A3.Authorization.Grants.Application.Commands.AddGrant;

/// <summary>
///     Handles the execution of the <see cref="AddGrantCommand" />, which adds a new grant to a user.
/// </summary>
/// <remarks>
///     This handler checks for existing grants, ensures the request is valid, and updates the repository with the new grant. It also
///     manages caching to ensure grant changes are reflected immediately.
/// </remarks>
/// <param name="grantRepository"> The repository for managing grants. </param>
/// <param name="cache"> The distributed cache for caching grant data. </param>
internal sealed class AddGrantCommandHandler(IGrantRepository grantRepository, IDistributedCache cache)
	: IRequestHandler<AddGrantCommand, AuditableResult<bool>>
{
	/// <inheritdoc />
	public async Task<AuditableResult<bool>> Handle(AddGrantCommand request, CancellationToken cancellationToken)
	{
		// Prevent users from administering their own grants.
		if (request.UserId.Equals(request.AccessToken.UserId, StringComparison.OrdinalIgnoreCase))
		{
			throw NotAuthorizedException.Because(request.AccessToken, "Users are not allowed to administer their own grants.");
		}

		// Generate the unique key for the grant.
		var key = new GrantKey(request.UserId, request.TenantId, request.GrantType, request.Qualifier);

		// Check if the grant already exists in the repository.
		var grant = await grantRepository.Read(key.ToString(), cancellationToken).ConfigureAwait(false);

		// Check if the grant already exists in the repository.
		if (grant.IsExpired())
		{
			_ = await grantRepository.Delete(grant, cancellationToken).ConfigureAwait(false);
		}
		else
		{
			// If the grant exists and is active, return a failure result with an audit message.
			return new AuditableResult<bool>(false,
				$"{request.AccessToken.FullName} failed to grant {request.FullName} on {DateTime.Now:g} because the grant was already in effect.");
		}

		// Create a new grant with the specified details.
		grant = new Grant(
			request.UserId,
			request.FullName,
			request.TenantId,
			request.GrantType,
			request.Qualifier,
			request.ExpiresOn,
			request.AccessToken.FullName);

		// Save the new grant to the repository.
		_ = await grantRepository.Save(grant, cancellationToken).ConfigureAwait(false);

		// Remove the relevant cache entry to reflect changes.
		await cache.RemoveAsync(AuthorizationCacheKey.ForGrants(request.UserId), cancellationToken).ConfigureAwait(false);

		// Return a success result with an audit message.
		return new AuditableResult<bool>(true, $"Granted to {request.FullName} on {DateTime.Now:g} by {request.AccessToken.FullName}.");
	}
}
