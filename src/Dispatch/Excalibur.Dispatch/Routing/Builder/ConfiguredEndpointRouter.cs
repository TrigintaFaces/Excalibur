// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections.Concurrent;

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Routing;

using AbstractionRouteInfo = Excalibur.Dispatch.Abstractions.Routing.RouteInfo;

namespace Excalibur.Dispatch.Routing.Builder;

/// <summary>
/// Endpoint router implementation that uses the fluent builder configuration.
/// </summary>
/// <remarks>
/// This implementation evaluates endpoint routing rules configured via the
/// <see cref="IRoutingBuilder"/> fluent API and supports multicast delivery
/// to multiple endpoints.
/// </remarks>
internal sealed class ConfiguredEndpointRouter : IEndpointRouter
{
	private readonly RoutingConfiguration _configuration;
	private readonly ConcurrentDictionary<Type, IReadOnlyList<string>> _typeToEndpointsCache = new();

	/// <summary>
	/// Initializes a new instance of the <see cref="ConfiguredEndpointRouter"/> class.
	/// </summary>
	/// <param name="configuration">The routing configuration.</param>
	public ConfiguredEndpointRouter(RoutingConfiguration configuration)
	{
		_configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
	}

	/// <inheritdoc/>
	public ValueTask<IReadOnlyList<string>> RouteToEndpointsAsync(
		IDispatchMessage message,
		IMessageContext context,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(message);
		ArgumentNullException.ThrowIfNull(context);

		var messageType = message.GetType();
		var endpoints = new List<string>();
		var hasConditionalRules = false;

		// Check if we have conditional rules for this message type
		foreach (var rule in _configuration.EndpointRules)
		{
			if (rule.MessageType.IsAssignableFrom(messageType) && rule.Predicate is not null)
			{
				hasConditionalRules = true;
				break;
			}
		}

		// If no conditional rules, use cache
		if (!hasConditionalRules && _typeToEndpointsCache.TryGetValue(messageType, out var cached))
		{
			return new ValueTask<IReadOnlyList<string>>(cached);
		}

		// Evaluate rules
		foreach (var rule in _configuration.EndpointRules)
		{
			if (!rule.MessageType.IsAssignableFrom(messageType))
			{
				continue;
			}

			// If there's a predicate, evaluate it
			if (rule.Predicate is not null)
			{
				if (rule.Predicate(message, context))
				{
					endpoints.AddRange(rule.Endpoints);
				}
			}
			else
			{
				// Unconditional rule
				endpoints.AddRange(rule.Endpoints);
			}
		}

		// Remove duplicates while preserving order
		var distinctEndpoints = endpoints.Distinct().ToList().AsReadOnly();

		// If no endpoints matched and we have a fallback, use it
		if (distinctEndpoints.Count == 0 && _configuration.FallbackEndpoint is not null)
		{
			distinctEndpoints = new List<string> { _configuration.FallbackEndpoint }.AsReadOnly();
		}

		// Cache if no conditional rules
		if (!hasConditionalRules)
		{
			_typeToEndpointsCache.TryAdd(messageType, distinctEndpoints);
		}

		return new ValueTask<IReadOnlyList<string>>(distinctEndpoints);
	}

	/// <inheritdoc/>
	public bool CanRouteToEndpoint(IDispatchMessage message, string endpoint)
	{
		ArgumentNullException.ThrowIfNull(message);
		ArgumentException.ThrowIfNullOrEmpty(endpoint);

		var messageType = message.GetType();

		foreach (var rule in _configuration.EndpointRules)
		{
			if (rule.MessageType.IsAssignableFrom(messageType) &&
				rule.Endpoints.Contains(endpoint, StringComparer.OrdinalIgnoreCase))
			{
				return true;
			}
		}

		// Check fallback
		return string.Equals(_configuration.FallbackEndpoint, endpoint, StringComparison.OrdinalIgnoreCase);
	}

	/// <inheritdoc/>
	public IEnumerable<AbstractionRouteInfo> GetEndpointRoutes(IDispatchMessage message, IMessageContext context)
	{
		ArgumentNullException.ThrowIfNull(message);
		ArgumentNullException.ThrowIfNull(context);

		var messageType = message.GetType();
		var priority = 0;

		foreach (var rule in _configuration.EndpointRules)
		{
			if (!rule.MessageType.IsAssignableFrom(messageType))
			{
				continue;
			}

			foreach (var endpoint in rule.Endpoints)
			{
				var routeInfo = new AbstractionRouteInfo(
					name: $"endpoint-rule-{priority}",
					endpoint: endpoint,
					priority: priority);

				routeInfo.Metadata["rule_type"] = rule.Predicate is null ? "unconditional" : "conditional";
				routeInfo.Metadata["message_type"] = messageType.Name;

				yield return routeInfo;
			}

			priority++;
		}

		// Include fallback if configured
		if (_configuration.FallbackEndpoint is not null)
		{
			var fallbackRoute = new AbstractionRouteInfo(
				name: "fallback",
				endpoint: _configuration.FallbackEndpoint,
				priority: int.MaxValue);

			fallbackRoute.Metadata["rule_type"] = "fallback";
			fallbackRoute.Metadata["is_fallback"] = true;

			if (_configuration.FallbackReason is not null)
			{
				fallbackRoute.Metadata["fallback_reason"] = _configuration.FallbackReason;
			}

			yield return fallbackRoute;
		}
	}
}
