// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Routing;

/// <summary>
/// Provides metadata that influences how a message is routed through the messaging infrastructure.
/// </summary>
internal interface IRouteMetadata
{
	/// <summary>
	/// Gets the preferred message bus name for routing this message.
	/// </summary>
	string? TargetBus { get; }

	/// <summary>
	/// Gets a value indicating whether the message should bypass local handlers and always be routed remotely.
	/// </summary>
	bool ForceRemote { get; }

	/// <summary>
	/// Gets the routing key for content-based routing scenarios.
	/// </summary>
	string? RoutingKey { get; }

	/// <summary>
	/// Gets the activity name this message represents for authorization and routing purposes.
	/// </summary>
	string? ActivityName { get; }

	/// <summary>
	/// Gets the target queue name for point-to-point messaging patterns.
	/// </summary>
	string? QueueName { get; }

	/// <summary>
	/// Gets the target topic name for publish/subscribe messaging patterns.
	/// </summary>
	string? TopicName { get; }
}
