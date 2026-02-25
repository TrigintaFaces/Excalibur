// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Routing;
using Excalibur.Dispatch.Abstractions.Validation;
using Excalibur.Dispatch.Diagnostics;

using Microsoft.Extensions.Logging;

namespace Excalibur.Dispatch.Routing;

/// <summary>
/// Middleware responsible for determining the message bus route for dispatched messages.
/// </summary>
/// <remarks>
/// <para>
/// This middleware uses <see cref="IDispatchRouter"/> which combines transport selection and
/// endpoint routing into a single <see cref="RoutingDecision"/>. Configure routing using the
/// fluent builder API via <c>UseRouting()</c>.
/// </para>
/// </remarks>
public sealed partial class RoutingMiddleware : IDispatchMiddleware
{
	private readonly IDispatchRouter _router;
	private readonly ILogger<RoutingMiddleware> _logger;

	/// <summary>
	/// Initializes a new instance of the <see cref="RoutingMiddleware"/> class.
	/// </summary>
	/// <param name="router">The unified dispatch router.</param>
	/// <param name="logger">Logger for diagnostic information.</param>
	/// <exception cref="ArgumentNullException">Thrown when router or logger is null.</exception>
	public RoutingMiddleware(
		IDispatchRouter router,
		ILogger<RoutingMiddleware> logger)
	{
		_router = router ?? throw new ArgumentNullException(nameof(router));
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
	}

	/// <summary>
	/// Gets the pipeline stage where this middleware executes.
	/// </summary>
	/// <value>Returns <see cref="DispatchMiddlewareStage.Routing"/>.</value>
	public DispatchMiddlewareStage? Stage => DispatchMiddlewareStage.Routing;

	/// <summary>
	/// Determines the route for the message and updates the context with routing information.
	/// </summary>
	/// <param name="message">The message being routed.</param>
	/// <param name="context">The message context to update with routing results.</param>
	/// <param name="nextDelegate">The next middleware in the pipeline.</param>
	/// <param name="cancellationToken">Token to cancel the operation.</param>
	/// <returns>The result from the next middleware if routing succeeds, or a failure result with 404 status if routing fails.</returns>
	/// <exception cref="ArgumentNullException">Thrown when required parameters are null.</exception>
	public async ValueTask<IMessageResult> InvokeAsync(
		IDispatchMessage message,
		IMessageContext context,
		DispatchRequestDelegate nextDelegate,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(message);
		ArgumentNullException.ThrowIfNull(context);
		ArgumentNullException.ThrowIfNull(nextDelegate);

		if (!TryGetUsableRoutingDecision(context, out var decision))
		{
			decision = await _router.RouteAsync(message, context, cancellationToken)
				.ConfigureAwait(false);
			context.RoutingDecision = decision;
		}

		if (!decision.IsSuccess)
		{
			LogRoutingFailed(decision.FailureReason ?? "unspecified");
			return CreateFailureResult(decision.FailureReason, context);
		}

		LogMessageRouted(decision.Transport);
		LogRoutingComplete(decision.Transport, decision.Endpoints.Count);

		return await nextDelegate(message, context, cancellationToken).ConfigureAwait(false);
	}

	private static bool TryGetUsableRoutingDecision(IMessageContext context, out RoutingDecision decision)
	{
		decision = context.RoutingDecision!;
		if (decision is null)
		{
			return false;
		}

		// Guard against fake context dummy values; only skip router for meaningful precomputed decisions.
		return !string.IsNullOrEmpty(decision.Transport) || !string.IsNullOrEmpty(decision.FailureReason);
	}

	private IMessageResult CreateFailureResult(string? failureReason, IMessageContext context)
	{
		var problemDetails = new MessageProblemDetails
		{
			Type = ProblemDetailsTypes.Routing,
			Title = "Routing failed",
			Status = 404,
			Detail = $"Routing failed: {failureReason ?? "unspecified"}",
			Instance = Guid.NewGuid().ToString(),
		};
		return new Excalibur.Dispatch.Messaging.MessageResult(
			succeeded: false,
			problemDetails: problemDetails,
			routingDecision: context.RoutingDecision,
			validationResult: context.ValidationResult() as IValidationResult,
			authorizationResult: context.AuthorizationResult() as IAuthorizationResult);
	}

	// Source-generated logging methods
	[LoggerMessage(MiddlewareEventId.RoutingFailed, LogLevel.Warning,
		"Routing failed: {Reason}")]
	private partial void LogRoutingFailed(string reason);

	[LoggerMessage(MiddlewareEventId.MessageRouted, LogLevel.Information,
		"Message routed to: {Target}")]
	private partial void LogMessageRouted(string? target);

	[LoggerMessage(MiddlewareEventId.UnifiedRoutingComplete, LogLevel.Debug,
		"Routing completed: transport={Transport}, endpoints={EndpointCount}")]
	private partial void LogRoutingComplete(string transport, int endpointCount);
}
