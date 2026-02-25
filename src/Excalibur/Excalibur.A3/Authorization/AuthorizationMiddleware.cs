// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Security.Claims;

using Excalibur.Dispatch.Abstractions;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;

namespace Excalibur.A3.Authorization;

/// <summary>
/// Dispatch middleware that handles authorization for messages that implement <see cref="IRequireAuthorization" />
/// or are decorated with <see cref="RequirePermissionAttribute"/>.
/// </summary>
/// <param name="accessToken"> The access token containing user authentication and authorization information. </param>
/// <param name="authorization"> The authorization service to validate permissions. </param>
/// <param name="attributeCache"> The cache for RequirePermission attribute lookups. </param>
public sealed class AuthorizationMiddleware(
	IAccessToken accessToken,
	IDispatchAuthorizationService authorization,
	AttributeAuthorizationCache attributeCache)
	: IDispatchMiddleware
{
	/// <summary>
	/// Gets the middleware execution stage. Authorization middleware runs during the Authorization stage.
	/// </summary>
	/// <value> The middleware execution stage, set to <see cref="DispatchMiddlewareStage.Authorization" />. </value>
	public DispatchMiddlewareStage? Stage => DispatchMiddlewareStage.Authorization;

	/// <summary>
	/// Executes authorization logic for messages that require authorization.
	/// </summary>
	/// <param name="message"> The dispatch message being processed. </param>
	/// <param name="context"> The message context containing metadata. </param>
	/// <param name="nextDelegate"> The next middleware delegate in the pipeline. </param>
	/// <param name="cancellationToken"> Token to cancel the operation. </param>
	/// <returns> The result of the message processing, including authorization status. </returns>
	[System.Diagnostics.CodeAnalysis.RequiresDynamicCode("Authorization middleware uses reflection for dynamic type resolution.")]
	[System.Diagnostics.CodeAnalysis.RequiresUnreferencedCode("Authorization middleware may access types that could be trimmed.")]
	[System.Diagnostics.CodeAnalysis.SuppressMessage("Maintainability", "CA1506:Avoid excessive class coupling",
		Justification = "Authorization middleware requires coordination of multiple authorization types by design.")]
	public async ValueTask<IMessageResult> InvokeAsync(
		IDispatchMessage message,
		IMessageContext context,
		DispatchRequestDelegate nextDelegate,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(message);
		ArgumentNullException.ThrowIfNull(context);
		ArgumentNullException.ThrowIfNull(nextDelegate);

		// Check both interface-based and attribute-based authorization
		var hasInterface = message is IRequireAuthorization;
		var attributes = attributeCache.GetAttributes(message.GetType());
		var hasAttributes = attributes.Length > 0;

		// Skip authorization if neither interface nor attributes are present
		if (!hasInterface && !hasAttributes)
		{
			return await nextDelegate(message, context, cancellationToken).ConfigureAwait(false);
		}

		var principal = BuildPrincipal(accessToken);
		var requirements = new List<IAuthorizationRequirement>();
		string? activityName = null;

		// 1. Add interface-based requirements (existing logic)
		if (message is IRequireAuthorization requirement)
		{
			activityName = requirement.ActivityName;

			switch (requirement)
			{
				case IRequireRoleAuthorization { RequiredRoles.Count: > 0 } roleAuthorization:
					requirements.AddRange(roleAuthorization.RequiredRoles
						.Select(r => new ClaimsAuthorizationRequirement(ClaimTypes.Role, [r])));
					break;

				case IRequireActivityAuthorization activityAuthorization when !string.IsNullOrWhiteSpace(activityAuthorization.ResourceId):
					requirements.Add(new GrantsAuthorizationRequirement(
						activityAuthorization.ActivityName,
						activityAuthorization.ResourceTypes,
						activityAuthorization.ResourceId));
					break;

				case IRequireActivityAuthorization activityAuthorization:
					requirements.Add(new GrantsAuthorizationRequirement(
						requirement.ActivityName,
						activityAuthorization.ResourceTypes));
					break;

				case IRequireCustomAuthorization custom:
					requirements.AddRange(custom.AuthorizationRequirements);
					break;
			}
		}

		// 2. Add attribute-based requirements (NEW - AND logic with interface requirements)
		foreach (var attr in attributes)
		{
			// Use first permission as activity name if not set by interface
			activityName ??= attr.Permission;

			// Extract resource ID from property if specified
			var resourceId = attributeCache.ExtractResourceId(message, attr.ResourceIdProperty);

			requirements.Add(new GrantsAuthorizationRequirement(
				attr.Permission,
				attr.ResourceTypes ?? [],
				resourceId));
		}

		var result = await authorization
			.AuthorizeAsync(principal, activityName ?? "unknown", [.. requirements])
			.ConfigureAwait(false);

		context.AuthorizationResult(result);

		if (!result.IsAuthorized)
		{
			return Dispatch.Messaging.MessageResult.Failed(
				problemDetails: new MessageProblemDetails
				{
					Type = "about:blank",
					Title = "Authorization Failed",
					ErrorCode = 403,
					Status = 403,
					Detail = result.FailureMessage ?? "Authorization failed",
					Instance = string.Empty,
				},
				routingDecision: Dispatch.Abstractions.Routing.RoutingDecision.Success("local", []),
				validationResult: Dispatch.Abstractions.Serialization.SerializableValidationResult.Success(),
				authorizationResult: Dispatch.Abstractions.AuthorizationResult.Failed(
					result.FailureMessage ?? "Authorization failed"));
		}

		return await nextDelegate(message, context, cancellationToken).ConfigureAwait(false);
	}

	private static ClaimsPrincipal BuildPrincipal(IAccessToken accessToken)
	{
		switch (accessToken)
		{
			case not null:
				var identity = new ClaimsIdentity(accessToken.Claims, accessToken.IsAuthenticated() ? "token" : "anonymous");
				return new ClaimsPrincipal(identity);

			default:
				return new ClaimsPrincipal(new ClaimsIdentity());
		}
	}
}
