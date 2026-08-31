// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Security.Claims;

using Excalibur.Dispatch;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

using MR = Excalibur.Dispatch.MessageResult;

namespace Excalibur.A3.Authorization;

/// <summary>
/// Dispatch middleware that handles authorization for messages that implement <see cref="IRequireAuthorization" />
/// or are decorated with <see cref="RequirePermissionAttribute"/>.
/// </summary>
/// <param name="authorization"> The authorization service to validate permissions. </param>
/// <param name="attributeCache"> The cache for RequirePermission attribute lookups. </param>
/// <param name="conditionEvaluator"> The evaluator for When condition expressions. </param>
/// <remarks>
/// The caller's <see cref="IAccessToken"/> is resolved from the message's own request scope on every
/// invocation and never held in a field. A middleware instance is built once, from the root provider,
/// and lives for the process: a token captured in a constructor would be one caller's identity and
/// grant set, answering every later message in the process as that first caller. Its registered
/// service lifetime cannot change this, because the instance is materialised once regardless.
/// </remarks>
internal sealed class A3AuthorizationMiddleware(
	IDispatchAuthorizationService authorization,
	AttributeAuthorizationCache attributeCache,
	ConditionExpressionEvaluator conditionEvaluator)
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
	[System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage("Trimming", "IL2046",
		Justification = "This implementation reflects; IDispatchMiddleware does not declare that, because a consumer-authored "
		+ "middleware need not reflect and must not inherit the claim. This type is internal, so the requirement cannot reach a "
		+ "consumer through it; it is declared on AddExcaliburAuthorization, which is where a consumer opts in.")]
	[System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage("AOT", "IL3051",
		Justification = "This implementation requires runtime code generation; IDispatchMiddleware does not declare that, because "
		+ "a consumer-authored middleware need not. This type is internal, so the requirement is declared on "
		+ "AddExcaliburAuthorization, which is where a consumer opts in.")]
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

		// The caller's identity comes from the scope this message is being processed in, never from a
		// field. A provider returned by BuildServiceProvider() is not an IServiceScope while a real
		// scope's provider is, so this accepts the request scope an integration supplies (for example
		// the ASP.NET Core request scope) and rejects the root provider. Resolving a scoped token from
		// the root would cache one caller's grants for the lifetime of the container, which is the same
		// cross-request identity leak in a different place.
		var accessToken = (context.RequestServices as IServiceScope)?.ServiceProvider.GetService<IAccessToken>();
		if (accessToken is null)
		{
			return CreateDeniedResult(
				"No access token is available for this message. Authorization requires a request scope "
				+ "carrying the caller's identity.");
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

		// Evaluate When conditions on attribute-based permissions (after grants pass)
		if (result.IsAuthorized && hasAttributes)
		{
			var conditionDenied = EvaluateConditions(attributes, message, accessToken);
			if (conditionDenied is not null)
			{
				return conditionDenied;
			}
		}

		if (!result.IsAuthorized)
		{
			return MR.Failed(
				new MessageProblemDetails
				{
					Type = "about:blank",
					Title = "Authorization Failed",
					ErrorCode = 403,
					Status = 403,
					Detail = result.FailureMessage ?? "Authorization failed",
					Instance = string.Empty,
				},
				Dispatch.Serialization.SerializableValidationResult.Success(),
				Dispatch.AuthorizationResult.Failed(
					result.FailureMessage ?? "Authorization failed"));
		}

		return await nextDelegate(message, context, cancellationToken).ConfigureAwait(false);
	}

	[System.Diagnostics.CodeAnalysis.RequiresUnreferencedCode(
		"Condition evaluation may access properties via reflection.")]
	private IMessageResult? EvaluateConditions(
		RequirePermissionAttribute[] attributes,
		IDispatchMessage message,
		IAccessToken accessToken)
	{
		// Build subject attributes once (same for all permissions on this message)
		Dictionary<string, string>? subjectAttrs = null;

		foreach (var attr in attributes)
		{
			if (attr.When is null)
			{
				continue;
			}

			var parsedCondition = attributeCache.GetParsedCondition(attr.When);

			// Malformed expression -> deny (fail-closed)
			if (parsedCondition is null)
			{
				return CreateDeniedResult($"Malformed condition expression: {attr.When}");
			}

			// Build subject attributes lazily, once for all When conditions
			subjectAttrs ??= BuildSubjectAttributes(accessToken);
			var actionAttrs = BuildActionAttributes(attr);
			var resourceAttrs = BuildResourceAttributes(message, attr);

			if (!conditionEvaluator.Evaluate(parsedCondition, subjectAttrs, actionAttrs, resourceAttrs))
			{
				return CreateDeniedResult($"Condition not met: {attr.When}");
			}
		}

		return null;
	}

	private static Dictionary<string, string> BuildSubjectAttributes(IAccessToken accessToken)
	{
		var attrs = new Dictionary<string, string>(StringComparer.Ordinal);

		if (accessToken.Claims is not null)
		{
			foreach (var claim in accessToken.Claims)
			{
				// Use last-wins for duplicate claim types (consistent with ClaimsPrincipal behavior)
				attrs[claim.Type] = claim.Value;
			}
		}

		if (accessToken.Login is not null)
		{
			attrs["Login"] = accessToken.Login;
		}

		return attrs;
	}

	private static Dictionary<string, string> BuildActionAttributes(RequirePermissionAttribute attr)
	{
		return new Dictionary<string, string>(StringComparer.Ordinal)
		{
			["Name"] = attr.Permission,
		};
	}

	[System.Diagnostics.CodeAnalysis.RequiresUnreferencedCode(
		"Resource attribute extraction uses reflection.")]
	private Dictionary<string, string> BuildResourceAttributes(
		IDispatchMessage message,
		RequirePermissionAttribute attr)
	{
		var attrs = new Dictionary<string, string>(StringComparer.Ordinal)
		{
			["Type"] = message.GetType().Name,
		};

		var resourceId = attributeCache.ExtractResourceId(message, attr.ResourceIdProperty);
		if (resourceId is not null)
		{
			attrs["Id"] = resourceId;
		}

		if (attr.ResourceTypes is { Length: > 0 })
		{
			attrs["ResourceType"] = attr.ResourceTypes[0];
		}

		return attrs;
	}

	private static IMessageResult CreateDeniedResult(string reason)
	{
		return MR.Failed(
			new MessageProblemDetails
			{
				Type = "about:blank",
				Title = "Condition Not Met",
				ErrorCode = 403,
				Status = 403,
				Detail = reason,
				Instance = string.Empty,
			},
			Dispatch.Serialization.SerializableValidationResult.Success(),
			Dispatch.AuthorizationResult.Failed(reason));
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
