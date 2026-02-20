// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Collections.Concurrent;
using System.Collections.Frozen;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Routing;
using Excalibur.Dispatch.Abstractions.Transport;
using Excalibur.Dispatch.Abstractions.Validation;
using Excalibur.Dispatch.Diagnostics;
using Excalibur.Dispatch.Messaging;
using Excalibur.Dispatch.Resilience;
using Excalibur.Dispatch.Routing;

using Microsoft.Extensions.Logging;

namespace Excalibur.Dispatch.Delivery.Handlers;

/// <summary>
/// Final step in the dispatch pipeline responsible for sending messages to the appropriate message bus.
/// Retry behavior is provided via <see cref="IRetryPolicy"/>.
/// </summary>
/// <param name="busProvider">Resolves the bus by name.</param>
/// <param name="logger">Used for logging failures.</param>
/// <param name="retryPolicy">Retry policy for transient failures, or <c>null</c> to disable retries.</param>
/// <param name="busOptionsMap">Configuration for individual message buses.</param>
public sealed partial class FinalDispatchHandler(
	IMessageBusProvider busProvider,
	ILogger<FinalDispatchHandler> logger,
	IRetryPolicy? retryPolicy,
	IDictionary<string, IMessageBusOptions> busOptionsMap)
{
	private readonly ILogger<FinalDispatchHandler> _logger = logger ?? throw new ArgumentNullException(nameof(logger));
	private const string ResultContextKey = "Dispatch:Result";
	private const string CacheHitContextKey = "Dispatch:CacheHit";
	private const string LocalBusName = "local";
	private readonly IMessageBus? _cachedLocalBus = ResolveLocalBus(busProvider);
	private readonly bool _localRetriesEnabled = ResolveLocalRetriesEnabled(busOptionsMap, retryPolicy);

	/// <summary>
	/// Publishes the provided message to the message bus determined by routing.
	/// </summary>
	/// <param name="message">Message to dispatch.</param>
	/// <param name="context">Processing context for the message.</param>
	/// <param name="cancellationToken">Token to cancel the dispatch.</param>
	/// <returns>The result of the bus operation.</returns>
	/// <exception cref="InvalidOperationException">Thrown when the message type is not supported.</exception>
	/// <exception cref="Exception">Forwarded if the underlying bus throws.</exception>
	[UnconditionalSuppressMessage(
		"Trimming",
		"IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code",
		Justification = "Result typing relies on known message types registered at startup.")]
	[UnconditionalSuppressMessage(
		"AotAnalysis",
		"IL3050:Calling members annotated with 'RequiresDynamicCodeAttribute' may break functionality when AOT compiling.",
		Justification = "Result typing relies on reflection and is not supported in AOT scenarios.")]
	[SuppressMessage(
		"Design",
		"CA1506:AvoidExcessiveClassCoupling",
		Justification = "Final dispatch composes the end-to-end pipeline across messaging abstractions and transports.")]
	public async ValueTask<IMessageResult> HandleAsync(
		IDispatchMessage message,
		IMessageContext context,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(message);
		ArgumentNullException.ThrowIfNull(context);

		var routingDecision = context.RoutingDecision;
		if (message is IDispatchAction actionMessage && routingDecision?.Endpoints is not { Count: > 0 })
		{
			return await HandleLocalActionFastPathAsync(actionMessage, context, routingDecision, cancellationToken)
				.ConfigureAwait(false);
		}

		if (message is IDispatchEvent eventMessage && routingDecision?.Endpoints is not { Count: > 0 })
		{
			return await HandleLocalEventFastPathAsync(eventMessage, context, routingDecision, cancellationToken)
				.ConfigureAwait(false);
		}

		if (message is IDispatchDocument documentMessage && routingDecision?.Endpoints is not { Count: > 0 })
		{
			return await HandleLocalDocumentFastPathAsync(documentMessage, context, routingDecision, cancellationToken)
				.ConfigureAwait(false);
		}

		if (routingDecision?.Endpoints is { Count: 1 } singleEndpoints)
		{
			return await HandleSingleTargetAsync(message, context, singleEndpoints[0], routingDecision, cancellationToken)
				.ConfigureAwait(false);
		}

		var routes = GetTargetRoutes(routingDecision);
		if (message is IDispatchAction && routes.Count > 1)
		{
			routes = [routes[0]];
		}

		var targets = new List<(IRouteResult Route, IMessageBus Bus)>(routes.Count);
		foreach (var route in routes)
		{
			var name = route.MessageBusName;
			if (!busProvider.TryGet(name, out var resolvedBus) || resolvedBus is null)
			{
				route.DeliveryStatus = RouteDeliveryStatus.Failed;
				route.Failure = new RouteFailure($"No message bus registered for '{name}'");

				LogNoMessageBusFound(_logger, name);
				var problemDetails = new MessageProblemDetails
				{
					Type = ProblemDetailsTypes.Routing,
					Title = "Routing failed",
					Status = 404,
					Detail = $"No message bus registered for '{name}'",
					Instance = Guid.NewGuid().ToString(),
				};
				var failed = new Messaging.MessageResult(
					succeeded: false,
					problemDetails: problemDetails,
					routingDecision: routingDecision,
					validationResult: context.ValidationResult() as IValidationResult,
					authorizationResult: context.AuthorizationResult() as IAuthorizationResult);
				return failed;
			}

			targets.Add((route, resolvedBus));
		}

		async Task<IMessageResult> PublishToBusesAsync<TMessage>(
			TMessage payload,
			Func<IMessageBus, TMessage, IMessageContext, CancellationToken, Task> publish)
		{
			var failures = new List<string>();

			foreach (var target in targets)
			{
				_ = busOptionsMap.TryGetValue(target.Route.MessageBusName, out var options);
				var policy = options?.EnableRetries == true && retryPolicy != null
					? retryPolicy
					: NoOpRetryPolicy.Instance;

				try
				{
					await policy.ExecuteAsync(
						ct => publish(target.Bus, payload, context, ct),
						cancellationToken).ConfigureAwait(false);
					target.Route.DeliveryStatus = RouteDeliveryStatus.Succeeded;
					target.Route.Failure = null;
				}
				catch (Exception ex)
				{
					target.Route.DeliveryStatus = RouteDeliveryStatus.Failed;
					target.Route.Failure = RouteFailure.FromException(ex);
					failures.Add($"{target.Route.MessageBusName}: {ex.Message}");
					LogUnhandledExceptionDuringDispatch(_logger, message.GetType().Name, ex);
				}
			}

			if (failures.Count > 0)
			{
				var problemDetails = new MessageProblemDetails
				{
					Type = ProblemDetailsTypes.Transport,
					Title = "Final dispatch failed",
					Status = 500,
					Detail = $"Failed to publish to {failures.Count} bus(es): {string.Join("; ", failures)}",
					Instance = Guid.NewGuid().ToString(),
				};
				return new Messaging.MessageResult(
					succeeded: false,
					problemDetails: problemDetails,
					routingDecision: routingDecision,
					validationResult: context.ValidationResult() as IValidationResult,
					authorizationResult: context.AuthorizationResult() as IAuthorizationResult);
			}

			return new Messaging.MessageResult(
				succeeded: true,
				routingDecision: routingDecision,
				validationResult: context.ValidationResult() as IValidationResult,
				authorizationResult: context.AuthorizationResult() as IAuthorizationResult);
		}

		// Handle all events (IDispatchEvent and IIntegrationEvent which inherits from it)
		if (message is IDispatchEvent dispatchEvent)
		{
			return await PublishToBusesAsync(
					dispatchEvent,
					static (bus, evt, ctx, token) => bus.PublishAsync(evt, ctx, token))
				.ConfigureAwait(false);
		}

		if (message is IDispatchDocument doc)
		{
			return await PublishToBusesAsync(
					doc,
					static (bus, document, ctx, token) => bus.PublishAsync(document, ctx, token))
				.ConfigureAwait(false);
		}

		if (message is IDispatchAction action)
		{
			var primary = targets[0];
			_ = busOptionsMap.TryGetValue(primary.Route.MessageBusName, out var primaryOptions);
			var retriesEnabled = primaryOptions?.EnableRetries == true && retryPolicy != null;

			try
			{
				async Task<IMessageResult> ExecutePrimaryActionAsync(CancellationToken ct)
				{
					// Skip handler execution if result already exists in context (cache hit scenario)
					if (!HasContextResult(context))
					{
						await primary.Bus.PublishAsync(action, context, ct).ConfigureAwait(false);
					}

					primary.Route.DeliveryStatus = RouteDeliveryStatus.Succeeded;
					primary.Route.Failure = null;
					return CreateTypedResult(action, context);
				}

				if (!retriesEnabled)
				{
					return await ExecutePrimaryActionAsync(cancellationToken).ConfigureAwait(false);
				}

				return await retryPolicy!.ExecuteAsync(ExecutePrimaryActionAsync, cancellationToken).ConfigureAwait(false);
			}
			catch (Exception ex)
			{
				LogUnhandledExceptionDuringDispatch(_logger, message.GetType().Name, ex);
				primary.Route.DeliveryStatus = RouteDeliveryStatus.Failed;
				primary.Route.Failure = RouteFailure.FromException(ex);

				var problemDetails = new MessageProblemDetails
				{
					Type = ProblemDetailsTypes.HandlerError,
					Title = "Final dispatch failed",
					Status = 500,
					Detail = ex.Message,
					Instance = Guid.NewGuid().ToString(),
				};
				return new Messaging.MessageResult(
					succeeded: false,
					problemDetails: problemDetails,
					routingDecision: routingDecision,
					validationResult: context.ValidationResult() as IValidationResult,
					authorizationResult: context.AuthorizationResult() as IAuthorizationResult);
			}
		}

		var unsupported = new MessageProblemDetails
		{
			Type = ProblemDetailsTypes.Routing,
			Title = "Unsupported message type",
			Status = 400,
			Detail = $"Message type '{message.GetType().Name}' is not supported.",
			Instance = Guid.NewGuid().ToString(),
		};
		return new Messaging.MessageResult(
			succeeded: false,
			problemDetails: unsupported,
			routingDecision: context.RoutingDecision,
			validationResult: context.ValidationResult() as IValidationResult,
			authorizationResult: context.AuthorizationResult() as IAuthorizationResult);
	}

	// PERF-7: Non-async wrapper avoids state machine allocation when handler completes synchronously.
	private ValueTask<IMessageResult> HandleLocalActionFastPathAsync(
		IDispatchAction action,
		IMessageContext context,
		RoutingDecision? routingDecision,
		CancellationToken cancellationToken)
	{
		if (!TryGetLocalBus(out var localBus) || localBus is null)
		{
			return new ValueTask<IMessageResult>(CreateNoLocalBusResult(routingDecision, context));
		}

		try
		{
			if (!_localRetriesEnabled)
			{
				if (!HasContextResult(context))
				{
					var publishTask = localBus.PublishAsync(action, context, cancellationToken);
					if (!publishTask.IsCompletedSuccessfully)
					{
						return HandleLocalActionFastPathSlowAsync(publishTask, action, context, routingDecision);
					}
				}

				return new ValueTask<IMessageResult>(CreateTypedResult(action, context));
			}

			return HandleLocalActionFastPathWithRetryAsync(localBus, action, context, routingDecision, cancellationToken);
		}
		catch (Exception ex)
		{
			return new ValueTask<IMessageResult>(CreateHandlerErrorResult(ex, action, routingDecision, context));
		}
	}

	private async ValueTask<IMessageResult> HandleLocalActionFastPathSlowAsync(
		Task publishTask,
		IDispatchAction action,
		IMessageContext context,
		RoutingDecision? routingDecision)
	{
		try
		{
			await publishTask.ConfigureAwait(false);
			return CreateTypedResult(action, context);
		}
		catch (Exception ex)
		{
			return CreateHandlerErrorResult(ex, action, routingDecision, context);
		}
	}

	private async ValueTask<IMessageResult> HandleLocalActionFastPathWithRetryAsync(
		IMessageBus localBus,
		IDispatchAction action,
		IMessageContext context,
		RoutingDecision? routingDecision,
		CancellationToken cancellationToken)
	{
		try
		{
			async Task<IMessageResult> ExecuteLocalAsync(CancellationToken ct)
			{
				if (!HasContextResult(context))
				{
					await localBus.PublishAsync(action, context, ct).ConfigureAwait(false);
				}

				return CreateTypedResult(action, context);
			}

			return await retryPolicy!.ExecuteAsync(ExecuteLocalAsync, cancellationToken).ConfigureAwait(false);
		}
		catch (Exception ex)
		{
			return CreateHandlerErrorResult(ex, action, routingDecision, context);
		}
	}

	// PERF-7: Non-async wrapper avoids state machine allocation when handler completes synchronously.
	private ValueTask<IMessageResult> HandleLocalEventFastPathAsync(
		IDispatchEvent dispatchEvent,
		IMessageContext context,
		RoutingDecision? routingDecision,
		CancellationToken cancellationToken)
	{
		if (!TryGetLocalBus(out var localBus) || localBus is null)
		{
			return new ValueTask<IMessageResult>(CreateNoLocalBusResult(routingDecision, context));
		}

		try
		{
			if (!_localRetriesEnabled)
			{
				var publishTask = localBus.PublishAsync(dispatchEvent, context, cancellationToken);
				if (!publishTask.IsCompletedSuccessfully)
				{
					return HandleLocalEventFastPathSlowAsync(publishTask, context, routingDecision);
				}

				return new ValueTask<IMessageResult>(CreateSuccessResult(context, routingDecision));
			}

			return HandleLocalEventFastPathWithRetryAsync(localBus, dispatchEvent, context, routingDecision, cancellationToken);
		}
		catch (Exception ex)
		{
			return new ValueTask<IMessageResult>(CreateEventHandlerErrorResult(ex, dispatchEvent, routingDecision, context));
		}
	}

	private async ValueTask<IMessageResult> HandleLocalEventFastPathSlowAsync(
		Task publishTask,
		IMessageContext context,
		RoutingDecision? routingDecision)
	{
		try
		{
			await publishTask.ConfigureAwait(false);
			return CreateSuccessResult(context, routingDecision);
		}
		catch (Exception ex)
		{
			return CreateEventHandlerErrorResult(ex, null, routingDecision, context);
		}
	}

	private async ValueTask<IMessageResult> HandleLocalEventFastPathWithRetryAsync(
		IMessageBus localBus,
		IDispatchEvent dispatchEvent,
		IMessageContext context,
		RoutingDecision? routingDecision,
		CancellationToken cancellationToken)
	{
		try
		{
			async Task<IMessageResult> ExecuteLocalAsync(CancellationToken ct)
			{
				await localBus.PublishAsync(dispatchEvent, context, ct).ConfigureAwait(false);
				return CreateSuccessResult(context, routingDecision);
			}

			return await retryPolicy!.ExecuteAsync(ExecuteLocalAsync, cancellationToken).ConfigureAwait(false);
		}
		catch (Exception ex)
		{
			return CreateEventHandlerErrorResult(ex, dispatchEvent, routingDecision, context);
		}
	}

	// PERF-7: Non-async wrapper avoids state machine allocation when handler completes synchronously.
	private ValueTask<IMessageResult> HandleLocalDocumentFastPathAsync(
		IDispatchDocument document,
		IMessageContext context,
		RoutingDecision? routingDecision,
		CancellationToken cancellationToken)
	{
		if (!TryGetLocalBus(out var localBus) || localBus is null)
		{
			return new ValueTask<IMessageResult>(CreateNoLocalBusResult(routingDecision, context));
		}

		try
		{
			if (!_localRetriesEnabled)
			{
				var publishTask = localBus.PublishAsync(document, context, cancellationToken);
				if (!publishTask.IsCompletedSuccessfully)
				{
					return HandleLocalDocumentFastPathSlowAsync(publishTask, context, routingDecision);
				}

				return new ValueTask<IMessageResult>(CreateSuccessResult(context, routingDecision));
			}

			return HandleLocalDocumentFastPathWithRetryAsync(localBus, document, context, routingDecision, cancellationToken);
		}
		catch (Exception ex)
		{
			return new ValueTask<IMessageResult>(CreateDocumentHandlerErrorResult(ex, document, routingDecision, context));
		}
	}

	private async ValueTask<IMessageResult> HandleLocalDocumentFastPathSlowAsync(
		Task publishTask,
		IMessageContext context,
		RoutingDecision? routingDecision)
	{
		try
		{
			await publishTask.ConfigureAwait(false);
			return CreateSuccessResult(context, routingDecision);
		}
		catch (Exception ex)
		{
			return CreateDocumentHandlerErrorResult(ex, null, routingDecision, context);
		}
	}

	private async ValueTask<IMessageResult> HandleLocalDocumentFastPathWithRetryAsync(
		IMessageBus localBus,
		IDispatchDocument document,
		IMessageContext context,
		RoutingDecision? routingDecision,
		CancellationToken cancellationToken)
	{
		try
		{
			async Task<IMessageResult> ExecuteLocalAsync(CancellationToken ct)
			{
				await localBus.PublishAsync(document, context, ct).ConfigureAwait(false);
				return CreateSuccessResult(context, routingDecision);
			}

			return await retryPolicy!.ExecuteAsync(ExecuteLocalAsync, cancellationToken).ConfigureAwait(false);
		}
		catch (Exception ex)
		{
			return CreateDocumentHandlerErrorResult(ex, document, routingDecision, context);
		}
	}

	private async ValueTask<IMessageResult> HandleSingleTargetAsync(
		IDispatchMessage message,
		IMessageContext context,
		string busName,
		RoutingDecision? routingDecision,
		CancellationToken cancellationToken)
	{
		if (!busProvider.TryGet(busName, out var resolvedBus) || resolvedBus is null)
		{
			LogNoMessageBusFound(_logger, busName);
			var problemDetails = new MessageProblemDetails
			{
				Type = ProblemDetailsTypes.Routing,
				Title = "Routing failed",
				Status = 404,
				Detail = $"No message bus registered for '{busName}'",
				Instance = Guid.NewGuid().ToString(),
			};

			return new Messaging.MessageResult(
				succeeded: false,
				problemDetails: problemDetails,
				routingDecision: routingDecision,
				validationResult: context.ValidationResult() as IValidationResult,
				authorizationResult: context.AuthorizationResult() as IAuthorizationResult);
		}

		_ = busOptionsMap.TryGetValue(busName, out var options);
		var policy = options?.EnableRetries == true && retryPolicy != null
			? retryPolicy
			: NoOpRetryPolicy.Instance;
		var useNoOpPolicy = policy is NoOpRetryPolicy;

		try
		{
			if (message is IDispatchAction action)
			{
				if (useNoOpPolicy)
				{
					if (!HasContextResult(context))
					{
						var publishTask = resolvedBus.PublishAsync(action, context, cancellationToken);
						if (!publishTask.IsCompletedSuccessfully)
						{
							await publishTask.ConfigureAwait(false);
						}
					}

					return CreateTypedResult(action, context);
				}

				async Task<IMessageResult> ExecuteActionAsync(CancellationToken ct)
				{
					if (!HasContextResult(context))
					{
						await resolvedBus.PublishAsync(action, context, ct).ConfigureAwait(false);
					}

					return CreateTypedResult(action, context);
				}

				return await policy.ExecuteAsync(ExecuteActionAsync, cancellationToken).ConfigureAwait(false);
			}

			if (message is IDispatchEvent dispatchEvent)
			{
				if (useNoOpPolicy)
				{
					var publishTask = resolvedBus.PublishAsync(dispatchEvent, context, cancellationToken);
					if (!publishTask.IsCompletedSuccessfully)
					{
						await publishTask.ConfigureAwait(false);
					}

					return CreateSuccessResult(context, routingDecision);
				}

				await policy.ExecuteAsync(
					ct => resolvedBus.PublishAsync(dispatchEvent, context, ct),
					cancellationToken).ConfigureAwait(false);
				return CreateSuccessResult(context, routingDecision);
			}

			if (message is IDispatchDocument document)
			{
				if (useNoOpPolicy)
				{
					var publishTask = resolvedBus.PublishAsync(document, context, cancellationToken);
					if (!publishTask.IsCompletedSuccessfully)
					{
						await publishTask.ConfigureAwait(false);
					}

					return CreateSuccessResult(context, routingDecision);
				}

				await policy.ExecuteAsync(
					ct => resolvedBus.PublishAsync(document, context, ct),
					cancellationToken).ConfigureAwait(false);
				return CreateSuccessResult(context, routingDecision);
			}
		}
		catch (Exception ex)
		{
			LogUnhandledExceptionDuringDispatch(_logger, message.GetType().Name, ex);

			var problemDetails = new MessageProblemDetails
			{
				Type = ProblemDetailsTypes.HandlerError,
				Title = "Final dispatch failed",
				Status = 500,
				Detail = ex.Message,
				Instance = Guid.NewGuid().ToString(),
			};

			return new Messaging.MessageResult(
				succeeded: false,
				problemDetails: problemDetails,
				routingDecision: routingDecision,
				validationResult: context.ValidationResult() as IValidationResult,
				authorizationResult: context.AuthorizationResult() as IAuthorizationResult);
		}

		var unsupported = new MessageProblemDetails
		{
			Type = ProblemDetailsTypes.Routing,
			Title = "Unsupported message type",
			Status = 400,
			Detail = $"Message type '{message.GetType().Name}' is not supported.",
			Instance = Guid.NewGuid().ToString(),
		};

		return new Messaging.MessageResult(
			succeeded: false,
			problemDetails: unsupported,
			routingDecision: context.RoutingDecision,
			validationResult: context.ValidationResult() as IValidationResult,
			authorizationResult: context.AuthorizationResult() as IAuthorizationResult);
	}

	private static IMessageResult CreateSuccessResult(IMessageContext context, RoutingDecision? routingDecision)
	{
		var cacheHit = IsCacheHit(context);
		var validationResult = context.ValidationResult();
		var authorizationResult = context.AuthorizationResult();

		if (routingDecision is null && validationResult is null && authorizationResult is null)
		{
			return cacheHit ? SimpleMessageResult.SuccessCacheHitResult : SimpleMessageResult.SuccessResult;
		}

		return Abstractions.MessageResult.Success(
			routingDecision,
			validationResult,
			authorizationResult,
			cacheHit);
	}

	#region PERF-7 Error Result Helpers

	// PERF-7: Shared error result factories eliminate duplicate MessageProblemDetails construction
	// across the three fast-path methods. Error paths are cold, so the indirection has no impact.

	private static IMessageResult CreateNoLocalBusResult(RoutingDecision? routingDecision, IMessageContext context)
	{
		var problemDetails = new MessageProblemDetails
		{
			Type = ProblemDetailsTypes.Routing,
			Title = "Routing failed",
			Status = 404,
			Detail = $"No message bus registered for '{LocalBusName}'",
			Instance = Guid.NewGuid().ToString(),
		};

		return new Messaging.MessageResult(
			succeeded: false,
			problemDetails: problemDetails,
			routingDecision: routingDecision,
			validationResult: context.ValidationResult() as IValidationResult,
			authorizationResult: context.AuthorizationResult() as IAuthorizationResult);
	}

	private IMessageResult CreateHandlerErrorResult(
		Exception ex,
		IDispatchAction? action,
		RoutingDecision? routingDecision,
		IMessageContext context)
	{
		if (action is not null)
		{
			LogUnhandledExceptionDuringDispatch(_logger, action.GetType().Name, ex);
		}

		var problemDetails = new MessageProblemDetails
		{
			Type = ProblemDetailsTypes.HandlerError,
			Title = "Final dispatch failed",
			Status = 500,
			Detail = ex.Message,
			Instance = Guid.NewGuid().ToString(),
		};

		return new Messaging.MessageResult(
			succeeded: false,
			problemDetails: problemDetails,
			routingDecision: routingDecision,
			validationResult: context.ValidationResult() as IValidationResult,
			authorizationResult: context.AuthorizationResult() as IAuthorizationResult);
	}

	private IMessageResult CreateEventHandlerErrorResult(
		Exception ex,
		IDispatchEvent? dispatchEvent,
		RoutingDecision? routingDecision,
		IMessageContext context)
	{
		if (dispatchEvent is not null)
		{
			LogUnhandledExceptionDuringDispatch(_logger, dispatchEvent.GetType().Name, ex);
		}

		var problemDetails = new MessageProblemDetails
		{
			Type = ProblemDetailsTypes.HandlerError,
			Title = "Final dispatch failed",
			Status = 500,
			Detail = ex.Message,
			Instance = Guid.NewGuid().ToString(),
		};

		return new Messaging.MessageResult(
			succeeded: false,
			problemDetails: problemDetails,
			routingDecision: routingDecision,
			validationResult: context.ValidationResult() as IValidationResult,
			authorizationResult: context.AuthorizationResult() as IAuthorizationResult);
	}

	private IMessageResult CreateDocumentHandlerErrorResult(
		Exception ex,
		IDispatchDocument? document,
		RoutingDecision? routingDecision,
		IMessageContext context)
	{
		if (document is not null)
		{
			LogUnhandledExceptionDuringDispatch(_logger, document.GetType().Name, ex);
		}

		var problemDetails = new MessageProblemDetails
		{
			Type = ProblemDetailsTypes.HandlerError,
			Title = "Final dispatch failed",
			Status = 500,
			Detail = ex.Message,
			Instance = Guid.NewGuid().ToString(),
		};

		return new Messaging.MessageResult(
			succeeded: false,
			problemDetails: problemDetails,
			routingDecision: routingDecision,
			validationResult: context.ValidationResult() as IValidationResult,
			authorizationResult: context.AuthorizationResult() as IAuthorizationResult);
	}

	#endregion

	private bool TryGetLocalBus(out IMessageBus? bus)
	{
		bus = _cachedLocalBus;
		return bus is not null || busProvider.TryGet(LocalBusName, out bus);
	}

	private static IReadOnlyList<IRouteResult> GetTargetRoutes(RoutingDecision? routingDecision)
	{
		if (routingDecision?.Endpoints is { Count: > 0 } endpoints)
		{
			var routes = new List<IRouteResult>(endpoints.Count);
			for (var i = 0; i < endpoints.Count; i++)
			{
				routes.Add(new RouteResult(endpoints[i]));
			}

			return routes;
		}

		return [new RouteResult("local")];
	}

	private static IMessageBus? ResolveLocalBus(IMessageBusProvider provider)
	{
		_ = provider.TryGet(LocalBusName, out var localBus);
		return localBus;
	}

	private static bool ResolveLocalRetriesEnabled(
		IDictionary<string, IMessageBusOptions> optionsMap,
		IRetryPolicy? configuredRetryPolicy)
	{
		if (configuredRetryPolicy is null)
		{
			return false;
		}

		return optionsMap.TryGetValue(LocalBusName, out var localOptions) && localOptions?.EnableRetries == true;
	}

	#region Result Factory Cache (PERF-6, PERF-13/PERF-14)

	/// <summary>
	/// Cached factory for creating typed MessageResult.Success instances.
	/// Eliminates per-dispatch reflection overhead.
	/// </summary>
	/// <remarks>
	/// PERF-13/PERF-14: Uses three-phase lazy freeze pattern for optimal lookup performance:
	/// <list type="number">
	/// <item>Warmup phase: ConcurrentDictionary for thread-safe population during startup</item>
	/// <item>Freeze transition: ToFrozenDictionary() when cache stabilizes</item>
	/// <item>Frozen phase: FrozenDictionary for zero-sync O(1) lookups</item>
	/// </list>
	/// Call <see cref="FreezeResultFactoryCache"/> after handler registration is complete (e.g., via UseOptimizedDispatch).
	/// </remarks>
	private static class ResultFactoryCache
	{
		/// <summary>
		/// Factory delegate signature using object? to avoid interface type argument issues.
		/// Parameters: result, routing, validation, authorization, cacheHit
		/// </summary>
		internal delegate IMessageResult SuccessFactory(object? result, object? routing, object? validation, object? authorization,
			bool cacheHit);

		internal delegate IMessageResult LeanSuccessFactory(object? result, bool cacheHit);

		/// <summary>
		/// Warmup cache for thread-safe population during startup (PERF-13/PERF-14).
		/// Null after freeze is called.
		/// </summary>
		private static ConcurrentDictionary<Type, SuccessFactory>? _warmupCache = new();

		private static ConcurrentDictionary<Type, LeanSuccessFactory>? _leanWarmupCache = new();

		/// <summary>
		/// Frozen cache for optimal read performance after warmup (PERF-13/PERF-14).
		/// Null until freeze is called.
		/// </summary>
		private static FrozenDictionary<Type, SuccessFactory>? _frozenCache;

		private static FrozenDictionary<Type, LeanSuccessFactory>? _leanFrozenCache;

		/// <summary>
		/// Flag indicating if the cache has been frozen.
		/// </summary>
		private static volatile bool _isFrozen;

		/// <summary>
		/// The base generic method for MessageResult.Success&lt;T&gt; with 5 parameters, cached once.
		/// </summary>
		private static readonly MethodInfo? GenericSuccessMethod5Params;

		/// <summary>
		/// The base generic method for MessageResult.Success&lt;T&gt; with 1 parameter, cached once.
		/// </summary>
		private static readonly MethodInfo? GenericSuccessMethod1Param;

		static ResultFactoryCache()
		{
			var methods = typeof(Abstractions.MessageResult).GetMethods(BindingFlags.Public | BindingFlags.Static);

			GenericSuccessMethod5Params = methods.FirstOrDefault(m =>
				m is { Name: nameof(Abstractions.MessageResult.Success), IsGenericMethodDefinition: true } &&
				m.GetGenericArguments().Length == 1 &&
				m.GetParameters().Length == 5);

			GenericSuccessMethod1Param = methods.FirstOrDefault(m =>
				m is { Name: nameof(Abstractions.MessageResult.Success), IsGenericMethodDefinition: true } &&
				m.GetGenericArguments().Length == 1 &&
				m.GetParameters().Length == 1);
		}

		/// <summary>
		/// Gets a value indicating whether the cache has been frozen.
		/// </summary>
		internal static bool IsCacheFrozen => _isFrozen;

		/// <summary>
		/// Gets or creates a factory delegate for the specified result type.
		/// </summary>
		[RequiresUnreferencedCode("Uses reflection to create typed method delegates")]
		[RequiresDynamicCode("Creates generic methods at runtime")]
		internal static SuccessFactory GetOrCreateFactory(Type resultType)
		{
			// PERF-13/PERF-14: Three-phase lazy freeze pattern
			if (_isFrozen)
			{
				// Phase 3 (frozen): Fast path with zero synchronization overhead
				if (_frozenCache.TryGetValue(resultType, out var frozenFactory))
				{
					return frozenFactory;
				}

				// Cache miss after freeze - build but don't cache (rare case)
				return CreateFactory(resultType);
			}

			// Phase 1 (warmup): Thread-safe population using ConcurrentDictionary
			return _warmupCache.GetOrAdd(resultType, CreateFactory);
		}

		[RequiresUnreferencedCode("Uses reflection to create typed method delegates")]
		[RequiresDynamicCode("Creates generic methods at runtime")]
		internal static LeanSuccessFactory GetOrCreateLeanFactory(Type resultType)
		{
			if (_isFrozen)
			{
				if (_leanFrozenCache != null && _leanFrozenCache.TryGetValue(resultType, out var frozenFactory))
				{
					return frozenFactory;
				}

				return CreateLeanFactory(resultType);
			}

			return _leanWarmupCache!.GetOrAdd(resultType, CreateLeanFactory);
		}

		/// <summary>
		/// Freezes the factory cache for optimal read performance (PERF-13/PERF-14).
		/// </summary>
		internal static void Freeze()
		{
			if (_isFrozen)
			{
				return;
			}

			var warmup = _warmupCache;
			if (warmup is null)
			{
				return;
			}

			// Phase 2 (freeze transition): Convert to FrozenDictionary
			_frozenCache = warmup.ToFrozenDictionary();
			_leanFrozenCache = _leanWarmupCache!.ToFrozenDictionary();
			_isFrozen = true;
			_warmupCache = null; // Allow GC to collect warmup dictionary
			_leanWarmupCache = null;
		}

		/// <summary>
		/// Clears the internal factory cache. Primarily intended for testing scenarios.
		/// </summary>
		internal static void Clear()
		{
			_isFrozen = false;
			_frozenCache = null;
			_leanFrozenCache = null;
			_warmupCache = new();
			_leanWarmupCache = new();
		}

		[RequiresUnreferencedCode("Uses reflection to create typed method delegates")]
		[RequiresDynamicCode("Creates generic methods at runtime")]
		private static SuccessFactory CreateFactory(Type resultType)
		{
			Expression BuildResultValueExpression(ParameterExpression resultParam)
			{
				// Preserve legacy reflection behavior: null -> default(T) for non-nullable value types.
				if (resultType.IsValueType && Nullable.GetUnderlyingType(resultType) is null)
				{
					return Expression.Condition(
						Expression.Equal(resultParam, Expression.Constant(null, typeof(object))),
						Expression.Default(resultType),
						Expression.Convert(resultParam, resultType));
				}

				return Expression.Convert(resultParam, resultType);
			}

			if (GenericSuccessMethod5Params != null)
			{
				var typedMethod = GenericSuccessMethod5Params.MakeGenericMethod(resultType);
				var resultParam = Expression.Parameter(typeof(object), "result");
				var routingParam = Expression.Parameter(typeof(object), "routing");
				var validationParam = Expression.Parameter(typeof(object), "validation");
				var authorizationParam = Expression.Parameter(typeof(object), "authorization");
				var cacheHitParam = Expression.Parameter(typeof(bool), "cacheHit");

				var call = Expression.Call(
					typedMethod,
					BuildResultValueExpression(resultParam),
					Expression.Convert(routingParam, typeof(RoutingDecision)),
					validationParam,
					authorizationParam,
					cacheHitParam);

				var castResult = Expression.Convert(call, typeof(IMessageResult));
				return Expression.Lambda<SuccessFactory>(
					castResult,
					resultParam,
					routingParam,
					validationParam,
					authorizationParam,
					cacheHitParam).Compile();
			}

			if (GenericSuccessMethod1Param != null)
			{
				var typedMethod = GenericSuccessMethod1Param.MakeGenericMethod(resultType);
				var resultParam = Expression.Parameter(typeof(object), "result");
				var routingParam = Expression.Parameter(typeof(object), "routing");
				var validationParam = Expression.Parameter(typeof(object), "validation");
				var authorizationParam = Expression.Parameter(typeof(object), "authorization");
				var cacheHitParam = Expression.Parameter(typeof(bool), "cacheHit");

				var call = Expression.Call(
					typedMethod,
					BuildResultValueExpression(resultParam));
				var castResult = Expression.Convert(call, typeof(IMessageResult));

				return Expression.Lambda<SuccessFactory>(
					castResult,
					resultParam,
					routingParam,
					validationParam,
					authorizationParam,
					cacheHitParam).Compile();
			}

			// Ultimate fallback - shouldn't happen
			return (result, _, _, _, _) => Abstractions.MessageResult.Success(result);
		}

		[RequiresUnreferencedCode("Uses reflection to create typed constructors")]
		[RequiresDynamicCode("Creates generic methods at runtime")]
		private static LeanSuccessFactory CreateLeanFactory(Type resultType)
		{
			var simpleResultType = typeof(SimpleMessageResultOfT<>).MakeGenericType(resultType);
			var ctor = simpleResultType.GetConstructor([resultType, typeof(bool)]);
			if (ctor is null)
			{
				return (result, _) => Abstractions.MessageResult.Success(result);
			}

			Expression BuildResultValueExpression(ParameterExpression resultParam)
			{
				if (resultType.IsValueType && Nullable.GetUnderlyingType(resultType) is null)
				{
					return Expression.Condition(
						Expression.Equal(resultParam, Expression.Constant(null, typeof(object))),
						Expression.Default(resultType),
						Expression.Convert(resultParam, resultType));
				}

				return Expression.Convert(resultParam, resultType);
			}

			var resultParam = Expression.Parameter(typeof(object), "result");
			var cacheHitParam = Expression.Parameter(typeof(bool), "cacheHit");
			var newExpr = Expression.New(ctor, BuildResultValueExpression(resultParam), cacheHitParam);
			var castExpr = Expression.Convert(newExpr, typeof(IMessageResult));
			return Expression.Lambda<LeanSuccessFactory>(castExpr, resultParam, cacheHitParam).Compile();
		}
	}

	/// <summary>
	/// Gets a value indicating whether the result factory cache has been frozen.
	/// </summary>
	public static bool IsResultFactoryCacheFrozen => ResultFactoryCache.IsCacheFrozen;

	/// <summary>
	/// Freezes the result factory cache for optimal read performance (PERF-13/PERF-14).
	/// </summary>
	/// <remarks>
	/// Call this method after all result types have been encountered (e.g., after warmup).
	/// Once frozen, the cache uses <see cref="FrozenDictionary{TKey, TValue}"/> for O(1) lookups
	/// with zero synchronization overhead.
	/// </remarks>
	public static void FreezeResultFactoryCache() => ResultFactoryCache.Freeze();

	/// <summary>
	/// Clears the result factory cache. Primarily intended for testing scenarios.
	/// </summary>
	internal static void ClearResultFactoryCache() => ResultFactoryCache.Clear();

	#endregion

	[RequiresUnreferencedCode("This method uses reflection to create typed MessageResult instances and set properties dynamically")]
	[RequiresDynamicCode("This method uses reflection to construct generic methods at runtime.")]
	private static IMessageResult CreateTypedResult(IDispatchAction action, IMessageContext context)
	{
		var result = TryGetContextResult(context);

		var resultType = result?.GetType();

		if (resultType is null)
		{
			var actionType = action.GetType();
			resultType = GetActionResultType(actionType);

			// Fast path for non-response actions: avoid reflective generic result factory.
			if (resultType is null)
			{
				var routingDecision = context.RoutingDecision;
				var validationResult = context.ValidationResult();
				var authorizationResult = context.AuthorizationResult();
				var nonResponseCacheHit = context.GetItem(CacheHitContextKey, false);
				return Abstractions.MessageResult.Success(
					routingDecision,
					validationResult,
					authorizationResult,
					nonResponseCacheHit);
			}
		}

		if (result is null && resultType.GetConstructor(Type.EmptyTypes) is null && resultType.IsClass)
		{
			throw new InvalidOperationException(
				string.Format(
					CultureInfo.CurrentCulture,
					Resources.FinalDispatchHandler_ReturnTypeMustHaveParameterlessCtorFormat,
					resultType.FullName));
		}

		// Read cache hit flag from context
		var cacheHit = IsCacheHit(context);
		var routing = context.RoutingDecision;
		var validation = context.ValidationResult();
		var authorization = context.AuthorizationResult();

		if (routing is null && validation is null && authorization is null)
		{
			var leanFactory = ResultFactoryCache.GetOrCreateLeanFactory(resultType);
			return leanFactory(result, cacheHit);
		}

#if AOT_ENABLED
		// AOT path: Use source-generated factory registry (no reflection/MakeGenericMethod)
		var aotFactory = ResultFactoryRegistry.GetFactory(resultType);
		if (aotFactory != null)
		{
			return aotFactory(
				result,
				routing,
				validation,
				authorization as IAuthorizationResult,
				cacheHit);
		}
#endif

		// Use cached factory delegate instead of per-dispatch reflection
		var factory = ResultFactoryCache.GetOrCreateFactory(resultType);
		return factory(
			result,
			routing,
			validation,
			authorization,
			cacheHit);
	}

	private static readonly ConcurrentDictionary<Type, Type?> ActionResultTypeCache = new();

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static object? TryGetContextResult(IMessageContext context)
	{
		if (context.Result is not null)
		{
			return context.Result;
		}

		if (context is MessageContext messageContext &&
		    messageContext.TryGetItemFast(ResultContextKey, out var fastCachedValue))
		{
			return fastCachedValue;
		}

		return context.GetItem<object?>(ResultContextKey);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static bool HasContextResult(IMessageContext context) => TryGetContextResult(context) is not null;

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static bool IsCacheHit(IMessageContext context)
	{
		if (context is MessageContext messageContext &&
		    messageContext.TryGetItemFast(CacheHitContextKey, out var fastValue) &&
		    fastValue is bool fastFlag)
		{
			return fastFlag;
		}

		return context.GetItem(CacheHitContextKey, false);
	}

	[RequiresUnreferencedCode("This method uses reflection to inspect generic interface types")]
	private static Type? GetActionResultType(Type actionType) =>
		ActionResultTypeCache.GetOrAdd(
			actionType,
			static type =>
				type.GetInterfaces()
					.FirstOrDefault(static i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IDispatchAction<>))
					?.GetGenericArguments()[0]);

	// Source-generated logging methods
	[LoggerMessage(DeliveryEventId.FinalDispatchNoBusFound, LogLevel.Error,
		"No message bus found for name '{BusName}'")]
	private static partial void LogNoMessageBusFound(ILogger logger, string? busName);

	[LoggerMessage(DeliveryEventId.FinalDispatchFailed, LogLevel.Error,
		"Unhandled exception during final dispatch of {MessageType}")]
	private static partial void LogUnhandledExceptionDuringDispatch(
		ILogger logger,
		string messageType,
		Exception ex);
}
