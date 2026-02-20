// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Abstractions.Routing;

/// <summary>
/// Represents comprehensive routing information for a message destination,
/// including endpoint details, priority, and metadata.
/// </summary>
/// <remarks>
/// <para>
/// RouteInfo encapsulates all information needed to make intelligent routing
/// decisions and provides operational visibility into available message destinations.
/// It supports priority-based routing, multi-bus architectures, and extensible
/// metadata for custom routing logic.
/// </para>
/// <para><strong>Priority Semantics:</strong></para>
/// <para>
/// Lower priority values indicate higher precedence (evaluated first).
/// This follows the convention used in ASP.NET Core routing.
/// </para>
/// <para><strong>Metadata Extensions:</strong></para>
/// <para>
/// The metadata dictionary enables custom routing behaviors including
/// endpoint-specific configuration, health indicators, capacity information,
/// and integration with external routing services or policy engines.
/// </para>
/// </remarks>
public sealed class RouteInfo
{
	/// <summary>
	/// Initializes a new instance of the <see cref="RouteInfo"/> class.
	/// </summary>
	/// <param name="name">The human-readable name identifying this route.</param>
	/// <param name="endpoint">The destination endpoint identifier.</param>
	/// <param name="priority">The routing priority (lower values = higher precedence).</param>
	/// <exception cref="ArgumentException">
	/// Thrown when <paramref name="name"/> or <paramref name="endpoint"/> is null or empty.
	/// </exception>
	public RouteInfo(string name, string endpoint, int priority = 0)
	{
		ArgumentException.ThrowIfNullOrEmpty(name);
		ArgumentException.ThrowIfNullOrEmpty(endpoint);

		Name = name;
		Endpoint = endpoint;
		Priority = priority;
	}

	/// <summary>
	/// Gets the human-readable name that identifies this route.
	/// </summary>
	/// <value>
	/// A descriptive name used for logging, monitoring, and operational visibility.
	/// </value>
	public string Name { get; }

	/// <summary>
	/// Gets the destination endpoint identifier for message delivery.
	/// </summary>
	/// <value>
	/// The endpoint identifier that specifies where messages should be delivered
	/// (e.g., service name, queue name, topic).
	/// </value>
	public string Endpoint { get; }

	/// <summary>
	/// Gets the routing priority for this destination.
	/// </summary>
	/// <value>
	/// A numeric priority value where lower numbers indicate higher precedence.
	/// Routes are evaluated in ascending priority order.
	/// </value>
	public int Priority { get; }

	/// <summary>
	/// Gets or sets the message bus name for multi-bus routing scenarios.
	/// </summary>
	/// <value>
	/// The identifier of the message bus instance that should handle this route.
	/// <see langword="null"/> indicates the default bus should be used.
	/// </value>
	public string? BusName { get; set; }

	/// <summary>
	/// Gets the extensible metadata dictionary for custom routing behaviors.
	/// </summary>
	/// <value>
	/// A mutable dictionary containing key-value pairs for custom routing logic,
	/// endpoint configuration, health status, and integration data.
	/// </value>
	/// <remarks>
	/// Common metadata keys include:
	/// <list type="bullet">
	/// <item><c>health</c>: Endpoint health status</item>
	/// <item><c>region</c>: Geographic region for compliance or performance routing</item>
	/// <item><c>rule_type</c>: The type of routing rule that matched</item>
	/// <item><c>stop_on_match</c>: Whether rule evaluation should stop on match</item>
	/// <item><c>is_fallback</c>: Whether this is a fallback route</item>
	/// </list>
	/// </remarks>
	public Dictionary<string, object?> Metadata { get; init; } = [];
}
