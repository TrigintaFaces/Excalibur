using Excalibur.A3.Authorization.Requests;
using Excalibur.A3.Exceptions;

using MediatR;

namespace Excalibur.A3.Authorization;

/// <summary>
///     Implements behavior for authorizing requests in the pipeline.
/// </summary>
/// <typeparam name="TRequest"> The type of the request being processed. </typeparam>
/// <typeparam name="TResponse"> The type of the response returned by the request handler. </typeparam>
/// <param name="accessToken"> The access token used to determine authorization. </param>
public class AuthorizationBehavior<TRequest, TResponse>(IAccessToken accessToken)
	: IPipelineBehavior<TRequest, TResponse> where TRequest : notnull
{
	/// <inheritdoc />
	public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(request);
		ArgumentNullException.ThrowIfNull(next);

		// Check if the request is authorizable
		if (request is not IAmAuthorizable authorizable)
		{
			return await next().ConfigureAwait(false);
		}

		// Ensure the user is authenticated
		if (accessToken.IsAnonymous())
		{
			throw NotAuthorizedException.BecauseNotAuthenticated();
		}

		// Extract activity name and resource ID for authorization checks
		var activityName = request.GetType().Name;
		var resourceId = (request as IAmAuthorizableForResource)?.ResourceId ??
						 throw new InvalidDataException("Request does not contain the required ResourceId for authorization.");

		// Perform authorization check
		if (!accessToken.IsAuthorized(activityName, resourceId))
		{
			throw NotAuthorizedException.BecauseForbidden(accessToken, activityName, resourceId);
		}

		// Associate the access token with the request
		authorizable.AccessToken = accessToken;

		// Proceed to the next step in the pipeline
		return await next().ConfigureAwait(false);
	}
}
