// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Routing;

/// <summary>
/// Attribute that provides routing metadata for message classes, specifying how messages should be routed through the messaging
/// infrastructure. This attribute controls message delivery behavior, targeting specific buses, queues, topics, and routing patterns.
/// </summary>
/// <param name="busName"> The name of the message bus to use for routing this message type. If null, uses the default bus. </param>
/// <param name="forceRemote"> Whether to force remote delivery even if a local handler is available. Defaults to false. </param>
/// <param name="routingKey">
/// The routing key to use for message routing in exchanges and topic-based systems. If null, uses message type name.
/// </param>
/// <param name="activityName"> The name of the activity for tracing and monitoring purposes. If null, uses the message type name. </param>
/// <param name="queueName"> The specific queue name to route messages to. If null, uses default queue naming conventions. </param>
/// <param name="topicName"> The specific topic name to publish messages to. If null, uses default topic naming conventions. </param>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class RouteMetadataAttribute(
	string? busName,
	bool forceRemote = false,
	string? routingKey = null,
	string? activityName = null,
	string? queueName = null,
	string? topicName = null) : Attribute, IRouteMetadata
{
	/// <summary>
	/// Gets the name of the message bus to use for routing this message type. When null, the default bus configuration will be used for
	/// message routing.
	/// </summary>
	/// <value> The bus name string, or null to use the default bus. </value>
	public string? BusName { get; init; } = busName;

	/// <summary>
	/// Gets a value indicating whether to force remote delivery even if a local handler is available. When true, messages will always be
	/// sent to the remote messaging infrastructure, bypassing any local in-process handlers.
	/// </summary>
	/// <value> True to force remote delivery; false to allow local processing when possible. </value>
	public bool ForceRemote { get; init; } = forceRemote;

	/// <summary>
	/// Gets the routing key to use for message routing in exchanges and topic-based messaging systems. This key determines which queues or
	/// consumers receive the message in routing scenarios.
	/// </summary>
	/// <value> The routing key string, or null to use default routing based on message type. </value>
	public string? RoutingKey { get; init; } = routingKey;

	/// <summary>
	/// Gets the activity name for tracing and monitoring purposes. This name is used in distributed tracing systems to track message flow
	/// and performance metrics.
	/// </summary>
	/// <value> The activity name string, or null to use the message type name as the activity name. </value>
	public string? ActivityName { get; init; } = activityName;

	/// <summary>
	/// Gets the specific queue name to route messages to. When specified, messages will be sent directly to this queue instead of using
	/// default queue naming conventions or routing logic.
	/// </summary>
	/// <value> The queue name string, or null to use default queue naming. </value>
	public string? QueueName { get; init; } = queueName;

	/// <summary>
	/// Gets the specific topic name to publish messages to. When specified, messages will be published to this topic instead of using
	/// default topic naming conventions or routing logic.
	/// </summary>
	/// <value> The topic name string, or null to use default topic naming. </value>
	public string? TopicName { get; init; } = topicName;

	/// <summary>
	/// Gets the target bus name for the <see cref="IRouteMetadata" /> interface implementation. This property maps to
	/// <see cref="BusName" /> for interface compliance.
	/// </summary>
	/// <value> The target bus name, or null if no specific bus is targeted. </value>
	string? IRouteMetadata.TargetBus => BusName;
}
