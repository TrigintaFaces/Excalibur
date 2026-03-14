// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions.Routing;

namespace Excalibur.Dispatch.Abstractions.Features;

/// <summary>
/// Feature interface for message routing information.
/// </summary>
public interface IMessageRoutingFeature
{
	/// <summary>
	/// Gets or sets the routing decision for this message.
	/// </summary>
	/// <value>The routing decision, or <see langword="null"/> if not yet routed.</value>
	RoutingDecision? RoutingDecision { get; set; }

	/// <summary>
	/// Gets or sets the partition key for message routing and ordering.
	/// </summary>
	/// <value>The partition key or <see langword="null"/>.</value>
	string? PartitionKey { get; set; }

	/// <summary>
	/// Gets or sets the source system or service that originated this message.
	/// </summary>
	/// <value>The source system identifier or <see langword="null"/>.</value>
	string? Source { get; set; }
}
