// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Dispatch.Abstractions.Routing;

namespace Excalibur.Dispatch.Patterns;

/// <summary>
/// Defines a strategy for selecting routes based on time and context.
/// </summary>
public interface IRoutingStrategy
{
	/// <summary>
	/// Gets the name of the strategy.
	/// </summary>
	/// <value>
	/// The name of the strategy.
	/// </value>
	string Name { get; }

	/// <summary>
	/// Evaluates and selects routes based on the strategy logic.
	/// </summary>
	/// <param name="availableRoutes"> The available routes to choose from. </param>
	/// <param name="context"> The routing context. </param>
	/// <param name="cancellationToken"> The cancellation token. </param>
	/// <returns> The selected routes ordered by preference. </returns>
	Task<IReadOnlyList<RouteDefinition>> SelectRoutesAsync(
		IReadOnlyCollection<RouteDefinition> availableRoutes,
		RoutingContext context,
		CancellationToken cancellationToken);

	/// <summary>
	/// Determines if this strategy can handle the given context.
	/// </summary>
	/// <param name="context"> The routing context to evaluate. </param>
	/// <returns> True if the strategy can handle the context. </returns>
	bool CanHandle(RoutingContext context);
}
