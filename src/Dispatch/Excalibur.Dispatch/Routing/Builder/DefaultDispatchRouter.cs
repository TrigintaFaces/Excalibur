// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Routing;

using AbstractionRouteInfo = Excalibur.Dispatch.Abstractions.Routing.RouteInfo;

namespace Excalibur.Dispatch.Routing.Builder;

/// <summary>
/// Default implementation of <see cref="IDispatchRouter"/> that combines
/// transport selection and endpoint routing.
/// </summary>
/// <remarks>
/// This router coordinates between <see cref="ITransportSelector"/> and
/// <see cref="IEndpointRouter"/> to produce a unified <see cref="RoutingDecision"/>
/// containing both the selected transport and target endpoints.
/// </remarks>
internal sealed class DefaultDispatchRouter : IDispatchRouter
{
	private readonly ITransportSelector _transportSelector;
	private readonly IEndpointRouter _endpointRouter;

	/// <summary>
	/// Initializes a new instance of the <see cref="DefaultDispatchRouter"/> class.
	/// </summary>
	/// <param name="transportSelector">The transport selector.</param>
	/// <param name="endpointRouter">The endpoint router.</param>
	public DefaultDispatchRouter(
		ITransportSelector transportSelector,
		IEndpointRouter endpointRouter)
	{
		_transportSelector = transportSelector ?? throw new ArgumentNullException(nameof(transportSelector));
		_endpointRouter = endpointRouter ?? throw new ArgumentNullException(nameof(endpointRouter));
	}

	/// <inheritdoc/>
	public async ValueTask<RoutingDecision> RouteAsync(
		IDispatchMessage message,
		IMessageContext context,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(message);
		ArgumentNullException.ThrowIfNull(context);

		var matchedRules = new List<string>();

		// Select transport
		var transport = await _transportSelector
			.SelectTransportAsync(message, context, cancellationToken)
			.ConfigureAwait(false);

		if (!string.IsNullOrEmpty(transport))
		{
			matchedRules.Add($"transport:{transport}");
		}

		// Route to endpoints
		var endpoints = await _endpointRouter
			.RouteToEndpointsAsync(message, context, cancellationToken)
			.ConfigureAwait(false);

		foreach (var endpoint in endpoints)
		{
			matchedRules.Add($"endpoint:{endpoint}");
		}

		// Build the routing decision
		if (string.IsNullOrEmpty(transport))
		{
			return RoutingDecision.Failure("No transport could be selected for the message");
		}

		return RoutingDecision.Success(transport, endpoints, matchedRules);
	}

	/// <inheritdoc/>
	public bool CanRouteTo(IDispatchMessage message, string destination)
	{
		ArgumentNullException.ThrowIfNull(message);
		ArgumentException.ThrowIfNullOrEmpty(destination);

		// Check if destination is a configured transport
		var transports = _transportSelector.GetAvailableTransports(message.GetType());
		if (transports.Contains(destination, StringComparer.OrdinalIgnoreCase))
		{
			return true;
		}

		// Check if destination is a configured endpoint
		return _endpointRouter.CanRouteToEndpoint(message, destination);
	}

	/// <inheritdoc/>
	public IEnumerable<AbstractionRouteInfo> GetAvailableRoutes(IDispatchMessage message, IMessageContext context)
	{
		ArgumentNullException.ThrowIfNull(message);
		ArgumentNullException.ThrowIfNull(context);

		var messageType = message.GetType();

		// Get available transports as routes
		var transports = _transportSelector.GetAvailableTransports(messageType);
		var priority = 0;

		foreach (var transport in transports)
		{
			var routeInfo = new AbstractionRouteInfo(
				name: $"transport:{transport}",
				endpoint: transport,
				priority: priority++);

			routeInfo.Metadata["route_type"] = "transport";
			routeInfo.BusName = transport;

			yield return routeInfo;
		}

		// Get endpoint routes
		foreach (var route in _endpointRouter.GetEndpointRoutes(message, context))
		{
			route.Metadata["route_type"] = "endpoint";
			yield return route;
		}
	}
}
