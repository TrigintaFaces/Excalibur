// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Routing.Policies;

/// <summary>
/// Represents a single routing rule loaded from an external policy file.
/// </summary>
public sealed class RoutingRule
{
	/// <summary>
	/// Gets or sets the message type pattern to match (supports wildcards).
	/// </summary>
	/// <value>The message type pattern (e.g., "OrderCreated", "Order*").</value>
	public string MessageTypePattern { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the target transport name for matched messages.
	/// </summary>
	/// <value>The transport name (e.g., "kafka", "rabbitmq", "local").</value>
	public string? Transport { get; set; }

	/// <summary>
	/// Gets or sets the target endpoint for matched messages.
	/// </summary>
	/// <value>The endpoint name or address.</value>
	public string? Endpoint { get; set; }

	/// <summary>
	/// Gets or sets the priority of this rule. Lower values have higher priority.
	/// </summary>
	/// <value>The rule priority. Defaults to 100.</value>
	public int Priority { get; set; } = 100;

	/// <summary>
	/// Gets or sets a value indicating whether this rule is enabled.
	/// </summary>
	/// <value><see langword="true"/> if enabled; otherwise, <see langword="false"/>. Defaults to <see langword="true"/>.</value>
	public bool Enabled { get; set; } = true;
}

/// <summary>
/// Represents the root object of a routing policy file.
/// </summary>
public sealed class RoutingPolicyFile
{
	/// <summary>
	/// Gets or sets the list of routing rules.
	/// </summary>
	/// <value>The collection of routing rules.</value>
	public List<RoutingRule> Rules { get; set; } = [];
}
