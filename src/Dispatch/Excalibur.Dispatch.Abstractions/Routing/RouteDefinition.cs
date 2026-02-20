// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Abstractions.Routing;

/// <summary>
/// Defines a route destination.
/// </summary>
public sealed class RouteDefinition
{
	/// <summary>
	/// Gets or sets the route identifier.
	/// </summary>
	/// <value>
	/// The route identifier.
	/// </value>
	public string RouteId { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the route name.
	/// </summary>
	/// <value>
	/// The route name.
	/// </value>
	public string Name { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the endpoint or destination.
	/// </summary>
	/// <value>
	/// The endpoint or destination.
	/// </value>
	public string Endpoint { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the weight for load balancing (higher = more traffic).
	/// </summary>
	/// <value>
	/// The weight for load balancing (higher = more traffic).
	/// </value>
	public int Weight { get; set; } = 100;

	/// <summary>
	/// Gets route-specific metadata.
	/// </summary>
	/// <value>
	/// Route-specific metadata.
	/// </value>
	public Dictionary<string, object> Metadata { get; } = [];
}
