// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Routing;

/// <summary>
/// Represents comprehensive routing information for a message destination, including endpoint details, priority, and metadata.
/// </summary>
/// <remarks>
/// RouteInfo encapsulates all information needed to make intelligent routing decisions and provides operational visibility into available
/// message destinations. It supports priority-based routing, multi-bus architectures, and extensible metadata for custom routing logic.
/// <para> <strong> Priority Semantics: </strong> </para>
/// Higher priority values indicate preferred routes. Routers can use priority for:
/// - Primary/fallback routing strategies
/// - Load balancing with weighted distribution
/// - Circuit breaker integration with endpoint ranking.
/// <para> <strong> Metadata Extensions: </strong> </para>
/// The metadata dictionary enables custom routing behaviors including endpoint-specific configuration, health indicators, capacity
/// information, and integration with external routing services or policy engines.
/// </remarks>
public sealed class RouteInfo
{
	/// <summary>
	/// Initializes a new instance of the <see cref="RouteInfo" /> class with the specified routing parameters.
	/// </summary>
	/// <param name="name"> The human-readable name identifying this route. Cannot be null or empty. </param>
	/// <param name="endpoint"> The destination endpoint identifier (queue name, topic, URL, etc.). Cannot be null or empty. </param>
	/// <param name="priority"> The routing priority where higher values indicate preferred routes. Default is 0. </param>
	/// <remarks>
	/// The route name should be descriptive and unique within the routing context to enable clear operational visibility. The endpoint
	/// should be a valid identifier that the underlying messaging infrastructure can resolve to a deliverable destination.
	/// </remarks>
	/// <exception cref="ArgumentException"> Thrown when name or endpoint is null or empty. </exception>
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
	/// A descriptive name used for logging, monitoring, and operational visibility. Should be unique within the routing context to avoid ambiguity.
	/// </value>
	public string Name { get; }

	/// <summary>
	/// Gets the destination endpoint identifier for message delivery.
	/// </summary>
	/// <value>
	/// The endpoint identifier that specifies where messages should be delivered. Format depends on the messaging infrastructure (e.g.,
	/// queue name, topic ARN, HTTP URL).
	/// </value>
	public string Endpoint { get; }

	/// <summary>
	/// Gets the routing priority for this destination.
	/// </summary>
	/// <value>
	/// A numeric priority value where higher numbers indicate preferred routes. Used for primary/fallback routing, weighted load balancing,
	/// and endpoint ranking.
	/// </value>
	/// <remarks>
	/// Priority values are relative within a routing context. Common patterns include:
	/// - Primary routes: 100+
	/// - Secondary routes: 50-99
	/// - Fallback routes: 1-49
	/// - Disabled routes: 0 or negative values.
	/// </remarks>
	public int Priority { get; }

	/// <summary>
	/// Gets or sets the message bus name for multi-bus routing scenarios.
	/// </summary>
	/// <value>
	/// The identifier of the message bus instance that should handle this route. Null indicates the default or current bus should be used.
	/// </value>
	/// <remarks>
	/// Multi-bus routing enables hybrid messaging architectures where different message types or destinations are handled by different
	/// messaging systems (RabbitMQ, Kafka, Service Bus, etc.). The bus name should match registered bus instances in the dependency
	/// injection container.
	/// </remarks>
	public string? BusName { get; set; }

	/// <summary>
	/// Gets the extensible metadata dictionary for custom routing behaviors and operational information.
	/// </summary>
	/// <value>
	/// A mutable dictionary containing key-value pairs for custom routing logic, endpoint configuration, health status, capacity
	/// information, and integration data.
	/// </value>
	/// <remarks>
	/// <para>
	/// Common metadata keys include:
	/// - "health": Endpoint health status (healthy, degraded, unhealthy)
	/// - "capacity": Current endpoint capacity or load information
	/// - "region": Geographic region for compliance or performance routing
	/// - "tenant": Multi-tenant routing information
	/// - "version": API version or message format requirements
	/// - "timeout": Endpoint-specific timeout configurations
	/// </para>
	/// <para>Custom routing implementations can define domain-specific metadata keys to support advanced routing scenarios and operational requirements.</para>
	/// </remarks>
	public Dictionary<string, object?> Metadata { get; init; } = [];
}
